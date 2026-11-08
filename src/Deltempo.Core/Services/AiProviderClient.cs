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
    /// <summary>The account has no credit, or its plan does not cover the request.</summary>
    PaymentRequired,
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
        AiFailureKind.PaymentRequired =>
            "The provider says this account has no credit, or its plan does not cover that model. " +
            "Pick a model marked FREE in the model list, top up the account, or choose another provider.",
        AiFailureKind.ProviderUnreachable =>
            "Could not reach the local model server. Start Ollama and confirm it is running.",
        AiFailureKind.Timeout =>
            "The request timed out. A local model that is loading its weights can exceed the timeout on the first call - try again now it is warm.",
        AiFailureKind.MalformedResponse =>
            "The provider replied, but not in the JSON shape Deltempo asked for. Try again, or choose a model that follows JSON instructions.",
        // The provider's own words are the only actionable part of an unclassified
        // failure; dropping them left the user with "unexpected error" and no cause.
        _ => $"The AI provider returned an unexpected error: {Message}",
    };
}

/// <summary>A provider-agnostic chat request.</summary>
/// <remarks>
/// <paramref name="OllamaEndpoint"/> is only meaningful for Ollama. Hosted providers own
/// their endpoint, and every caller passes the settings value here, so a transport that
/// honoured it for all providers sent OpenAI/Groq/OpenRouter traffic to localhost.
/// </remarks>
public sealed record AiChatRequest(
    string Provider,
    string? ApiKey,
    string ModelId,
    string SystemPrompt,
    string UserPrompt,
    string? OllamaEndpoint = null,
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
        // Effectively disabled: every request sets its own timeout via a linked
        // CancellationTokenSource, which is what lets a 120s local model and a 30s
        // hosted call share one client. (Spelled out rather than abbreviated because
        // DevSkim reads the three-letter abbreviation as a cipher mode name and files
        // a weak-cipher error against a comment about cancelling a request.)
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
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ModelId))
        {
            throw new AiProviderException(AiFailureKind.ModelUnavailable,
                $"No model configured for provider '{request.Provider}'.");
        }

        using var timeoutCts = new CancellationTokenSource(AiModelCatalog.ResolveTimeout(request.Provider));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        HttpResponseMessage? response = null;
        try
        {
            response = request.Provider.ToLowerInvariant() switch
            {
                "gemini" => await SendGeminiAsync(request, linked.Token),
                "ollama" => await SendOllamaAsync(request, linked.Token),
                _ => await SendOpenAiCompatibleAsync(request, linked.Token),
            };

            string body = await response.Content.ReadAsStringAsync(linked.Token);

            // A 400 that names response_format means the model does not implement
            // OpenAI's structured-output flag. The rest of the request is valid, so retry
            // once without it rather than failing every OpenRouter model that predates it.
            if (ShouldRetryWithoutJsonMode(request, response.StatusCode, body))
            {
                response.Dispose();
                response = await SendOpenAiCompatibleAsync(request, linked.Token, jsonMode: false);
                body = await response.Content.ReadAsStringAsync(linked.Token);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw ClassifyFailure(response.StatusCode, body, request.Provider);
            }

            return ExtractText(body, request.Provider);
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
        finally
        {
            response?.Dispose();
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

    private static async Task<HttpResponseMessage> SendOpenAiCompatibleAsync(
        AiChatRequest request,
        CancellationToken ct,
        bool jsonMode = true)
    {
        // The endpoint belongs to the provider, never to the caller: every caller passes
        // the Ollama host on every request, so trusting it here routed OpenAI, Groq and
        // OpenRouter to http://localhost:11434 and failed as an unreachable provider.
        string endpoint = ResolveOpenAiEndpoint(request);

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

        // A dictionary, not an anonymous type, because response_format is conditional:
        // JSON mode is a request we can drop and retry, never a hard requirement.
        var payload = new Dictionary<string, object?>
        {
            ["model"] = request.ModelId,
            ["messages"] = new[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserPrompt }
            },
            ["temperature"] = request.Temperature,
        };
        if (jsonMode)
        {
            payload["response_format"] = new { type = "json_object" };
        }

        message.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        return await HttpClient.SendAsync(message, ct);
    }

    private static async Task<HttpResponseMessage> SendOllamaAsync(AiChatRequest request, CancellationToken ct)
    {
        string host = string.IsNullOrWhiteSpace(request.OllamaEndpoint)
            // Ollama binds to loopback by design: it is the provider a user picks
            // specifically so nothing leaves the machine. Not debug code.
            ? "http://localhost:11434" // DevSkim: ignore DS162092
            : request.OllamaEndpoint.TrimEnd('/');

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

    /// <summary>
    /// The chat-completions URL for an OpenAI-compatible provider. Internal rather than
    /// private so a test can pin the routing table: the previous bug was a caller-supplied
    /// endpoint winning over this table, which no unit test observed.
    /// </summary>
    internal static string ResolveOpenAiEndpoint(string provider) => provider.ToLowerInvariant() switch
    {
        "groq" => "https://api.groq.com/openai/v1/chat/completions",
        "openrouter" => "https://openrouter.ai/api/v1/chat/completions",
        _ => "https://api.openai.com/v1/chat/completions",
    };

    /// <summary>
    /// The endpoint an OpenAI-compatible request is actually sent to. Deliberately ignores
    /// <see cref="AiChatRequest.OllamaEndpoint"/>; the overload exists so a test can prove
    /// that a caller carrying the local host cannot redirect a hosted provider.
    /// </summary>
    internal static string ResolveOpenAiEndpoint(AiChatRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ResolveOpenAiEndpoint(request.Provider);
    }

    /// <summary>
    /// The models endpoint for an OpenAI-compatible provider. Same routing table as the
    /// chat endpoint, minus the operation.
    /// </summary>
    internal static string ResolveModelsEndpoint(string provider) => provider.ToLowerInvariant() switch
    {
        "groq" => "https://api.groq.com/openai/v1/models",
        "openrouter" => "https://openrouter.ai/api/v1/models",
        _ => "https://api.openai.com/v1/models",
    };

    /// <summary>
    /// Lists the models the supplied key can actually use, straight from the provider.
    /// </summary>
    /// <remarks>
    /// The catalog's suggestion lists go stale between releases - every hardcoded default
    /// this app has replaced was already retired upstream when it was found. Asking the
    /// provider turns "type the model id and hope" into a list of what this account can
    /// call right now. A failure here is reported, never fatal: the manual box still works.
    /// </remarks>
    /// <exception cref="AiProviderException">The provider could not be asked.</exception>
    public static async Task<IReadOnlyList<AiModelOption>> ListModelsAsync(
        string provider,
        string? apiKey,
        string? ollamaEndpoint,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(provider) ||
            provider.Equals("BuiltIn", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<AiModelOption>();
        }

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        HttpResponseMessage? response = null;
        try
        {
            response = provider.ToLowerInvariant() switch
            {
                "gemini" => await GetGeminiModelsAsync(apiKey, linked.Token),
                "ollama" => await GetOllamaModelsAsync(ollamaEndpoint, linked.Token),
                _ => await GetOpenAiModelsAsync(provider, apiKey, linked.Token),
            };

            string body = await response.Content.ReadAsStringAsync(linked.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw ClassifyFailure(response.StatusCode, body, provider);
            }

            return ParseModelList(provider, body);
        }
        catch (AiProviderException)
        {
            throw;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new AiProviderException(AiFailureKind.Timeout,
                $"Listing models from {provider} timed out.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new AiProviderException(AiFailureKind.ProviderUnreachable,
                $"Could not reach {provider}: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new AiProviderException(AiFailureKind.Unknown,
                $"Unexpected error listing models from {provider}: {ex.Message}", ex);
        }
        finally
        {
            response?.Dispose();
        }
    }

    private static async Task<HttpResponseMessage> GetGeminiModelsAsync(string? apiKey, CancellationToken ct)
    {
        string url =
            "https://generativelanguage.googleapis.com/v1beta/models?pageSize=1000" +
            $"&key={Uri.EscapeDataString(apiKey ?? string.Empty)}";
        return await HttpClient.GetAsync(url, ct);
    }

    private static async Task<HttpResponseMessage> GetOllamaModelsAsync(string? ollamaEndpoint, CancellationToken ct)
    {
        string host = string.IsNullOrWhiteSpace(ollamaEndpoint)
            ? "http://localhost:11434" // DevSkim: ignore DS162092
            : ollamaEndpoint.TrimEnd('/');
        return await HttpClient.GetAsync($"{host}/api/tags", ct);
    }

    private static async Task<HttpResponseMessage> GetOpenAiModelsAsync(
        string provider,
        string? apiKey,
        CancellationToken ct)
    {
        string endpoint = ResolveModelsEndpoint(provider);
        using var message = new HttpRequestMessage(HttpMethod.Get, endpoint);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        if (endpoint.Contains("openrouter.ai", StringComparison.OrdinalIgnoreCase))
        {
            message.Headers.Add("HTTP-Referer", "https://github.com/Beso1227/Deltempo");
            message.Headers.Add("X-Title", "Deltempo");
        }

        return await HttpClient.SendAsync(message, ct);
    }

    /// <summary>
    /// Reads the three model-list shapes into one list. Internal so the parsing is pinned
    /// by tests without a network round trip.
    /// </summary>
    internal static IReadOnlyList<AiModelOption> ParseModelList(string provider, string body)
    {
        var models = new List<AiModelOption>();
        try
        {
            using var doc = JsonDocument.Parse(body);
            JsonElement root = doc.RootElement;
            string normalized = provider.ToLowerInvariant();

            if (normalized == "gemini")
            {
                if (root.TryGetProperty("models", out var geminiModels) &&
                    geminiModels.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in geminiModels.EnumerateArray())
                    {
                        // Embedding-only and legacy models cannot answer a chat request;
                        // offering them produced a 400 the moment the user picked one.
                        if (!item.TryGetProperty("supportedGenerationMethods", out var methods) ||
                            methods.ValueKind != JsonValueKind.Array ||
                            !methods.EnumerateArray().Any(m =>
                                m.ValueKind == JsonValueKind.String &&
                                string.Equals(m.GetString(), "generateContent", StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        if (!item.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String)
                        {
                            continue;
                        }

                        string id = (name.GetString() ?? string.Empty).Replace("models/", string.Empty, StringComparison.Ordinal);
                        if (string.IsNullOrWhiteSpace(id))
                        {
                            continue;
                        }

                        string display = item.TryGetProperty("displayName", out var displayName) &&
                            displayName.ValueKind == JsonValueKind.String
                            ? displayName.GetString() ?? id
                            : id;
                        models.Add(new AiModelOption(id, display));
                    }
                }
            }
            else if (normalized == "ollama")
            {
                if (root.TryGetProperty("models", out var ollamaModels) &&
                    ollamaModels.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in ollamaModels.EnumerateArray())
                    {
                        string? id = item.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
                            ? name.GetString()
                            : item.TryGetProperty("model", out var model) && model.ValueKind == JsonValueKind.String
                                ? model.GetString()
                                : null;
                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            models.Add(new AiModelOption(id!, id!));
                        }
                    }
                }
            }
            else if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in data.EnumerateArray())
                {
                    if (!item.TryGetProperty("id", out var idElement) || idElement.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    string id = idElement.GetString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        continue;
                    }

                    // OpenAI and Groq list every model they host, including the ones that
                    // cannot answer a chat request. Offering text-embedding-3-small or
                    // whisper-1 in a chat picker guaranteed a 400 the moment it was chosen.
                    if (!IsChatCapableId(id))
                    {
                        continue;
                    }

                    // OpenRouter's :batch variants are asynchronous jobs, not chat
                    // completions; a chat request against one is rejected.
                    if (normalized == "openrouter" && id.EndsWith(":batch", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // OpenRouter states the modality. A model that cannot emit text cannot
                    // answer this app's JSON request, so it does not belong in the picker.
                    if (item.TryGetProperty("architecture", out var architecture) &&
                        architecture.ValueKind == JsonValueKind.Object &&
                        architecture.TryGetProperty("output_modalities", out var outputs) &&
                        outputs.ValueKind == JsonValueKind.Array &&
                        !outputs.EnumerateArray().Any(m =>
                            m.ValueKind == JsonValueKind.String &&
                            string.Equals(m.GetString(), "text", StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    // OpenRouter carries a human label; OpenAI and Groq only an id.
                    string display = item.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
                        ? name.GetString() ?? id
                        : id;

                    models.Add(BuildOpenAiCompatibleOption(id, display, item));
                }
            }
        }
        catch (JsonException ex)
        {
            throw new AiProviderException(AiFailureKind.MalformedResponse,
                $"{provider} returned a model list that was not valid JSON.", ex);
        }

        // OpenRouter is ordered free-first: a zero-credit account can only call those, and
        // the picker is the one place a user can see which is which before pressing Test.
        return provider.Equals("openrouter", StringComparison.OrdinalIgnoreCase)
            ? models
                .DistinctBy(model => model.Id, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(model => string.Equals(model.Badge, "FREE", StringComparison.Ordinal))
                .ThenBy(model => model.Id, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : models
                .DistinctBy(model => model.Id, StringComparer.OrdinalIgnoreCase)
                .OrderBy(model => model.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    /// <summary>
    /// True when a model id can answer a chat request. OpenAI and Groq publish their whole
    /// catalogue on /models - embeddings, speech, image and moderation models included - and
    /// a picker built from it offered entries that fail on selection.
    /// </summary>
    internal static bool IsChatCapableId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        ReadOnlySpan<string> excluded =
        [
            "embed", "whisper", "tts", "dall-e", "moderation", "transcribe",
            "realtime", "rerank", "similarity", "audio", "image", "search", "guard",
        ];

        foreach (string marker in excluded)
        {
            if (id.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Builds the picker entry for an OpenAI-compatible model, reading OpenRouter's pricing
    /// when it is there. Free models are badged so the list is readable at a glance.
    /// </summary>
    private static AiModelOption BuildOpenAiCompatibleOption(string id, string display, JsonElement item)
    {
        if (!item.TryGetProperty("pricing", out var pricing) || pricing.ValueKind != JsonValueKind.Object)
        {
            return new AiModelOption(id, display);
        }

        double prompt = ReadPrice(pricing, "prompt");
        double completion = ReadPrice(pricing, "completion");

        if (prompt <= 0 && completion <= 0)
        {
            return new AiModelOption(id, display, "No credit needed", "FREE");
        }

        // Per-million-token figures: the API quotes per-token strings.
        string note = $"${prompt * 1_000_000:0.###}/M in, ${completion * 1_000_000:0.###}/M out";
        return new AiModelOption(id, display, note);
    }

    private static double ReadPrice(JsonElement pricing, string property)
    {
        if (!pricing.TryGetProperty(property, out var value))
        {
            return 0;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.String when double.TryParse(
                value.GetString(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out double parsed) => parsed,
            _ => 0,
        };
    }

    private static bool ShouldRetryWithoutJsonMode(AiChatRequest request, HttpStatusCode status, string body)
    {
        if (status != HttpStatusCode.BadRequest)
        {
            return false;
        }

        if (request.Provider.Equals("gemini", StringComparison.OrdinalIgnoreCase) ||
            request.Provider.Equals("ollama", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return body.Contains("response_format", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("json_object", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("structured output", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractText(string body, string provider)
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
            HttpStatusCode.PaymentRequired => AiFailureKind.PaymentRequired,
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
