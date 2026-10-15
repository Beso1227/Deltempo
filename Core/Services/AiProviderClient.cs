using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace WinTempCleaner.Services;

/// <summary>Why a provider call failed, in terms the UI can act on.</summary>
public enum AiFailureKind
{
    Unknown,
    /// <summary>API key missing, rejected, or lacking permission. Fix the key.</summary>
    Authentication,
    /// <summary>The model identifier does not exist or the account cannot use it. Fix the model.</summary>
    ModelUnavailable,
    /// <summary>Provider rate limit. Retry later; not the user's fault.</summary>
    RateLimited,
    /// <summary>Local provider (Ollama) not running or unreachable.</summary>
    ProviderUnreachable,
    /// <summary>The request exceeded its timeout - most often a cold local model.</summary>
    Timeout,
    /// <summary>Provider reachable but returned something unusable.</summary>
    MalformedResponse,
}

/// <summary>
/// A provider failure carrying enough classification for the UI to say something useful.
/// Without this, an expired key, a retired model, a rate limit and a dead network all
/// collapsed into one "fallback" report, and the user had no way to tell a working
/// integration from a broken one.
/// </summary>
public sealed class AiProviderException : Exception
{
    public AiProviderException(AiFailureKind kind, string message, Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
    }

    public AiFailureKind Kind { get; }

    /// <summary>True when retrying the identical request could plausibly succeed.</summary>
    public bool IsTransient => Kind is AiFailureKind.RateLimited or AiFailureKind.Timeout or AiFailureKind.ProviderUnreachable;

    /// <summary>Actionable, user-facing explanation.</summary>
    public string UserMessage => Kind switch
    {
        AiFailureKind.Authentication =>
            "The provider rejected the API key. Check the key in Settings, and that it has not expired or been revoked.",
        AiFailureKind.ModelUnavailable =>
            "The provider does not recognise that model name. Pick a current model in Settings - this one has most likely been retired upstream.",
        AiFailureKind.RateLimited =>
            "The provider is rate limiting this key. Wait a moment and try again.",
        AiFailureKind.ProviderUnreachable =>
            "Could not reach the local model server. Start Ollama and confirm it is running.",
        AiFailureKind.Timeout =>
            "The request timed out. A local model that is loading its weights can exceed the timeout on the first call - try again now it is warm.",
        _ => "The AI provider returned an unexpected error.",
    };
}

/// <summary>A provider-agnostic chat request.</summary>
public sealed record AiChatRequest(
    string Provider,
    string? ApiKey,
    string ModelId,
    string SystemPrompt,
    string UserPrompt,
    string? Endpoint = null,
    double Temperature = 0.1);

