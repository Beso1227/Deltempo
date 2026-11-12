using System.Collections.Frozen;

namespace WinTempCleaner.Services;

/// <summary>
/// A model the app can offer for a provider.
/// </summary>
/// <param name="Id">The wire identifier sent in the request body.</param>
/// <param name="DisplayName">Human label for the settings dropdown.</param>
/// <param name="Note">
/// Optional caveat shown next to the entry - for example a tier restriction or a
/// scheduled retirement. Surfacing these matters more than hiding them: every hardcoded
/// default this catalog replaced was already dead when found.
/// </param>
/// <param name="Badge">
/// A short marker a picker can append to the label without a second line, such as
/// <c>FREE</c>. OpenRouter lists hundreds of models where the free ones and the paid ones
/// look identical; picking a paid one on an account with no credit is the most common way
/// this integration "fails", so the distinction belongs in the row itself.
/// </param>
public sealed record AiModelOption(
    string Id,
    string DisplayName,
    string? Note = null,
    string? Badge = null)
{
    /// <summary>The label a picker row shows: the human name, plus its badge when it has one.</summary>
    public string PickerLabel => string.IsNullOrWhiteSpace(Badge)
        ? DisplayName
        : $"{DisplayName} - {Badge}";

    /// <summary>
    /// True when the provider charges nothing for this model. A picker binds its green
    /// FREE pill to this rather than string-comparing the badge in XAML, so the marker and
    /// the visibility rule can never disagree.
    /// </summary>
    public bool IsFree => string.Equals(Badge, "FREE", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The wire id plus its caveat, for a row tooltip. The picker rows show the human name,
    /// so the id a request actually sends has to stay reachable somewhere.
    /// </summary>
    public string Detail => string.IsNullOrWhiteSpace(Note) ? Id : $"{Id} - {Note}";
}

/// <summary>
/// A provider Deltempo can talk to, with its transport shape and default model.
/// </summary>
public sealed record AiProviderDescriptor(
    string Name,
    bool RequiresApiKey,
    string DefaultModelId,
    TimeSpan RequestTimeout,
    string? EndpointOverrideDefault = null);

/// <summary>
/// A provider as the settings surfaces present it: the stored value, the label the
/// user reads, and one line about where the request goes.
/// </summary>
public sealed record AiProviderOption(string Id, string Label, string Description);

/// <summary>
/// Single source of truth for which AI providers exist, what their defaults are, and what
/// a plausible model list looks like.
/// </summary>
/// <remarks>
/// WHY THIS EXISTS: provider routing and model defaults were copy-pasted into three
/// separate services (OnlineFileIntelligenceService, AppIntelligenceService,
/// StartupIntelligenceService) and each copy carried its own hardcoded model literals -
/// 24 of them in total, in the routing switch AND again in TestConnectionAsync. When those
/// models were retired upstream, every provider silently failed on every single request,
/// and the failure was swallowed into an offline heuristic. The user could not tell a
/// working integration from a dead one. Duplicated defaults rot independently and silently.
///
/// The settings surface offers a picker rather than a free-text box: the provider's own
/// model list fills it once a key is present, the catalog is the offline fallback, and a
/// saved id that is missing from both is kept as its own row instead of being dropped.
/// A catalog can go stale between releases; a picker fed by the provider cannot, and it
/// refreshes quietly while the settings stay open.
///
/// VERIFY IDs BEFORE A RELEASE. Retirement dates move. `IsRetiring` and `Note` exist so a
/// scheduled deprecation is visible in the UI rather than discovered as a hard failure.
/// </remarks>
public static class AiModelCatalog
{
    /// <summary>
    /// A local model can legitimately take longer than a hosted API to produce its first
    /// token: loading weights into RAM is part of the request. The previous shared
    /// HttpClient timeout of 15 seconds made Ollama - the one provider a user picks
    /// SPECIFICALLY for privacy - unusable for any real model size.
    /// </summary>
    public static readonly TimeSpan LocalModelTimeout = TimeSpan.FromSeconds(120);

    public static readonly TimeSpan HostedModelTimeout = TimeSpan.FromSeconds(30);

    private static readonly FrozenDictionary<string, AiProviderDescriptor> Providers =
        new Dictionary<string, AiProviderDescriptor>(StringComparer.OrdinalIgnoreCase)
        {
            ["BuiltIn"] = new("BuiltIn", RequiresApiKey: false, DefaultModelId: string.Empty, HostedModelTimeout),
            ["OpenAI"] = new("OpenAI", RequiresApiKey: true, DefaultModelId: "gpt-5.4-mini", HostedModelTimeout),
            // Claude speaks its own Messages API, not the OpenAI chat shape. The default is
            // only a fallback for an offline first run; the settings picker replaces it with
            // whatever /v1/models reports for the key.
            ["Anthropic"] = new("Anthropic", RequiresApiKey: true, DefaultModelId: "claude-sonnet-4-5", HostedModelTimeout),
            ["Gemini"] = new("Gemini", RequiresApiKey: true, DefaultModelId: "gemini-3.5-flash", HostedModelTimeout),
            ["xAI"] = new("xAI", RequiresApiKey: true, DefaultModelId: "grok-4", HostedModelTimeout),
            // gpt-oss-120b is the verified production default: llama-3.3-70b-versatile
            // answers only on Groq's Enterprise tier, so an ordinary API key got a 403.
            ["Groq"] = new("Groq", RequiresApiKey: true, DefaultModelId: "openai/gpt-oss-120b", HostedModelTimeout),
            // The default is a FREE model on purpose. OpenRouter is the one provider a user
            // can add with a key and no credit, and its paid models answer a zero-balance
            // account with HTTP 402 - which the old build reported as "unexpected error".
            // A free default means "paste key, press Test" goes green on a brand-new account.
            ["OpenRouter"] = new("OpenRouter", RequiresApiKey: true, DefaultModelId: "google/gemma-4-31b-it:free", HostedModelTimeout),
            ["Mistral"] = new("Mistral", RequiresApiKey: true, DefaultModelId: "mistral-large-latest", HostedModelTimeout),
            ["Cerebras"] = new("Cerebras", RequiresApiKey: true, DefaultModelId: "llama-3.3-70b", HostedModelTimeout),
            ["DeepSeek"] = new("DeepSeek", RequiresApiKey: true, DefaultModelId: "deepseek-chat", HostedModelTimeout),
            ["GitHubModels"] = new("GitHubModels", RequiresApiKey: true, DefaultModelId: "openai/gpt-5.4-mini", HostedModelTimeout),
            ["HuggingFace"] = new("HuggingFace", RequiresApiKey: true, DefaultModelId: "meta-llama/Llama-3.3-70B-Instruct", HostedModelTimeout),
            ["Together"] = new("Together", RequiresApiKey: true, DefaultModelId: "meta-llama/Llama-3.3-70B-Instruct-Turbo", HostedModelTimeout),
            ["Ollama"] = new("Ollama", RequiresApiKey: false, DefaultModelId: "llama3.2", LocalModelTimeout, "http://localhost:11434"), // DevSkim: ignore DS162092
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<string> ProviderNames => Providers.Keys;

    public static bool TryGetProvider(string? providerName, out AiProviderDescriptor descriptor)
    {
        if (!string.IsNullOrWhiteSpace(providerName))
        {
            return Providers.TryGetValue(providerName, out descriptor!);
        }

        descriptor = Providers["BuiltIn"];
        return false;
    }

    /// <summary>Never returns an empty string for a cloud provider, so a caller can never
    /// accidentally fall back to a hardcoded literal of its own.</summary>
    public static string ResolveModelId(string? providerName, string? userModelName)
    {
        if (!string.IsNullOrWhiteSpace(userModelName))
        {
            return userModelName.Trim();
        }

        return TryGetProvider(providerName, out var descriptor) && !string.IsNullOrEmpty(descriptor.DefaultModelId)
            ? descriptor.DefaultModelId
            : string.Empty;
    }

    public static TimeSpan ResolveTimeout(string? providerName) =>
        TryGetProvider(providerName, out var descriptor) ? descriptor.RequestTimeout : HostedModelTimeout;

    /// <summary>
    /// Suggested models per provider. A suggestion list, never a gate - the settings model
    /// box accepts any identifier so a user on a new model is not blocked waiting for us.
    /// </summary>
    public static IReadOnlyList<AiModelOption> GetSuggestedModels(string? providerName) =>
        providerName?.ToLowerInvariant() switch
        {
            "gemini" => new[]
            {
                new AiModelOption("gemini-3.5-flash", "Gemini 3.5 Flash", "Default - fast, cheap, strong JSON"),
                new AiModelOption("gemini-3.8-flash", "Gemini 3.8 Flash", "Newest stable generation"),
                new AiModelOption("gemini-3.5-flash-lite", "Gemini 3.5 Flash Lite", "Cheapest hosted option"),
            },
            "openai" => new[]
            {
                // gpt-5-mini / gpt-5-nano / gpt-5 were found retired upstream; every
                // suggestion below was live at the last catalog check.
                new AiModelOption("gpt-5.4-mini", "GPT-5.4 mini", "Default - good quality per token"),
                new AiModelOption("gpt-5.4-nano", "GPT-5.4 nano", "Cheapest"),
                new AiModelOption("gpt-5.2", "GPT-5.2", "Highest quality"),
            },
            "groq" => new[]
            {
                new AiModelOption("openai/gpt-oss-120b", "GPT-OSS 120B", "Default - open weights, no tier gate"),
                new AiModelOption("llama-3.3-70b-versatile", "Llama 3.3 70B", "Enterprise tier only"),
            },
            "openrouter" => new[]
            {
                // Free first: these answer a zero-credit account. Verified against
                // https://openrouter.ai/api/v1/models - the paid entries below return 402
                // until the account is topped up.
                new AiModelOption("google/gemma-4-31b-it:free", "Gemma 4 31B", "No credit needed", "FREE"),
                new AiModelOption("xingchen-agi/xing4.0-29b-a4b:free", "Xing4.0 29B A4B", "No credit needed", "FREE"),
                new AiModelOption("meta-llama/llama-3.3-70b-instruct", "Llama 3.3 70B", "Paid - needs OpenRouter credit"),
                new AiModelOption("openai/gpt-oss-120b", "GPT-OSS 120B", "Paid - open weights"),
                new AiModelOption("qwen/qwen3-32b", "Qwen3 32B", "Paid - strong multilingual"),
            },
            "anthropic" => new[]
            {
                new AiModelOption("claude-sonnet-4-5", "Claude Sonnet 4.5", "Default - balanced quality and cost"),
                new AiModelOption("claude-opus-4-1", "Claude Opus 4.1", "Highest quality"),
                new AiModelOption("claude-haiku-4-5", "Claude Haiku 4.5", "Fastest"),
            },
            "xai" => new[]
            {
                new AiModelOption("grok-4", "Grok 4", "Default - flagship reasoning"),
                new AiModelOption("grok-4-fast", "Grok 4 Fast", "Cheaper, lower latency"),
                new AiModelOption("grok-3-mini", "Grok 3 mini", "Smallest"),
            },
            "mistral" => new[]
            {
                new AiModelOption("mistral-large-latest", "Mistral Large", "Default - flagship"),
                new AiModelOption("mistral-small-latest", "Mistral Small", "Cheaper"),
                new AiModelOption("open-mistral-nemo", "Mistral Nemo", "Open weights"),
            },
            "cerebras" => new[]
            {
                new AiModelOption("llama-3.3-70b", "Llama 3.3 70B", "Default - fast, strong JSON"),
                new AiModelOption("llama3.1-8b", "Llama 3.1 8B", "Smallest and quickest"),
                new AiModelOption("qwen-3-32b", "Qwen3 32B", "Strong multilingual"),
            },
            "deepseek" => new[]
            {
                new AiModelOption("deepseek-chat", "DeepSeek Chat", "Default - general purpose"),
                new AiModelOption("deepseek-reasoner", "DeepSeek Reasoner", "Reasoning-first"),
            },
            "githubmodels" => new[]
            {
                new AiModelOption("openai/gpt-5.4-mini", "GPT-5.4 mini", "Default - free within rate limits"),
                new AiModelOption("openai/gpt-5.2", "GPT-5.2", "Higher quality"),
                new AiModelOption("meta/Llama-3.3-70B-Instruct", "Llama 3.3 70B", "Open weights"),
            },
            "huggingface" => new[]
            {
                new AiModelOption("meta-llama/Llama-3.3-70B-Instruct", "Llama 3.3 70B", "Default - open weights"),
                new AiModelOption("Qwen/Qwen2.5-72B-Instruct", "Qwen2.5 72B", "Strong multilingual"),
                new AiModelOption("mistralai/Mistral-7B-Instruct-v0.3", "Mistral 7B", "Small and quick"),
            },
            "together" => new[]
            {
                new AiModelOption("meta-llama/Llama-3.3-70B-Instruct-Turbo", "Llama 3.3 70B Turbo", "Default - fast open weights"),
                new AiModelOption("Qwen/Qwen2.5-72B-Instruct-Turbo", "Qwen2.5 72B Turbo", "Strong multilingual"),
                new AiModelOption("mistralai/Mixtral-8x7B-Instruct-v0.1", "Mixtral 8x7B", "MoE open weights"),
            },
            "ollama" => new[]
            {
                new AiModelOption("llama3.2", "Llama 3.2", "Default - runs on most laptops"),
                new AiModelOption("qwen2.5:7b", "Qwen2.5 7B", "Good JSON, modest VRAM"),
                new AiModelOption("mistral-nemo", "Mistral Nemo", "Fast local option"),
            },
            _ => Array.Empty<AiModelOption>(),
        };

    /// <summary>
    /// A readable label for a model id. OpenAI and Groq publish only wire ids, so the picker
    /// offered "gpt-5.4-mini" and "openai/gpt-oss-120b" beside hand-written catalog names.
    /// A curated catalog name wins when one exists; otherwise the id is humanised by rule,
    /// never guessed, so an unfamiliar model still reads as a name.
    /// </summary>
    public static string GetFriendlyName(string? providerName, string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        string id = modelId.Trim();

        // Ollama tags its default weight set ":latest"; the catalog lists the bare name.
        string bare = id.EndsWith(":latest", StringComparison.OrdinalIgnoreCase)
            ? id[..^":latest".Length]
            : id;

        foreach (AiModelOption option in GetSuggestedModels(providerName))
        {
            if (string.Equals(option.Id, id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(option.Id, bare, StringComparison.OrdinalIgnoreCase))
            {
                return option.DisplayName;
            }
        }

        return HumanizeModelId(bare);
    }

    /// <summary>
    /// Turns a wire id into a label: vendor prefix and a ":latest" tag dropped, separators
    /// become spaces, known acronyms and parameter sizes are uppercased, and "GPT" hyphenates
    /// onto its version ("gpt-5.4-mini" -> "GPT-5.4 mini", "openai/gpt-oss-120b" -> "GPT-OSS 120B").
    /// </summary>
    private static string HumanizeModelId(string modelId)
    {
        int slash = modelId.LastIndexOf('/');
        string core = slash >= 0 && slash < modelId.Length - 1 ? modelId[(slash + 1)..] : modelId;

        string[] tokens = core.Split(['-', '_', ':', '/', ' '], StringSplitOptions.RemoveEmptyEntries);
        var parts = new List<string>(tokens.Length);
        foreach (string token in tokens)
        {
            // The OpenAI o-series is written lowercase and as one word ("o3-mini"), so it
            // must not be split into "o" + "3" like a version suffix.
            string lowered = token.ToLowerInvariant();
            if (lowered is "o1" or "o3" or "o4")
            {
                parts.Add(lowered);
                continue;
            }

            foreach (string piece in SplitLettersAndDigits(token))
            {
                parts.Add(FormatNameToken(piece));
            }
        }

        if (parts.Count == 0)
        {
            return modelId;
        }

        var label = new System.Text.StringBuilder();
        for (int i = 0; i < parts.Count; i++)
        {
            if (i > 0)
            {
                // A version split across tokens keeps its dot: "claude-haiku-4-5" reads
                // "Claude Haiku 4.5", not "Claude Haiku 4 5".
                bool versionDot = parts[i].All(char.IsDigit) &&
                    char.IsDigit(parts[i - 1][^1]) &&
                    !string.Equals(parts[i - 1], "GPT", StringComparison.Ordinal);

                // GPT is written with a hyphen onto its generation, not a space.
                bool hyphenate = string.Equals(parts[i - 1], "GPT", StringComparison.Ordinal) &&
                    (string.Equals(parts[i], "OSS", StringComparison.OrdinalIgnoreCase) ||
                     char.IsDigit(parts[i][0]));

                label.Append(versionDot ? '.' : hyphenate ? '-' : ' ');
            }

            label.Append(parts[i]);
        }

        return label.ToString();
    }

    /// <summary>"llama3.2" -> "llama", "3.2"; "120b" stays whole so it can uppercase as a size.</summary>
    /// <remarks>
    /// A single leading letter is a designation, not a word in front of a version, so "k2"
    /// and "r1" stay whole ("Kimi K2", "DeepSeek R1") while "gemma2" still splits.
    /// </remarks>
    private static IEnumerable<string> SplitLettersAndDigits(string token)
    {
        int start = 0;
        for (int i = 1; i < token.Length; i++)
        {
            if (char.IsLetter(token[i - 1]) && char.IsDigit(token[i]) && i - start >= 2)
            {
                yield return token[start..i];
                start = i;
            }
        }

        yield return token[start..];
    }

    private static string FormatNameToken(string token)
    {
        if (token.Length == 0)
        {
            return token;
        }

        if (char.IsDigit(token[0]))
        {
            // Parameter sizes ("70b", "1.5b", "8x7b") carry an uppercase unit; version
            // numbers ("5.4"), o-series ("4o") and dated ids ("002") stay as written.
            int last = token.Length - 1;
            char unit = char.ToLowerInvariant(token[last]);
            if (char.IsLetter(token[last]) &&
                unit is 'b' or 'm' &&
                token[..last].All(c => char.IsDigit(c) || c is '.' or 'x'))
            {
                return token[..last] + char.ToUpperInvariant(token[last]);
            }

            return token;
        }

        string lower = token.ToLowerInvariant();
        if (AcronymTokens.Contains(lower))
        {
            return lower.ToUpperInvariant();
        }

        if (LowercaseTokens.Contains(lower))
        {
            return lower;
        }

        if (BrandTokens.TryGetValue(lower, out string? brand))
        {
            return brand;
        }

        return char.ToUpperInvariant(token[0]) + token[1..].ToLowerInvariant();
    }

    private static readonly FrozenSet<string> AcronymTokens = new[]
    {
        "gpt", "oss", "ai", "it", "vl", "llm", "moe", "api", "glm",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Descriptors OpenAI writes in lowercase after the size ("GPT-4o mini").</summary>
    private static readonly FrozenSet<string> LowercaseTokens = new[]
    {
        "mini", "nano", "micro", "lite",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> BrandTokens =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["deepseek"] = "DeepSeek",
            ["chatgpt"] = "ChatGPT",
            ["openai"] = "OpenAI",
            ["xai"] = "xAI",
            ["anthropic"] = "Anthropic",
            ["claude"] = "Claude",
            ["sonnet"] = "Sonnet",
            ["opus"] = "Opus",
            ["haiku"] = "Haiku",
            ["grok"] = "Grok",
            ["mistral"] = "Mistral",
            ["mixtral"] = "Mixtral",
            ["cerebras"] = "Cerebras",
            ["together"] = "Together",
            ["hugging"] = "Hugging",
            ["face"] = "Face",
            ["github"] = "GitHub",
            ["llama"] = "Llama",
            ["qwen"] = "Qwen",
            ["gemma"] = "Gemma",
            ["phi"] = "Phi",
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Narrows a list to the rows the provider charges nothing for, when the provider says
    /// anything about pricing at all.
    /// </summary>
    /// <remarks>
    /// Only OpenRouter publishes per-model pricing, and that is the one provider where a
    /// paid row fails on a zero-credit account (HTTP 402). OpenAI, Groq, Gemini and Ollama
    /// return ids with no pricing, so treating "no FREE badge" as "paid" would empty their
    /// pickers and leave the user unable to choose anything. The rule is therefore "free
    /// only where pricing exists": with at least one FREE row the paid rows are dropped;
    /// with none the list is returned untouched.
    /// </remarks>
    public static IReadOnlyList<AiModelOption> FreeModelsOnly(IReadOnlyList<AiModelOption> models)
    {
        ArgumentNullException.ThrowIfNull(models);

        if (!models.Any(model => model.IsFree))
        {
            return models;
        }

        var free = new List<AiModelOption>(models.Count);
        foreach (AiModelOption model in models)
        {
            if (model.IsFree)
            {
                free.Add(model);
            }
        }

        return free;
    }

    /// <summary>
    /// True when two listings describe the same models in the same order, so a quiet
    /// background refresh can leave the open picker alone when the provider answered with
    /// what it already showed.
    /// </summary>
    /// <remarks>
    /// Compared by value rather than by instance: every refresh builds a new list, and
    /// rebuilding a bound <c>ItemsSource</c> (or a WinUI collection) closes whatever the
    /// user has open. Names and badges are part of the comparison, so a pricing change or
    /// a corrected label still reaches the picker.
    /// </remarks>
    public static bool IsSameModelSet(
        IReadOnlyList<AiModelOption>? left,
        IReadOnlyList<AiModelOption>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null || left.Count != right.Count)
        {
            return false;
        }

        for (int i = 0; i < left.Count; i++)
        {
            AiModelOption a = left[i];
            AiModelOption b = right[i];
            if (!string.Equals(a.Id, b.Id, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal) ||
                !string.Equals(a.Badge, b.Badge, StringComparison.Ordinal) ||
                !string.Equals(a.Note, b.Note, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Keeps a saved model visible in a picker even when the provider's current list does
    /// not contain it. Without this, removing the free-text box would silently drop a model
    /// the user saved earlier (a retired id, a private fine-tune, a model the key can call
    /// through another route) and the next Save would write an empty model.
    /// </summary>
    /// <remarks>
    /// The synthesized row carries the readable name of the id and says where it came from,
    /// so it reads as "your saved choice", not as something the provider advertised.
    /// </remarks>
    public static IReadOnlyList<AiModelOption> WithSavedModel(
        IReadOnlyList<AiModelOption> models,
        string? savedModelId,
        string? providerName = null)
    {
        ArgumentNullException.ThrowIfNull(models);

        string saved = savedModelId?.Trim() ?? string.Empty;
        if (saved.Length == 0)
        {
            return models;
        }

        foreach (AiModelOption model in models)
        {
            if (string.Equals(model.Id, saved, StringComparison.OrdinalIgnoreCase))
            {
                return models;
            }
        }

        var merged = new List<AiModelOption>(models.Count + 1)
        {
            new(saved, GetFriendlyName(providerName, saved),
                "Saved in settings - not in the list this key returned."),
        };
        merged.AddRange(models);
        return merged;
    }

    /// <summary>
    /// The provider list, in the order the settings show it. WPF's XAML and WinUI's
    /// view-model used to carry their own copies of these labels - including an
    /// "OpenAI (GPT-4o-mini)" label that had already outlived the model it named.
    /// One list means a provider or a default can never be current in one app and stale in
    /// the other.
    /// </summary>
    /// <remarks>
    /// Labels name the provider, never a model: a model name in a provider label is stale
    /// the day it retires, and the model list beside it already comes live from the API.
    /// </remarks>
    public static IReadOnlyList<AiProviderOption> GetProviderOptions() => new[]
    {
        new AiProviderOption(
            "BuiltIn",
            "Built-in Web Knowledge (Free, No Key)",
            "Offline heuristics and a curated catalog. Nothing leaves this PC."),
        new AiProviderOption(
            "OpenAI",
            "OpenAI (API Key)",
            "GPT models. Paid per use. The file summary is sent to OpenAI."),
        new AiProviderOption(
            "Anthropic",
            "Anthropic Claude (API Key)",
            "Claude models via the Messages API. The file summary is sent to Anthropic."),
        new AiProviderOption(
            "Gemini",
            "Google Gemini (Free Tier)",
            "Google AI Studio key with a free tier. The file summary is sent to Google."),
        new AiProviderOption(
            "xAI",
            "xAI Grok (API Key)",
            "Grok models. The file summary is sent to xAI."),
        new AiProviderOption(
            "OpenRouter",
            "OpenRouter (Free Models + Paid)",
            "One key, hundreds of hosted models, free ones badged. The file summary is sent to OpenRouter."),
        new AiProviderOption(
            "Groq",
            "Groq (Ultra-Fast Free Tier)",
            "Very fast inference with a free tier. The file summary is sent to Groq."),
        new AiProviderOption(
            "Mistral",
            "Mistral AI (API Key)",
            "Mistral and open-weight models. The file summary is sent to Mistral."),
        new AiProviderOption(
            "Cerebras",
            "Cerebras (Free Tier)",
            "Very fast inference with a free tier. The file summary is sent to Cerebras."),
        new AiProviderOption(
            "DeepSeek",
            "DeepSeek (Low-Cost)",
            "Low-cost DeepSeek chat models. The file summary is sent to DeepSeek."),
        new AiProviderOption(
            "GitHubModels",
            "GitHub Models (Free Tier)",
            "Models hosted by GitHub, free within rate limits. The file summary is sent to GitHub."),
        new AiProviderOption(
            "HuggingFace",
            "Hugging Face (Free Credits)",
            "Open-weight models through the HF router. The file summary is sent to Hugging Face."),
        new AiProviderOption(
            "Together",
            "Together AI (Free Models)",
            "Open-weight models, some free. The file summary is sent to Together AI."),
        new AiProviderOption(
            "Ollama",
            "Ollama (Local & Private)",
            "Runs a model on this PC. Nothing leaves the machine."),
    };

    /// <summary>
    /// The label for the model a request will actually use: the provider default when the
    /// box is empty, the typed identifier when it is not.
    /// </summary>
    // internal: no product code calls this - the settings UI composes its own label.
    internal static string DescribeModel(string? providerName, string? userModelName)
    {
        string model = ResolveModelId(providerName, userModelName);
        if (string.IsNullOrEmpty(model))
        {
            return "Built-in engine - no model call.";
        }

        return string.IsNullOrWhiteSpace(userModelName)
            ? $"{model} (provider default)"
            : $"{model} (custom)";
    }

    /// <summary>
    /// The label for the model a request will actually use: the readable name, with the
    /// wire id in brackets when the two differ, so "GPT-5.4 mini" never hides what is sent.
    /// </summary>
    public static string DescribeChosenModel(string? providerName, string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        string id = modelId.Trim();
        string name = GetFriendlyName(providerName, id);
        return string.Equals(name, id, StringComparison.OrdinalIgnoreCase) ? id : $"{name} ({id})";
    }

    /// <summary>
    /// The status line a settings surface shows after asking the provider for its models.
    /// </summary>
    /// <remarks>
    /// The count alone was not enough. OpenRouter answers a zero-credit account with HTTP
    /// 402 for every paid model, so a user whose saved model was paid saw "460 models
    /// available to this key" and then a failed Test with no connection between the two.
    ///
    /// The picker now hides paid rows whenever the provider publishes pricing, so the line
    /// counts what is actually offered (the FREE rows) and says the paid ones are hidden.
    /// A saved model that the filter therefore dropped is called out instead of vanishing
    /// silently: the hint above still names it, because that is what a request would use.
    ///
    /// Deliberately a hint, not a rewrite: the saved model is the user's choice, and
    /// silently swapping it would hide which model actually answered.
    /// </remarks>
    public static string DescribeFetchedModels(
        string? providerName,
        IReadOnlyList<AiModelOption> models,
        string? selectedModelId)
    {
        ArgumentNullException.ThrowIfNull(models);

        if (models.Count == 0)
        {
            return $"{providerName} returned no models for this key. " +
                   "Check the key's permissions, then press Refresh.";
        }

        int freeCount = 0;
        foreach (AiModelOption model in models)
        {
            if (model.IsFree)
            {
                freeCount++;
            }
        }

        // No pricing published: every row stays selectable, so the plain count is the truth.
        if (freeCount == 0)
        {
            return $"{models.Count} model{(models.Count == 1 ? string.Empty : "s")} available to this key.";
        }

        string status = freeCount == models.Count
            ? $"{freeCount} free model{(freeCount == 1 ? string.Empty : "s")} available to this key."
            : $"{freeCount} free model{(freeCount == 1 ? string.Empty : "s")} available to this key " +
              "(paid models are hidden).";

        string saved = selectedModelId?.Trim() ?? string.Empty;
        if (saved.Length == 0)
        {
            return status;
        }

        AiModelOption? selected = models.FirstOrDefault(
            model => string.Equals(model.Id, saved, StringComparison.OrdinalIgnoreCase));

        if (selected is null)
        {
            // The saved id may be retired or callable through another route. Saying it is
            // not in the list is factual; guessing what it costs would not be.
            return status + " The saved model is not in this list; pick a FREE row to replace it.";
        }

        return selected.IsFree
            ? status
            : status + " The saved model is paid and hidden; pick a FREE row to use no credit.";
    }

    /// <summary>
    /// The one-line description of <paramref name="providerName"/>, or an empty string for
    /// a provider the catalog does not know. Shared so WPF and WinUI cannot describe the
    /// same provider differently.
    /// </summary>
    public static string DescribeProvider(string? providerName)
    {
        string id = string.IsNullOrWhiteSpace(providerName) ? "BuiltIn" : providerName.Trim();
        foreach (AiProviderOption option in GetProviderOptions())
        {
            if (string.Equals(option.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return option.Description;
            }
        }

        return string.Empty;
    }
}