/// <summary>
/// The single HTTP transport for every AI provider Deltempo talks to.
/// </summary>
/// <remarks>
/// This replaces three near-identical copies of the provider switch, each with its own
/// hardcoded model literals, its own response shape, and its own error handling. One
/// transport means a provider fix lands once, and the three intelligence services
/// (large files, installed apps, startup items) can never drift apart again.
///
/// Design notes:
/// - Timeout is PER REQUEST, not per client. A single shared HttpClient with a 15s timeout
///   made local models unusable, because loading weights into RAM is part of the request.
/// - Cancellation is never converted into a fallback result. A cancelled call is a
///   cancelled call; swallowing it wrote a heuristic answer into the persistent cache.
/// - Ollama is normalised onto /api/chat with a messages array. The copies disagreed:
///   two used /api/generate with a flat prompt, one used /api/chat.
/// </remarks>
public static class AiProviderClient
{
    private static readonly HttpClient HttpClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2)
    })
    {
        // Effectively disabled: every request sets its own timeout via a linked CTS, which
        // is what lets a 120s local model and a 30s hosted call share one client.
        Timeout = Timeout.InfiniteTimeSpan
    };

    static AiProviderClient()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("Deltempo-Guardian", "1.6.0"));
    }

    /// <summary>Sends a chat request and returns the model's raw text output.</summary>
    /// <exception cref="AiProviderException">Always, for any provider-level failure.</exception>
    public static async Task<string> CompleteAsync(AiChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ModelId))
        {
            throw new AiProviderException(AiFailureKind.ModelUnavailable,
                $"No model configured for provider '{request.Provider}'.");
        }

        using var timeoutCts = new CancellationTokenSource(AiModelCatalog.ResolveTimeout(request.Provider));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            HttpResponseMessage response = request.Provider.ToLowerInvariant() switch
            {
                "gemini" => await SendGeminiAsync(request, linked.Token),
                "ollama" => await SendOllamaAsync(request, linked.Token),
                _ => await SendOpenAiCompatibleAsync(request, linked.Token),
            };

            using (response)
            {
                string body = await response.Content.ReadAsStringAsync(linked.Token);

                if (!response.IsSuccessStatusCode)
                {
                    throw ClassifyFailure(response.StatusCode, body, request.Provider);
                }

                return ExtractText(response.IsSuccessStatusCode, body, request.Provider);
            }
        }
        catch (AiProviderException)
        {
            throw;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            // Distinguish OUR timeout from the caller's cancellation. Conflating the two is
            // what let a cancelled batch report success.
            throw new AiProviderException(AiFailureKind.Timeout,
                $"The request to {request.Provider} timed out.");
        }
        catch (OperationCanceledException)
        {
            // Genuine caller cancellation: propagate unchanged, do not cache anything.
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new AiProviderException(AiFailureKind.ProviderUnreachable,
                $"Could not reach {request.Provider}: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new AiProviderException(AiFailureKind.Unknown,
                $"Unexpected error talking to {request.Provider}: {ex.Message}", ex);
        }
    }

    private static async Task<HttpResponseMessage> SendGeminiAsync(AiChatRequest request, CancellationToken ct)
    {
        string url =
            $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(request.ModelId)}:generateContent" +
            $"?key={Uri.EscapeDataString(request.ApiKey ?? string.Empty)}";

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[] { new { text = $"{request.SystemPrompt}\n\n{request.UserPrompt}" } }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = request.Temperature
            }
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        return await HttpClient.PostAsync(url, content, ct);
    }

    private static async Task<HttpResponseMessage> SendOpenAiCompatibleAsync(AiChatRequest request, CancellationToken ct)
    {
        string endpoint = request.Endpoint ?? ResolveOpenAiEndpoint(request.Provider);

        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
        }

        if (endpoint.Contains("openrouter.ai", StringComparison.OrdinalIgnoreCase))
        {
            message.Headers.Add("HTTP-Referer", "https://github.com/Beso1227/Deltempo");
            message.Headers.Add("X-Title", "Deltempo");
        }

        var payload = new
        {
            model = request.ModelId,
            messages = new[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt }
            },
            temperature = request.Temperature,
            response_format = new { type = "json_object" }
        };

        message.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        return await HttpClient.SendAsync(message, ct);
    }

    private static async Task<HttpResponseMessage> SendOllamaAsync(AiChatRequest request, CancellationToken ct)
    {
        string host = string.IsNullOrWhiteSpace(request.Endpoint)
            ? "http://localhost:11434"
            : request.Endpoint.TrimEnd('/');

        // Normalised onto /api/chat + messages. The previous copies disagreed: two used
        // /api/generate with a single flat prompt, one used /api/chat. Only the OpenAI
        // compatible shape is exercised by the other providers, so one shape it is.
        string url = $"{host}/api/chat";

        var payload = new
        {
            model = request.ModelId,
            messages = new[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt }
            },
            stream = false,
            format = "json",
            options = new { temperature = request.Temperature }
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        return await HttpClient.PostAsync(url, content, ct);
    }

    private static string ResolveOpenAiEndpoint(string provider) => provider.ToLowerInvariant() switch
    {
        "groq" => "https://api.groq.com/openai/v1/chat/completions",
        "openrouter" => "https://openrouter.ai/api/v1/chat/completions",
        _ => "https://api.openai.com/v1/chat/completions",
    };

    private static string ExtractText(bool success, string body, string provider)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                // Gemini 2.5+ can interleave "thought" parts before the answer part; taking
                // parts[0] blindly returned reasoning prose instead of the JSON.
                if (candidates[0].TryGetProperty("content", out var geminiContent) &&
                    geminiContent.TryGetProperty("parts", out var parts))
                {
                    foreach (var part in parts.EnumerateArray())
                    {
                        if (part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True)
                        {
                            continue;
                        }

                        if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                        {
                            return text.GetString() ?? "{}";
                        }
                    }
                }

                throw new AiProviderException(AiFailureKind.MalformedResponse,
                    $"{provider} returned no usable text part.");
            }

            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                if (choices[0].TryGetProperty("message", out var msg) &&
                    msg.TryGetProperty("content", out var content) &&
                    content.ValueKind == JsonValueKind.String)
                {
                    return content.GetString() ?? "{}";
                }
            }

            if (root.TryGetProperty("message", out var ollamaMessage) &&
                ollamaMessage.TryGetProperty("content", out var ollamaContent) &&
                ollamaContent.ValueKind == JsonValueKind.String)
            {
                return ollamaContent.GetString() ?? "{}";
            }

            if (root.TryGetProperty("response", out var legacyOllama) &&
                legacyOllama.ValueKind == JsonValueKind.String)
            {
                return legacyOllama.GetString() ?? "{}";
            }

            if (root.TryGetProperty("error", out var error))
            {
                throw new AiProviderException(AiFailureKind.Unknown,
                    $"{provider} reported: {error.ToString()}");
            }

            throw new AiProviderException(AiFailureKind.MalformedResponse,
                $"{provider} returned an unrecognised response shape.");
        }
        catch (JsonException ex)
        {
            throw new AiProviderException(AiFailureKind.MalformedResponse,
                $"{provider} returned a response that was not valid JSON.", ex);
        }
    }

    private static AiProviderException ClassifyFailure(HttpStatusCode status, string body, string provider)
    {
        // 404 and 400-with-model-references are the "your model is retired" case, which is
        // the single most likely real-world failure now that defaults move out from under us.
        bool mentionsModel =
            body.Contains("model", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("not found", StringComparison.OrdinalIgnoreCase);

        AiFailureKind kind = status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => AiFailureKind.Authentication,
            HttpStatusCode.NotFound => AiFailureKind.ModelUnavailable,
            HttpStatusCode.TooManyRequests => AiFailureKind.RateLimited,
            HttpStatusCode.BadRequest when mentionsModel => AiFailureKind.ModelUnavailable,
            HttpStatusCode.RequestTimeout => AiFailureKind.Timeout,
            _ => AiFailureKind.Unknown,
        };

        // Ollama reports "model not found" with a 404 too - keep the local hint.
        if (provider.Equals("ollama", StringComparison.OrdinalIgnoreCase) &&
            kind == AiFailureKind.ModelUnavailable)
        {
            kind = AiFailureKind.ModelUnavailable;
        }

        string detail = Summarize(body);
        Trace.WriteLine($"[Deltempo AI] {provider} {(int)status} ({kind}): {detail}");

        return new AiProviderException(kind, $"{provider} returned {(int)status}: {detail}");
    }

    private static string Summarize(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "(empty response body)";
        string trimmed = body.Trim();
        return trimmed.Length <= 300 ? trimmed : trimmed[..300] + "...";
    }
}
