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
/// A catalog can go stale between releases; a picker fed by the provider cannot, and the
/// search box still narrows hundreds of OpenRouter rows to the handful worth reading.
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
            ["Gemini"] = new("Gemini", RequiresApiKey: true, DefaultModelId: "gemini-3.5-flash", HostedModelTimeout),
            ["OpenAI"] = new("OpenAI", RequiresApiKey: true, DefaultModelId: "gpt-5.4-mini", HostedModelTimeout),
            // gpt-oss-120b is the verified production default: llama-3.3-70b-versatile
            // answers only on Groq's Enterprise tier, so an ordinary API key got a 403.
            ["Groq"] = new("Groq", RequiresApiKey: true, DefaultModelId: "openai/gpt-oss-120b", HostedModelTimeout),
            // The default is a FREE model on purpose. OpenRouter is the one provider a user
            // can add with a key and no credit, and its paid models answer a zero-balance
            // account with HTTP 402 - which the old build reported as "unexpected error".
            // A free default means "paste key, press Test" goes green on a brand-new account.
            ["OpenRouter"] = new("OpenRouter", RequiresApiKey: true, DefaultModelId: "google/gemma-4-31b-it:free", HostedModelTimeout),
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
            "ollama" => new[]
            {
                new AiModelOption("llama3.2", "Llama 3.2", "Default - runs on most laptops"),
                new AiModelOption("qwen2.5:7b", "Qwen2.5 7B", "Good JSON, modest VRAM"),
                new AiModelOption("mistral-nemo", "Mistral Nemo", "Fast local option"),
            },
            _ => Array.Empty<AiModelOption>(),
        };

    /// <summary>
    /// Narrows a model list to the rows that match what the user typed in the picker's
    /// search box. Every whitespace-separated token must appear somewhere in the row - the
    /// wire id, the human name, the pricing note or the FREE badge - so "free gemma" finds
    /// the free Gemma entry without the user knowing the exact id.
    /// </summary>
    /// <remarks>
    /// A blank query returns the same list instance: filtering is a view, not a rebuild, and
    /// the picker must keep its free-first order. Kept in Core so WPF and WinUI cannot
    /// disagree about what a search matches.
    /// </remarks>
    public static IReadOnlyList<AiModelOption> FilterModels(
        IReadOnlyList<AiModelOption> models,
        string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return models;
        }

        string[] tokens = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var matches = new List<AiModelOption>();
        foreach (AiModelOption model in models)
        {
            string haystack = $"{model.Id} {model.DisplayName} {model.Note} {model.Badge}";
            bool all = true;
            foreach (string token in tokens)
            {
                if (!haystack.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    all = false;
                    break;
                }
            }

            if (all)
            {
                matches.Add(model);
            }
        }

        return matches;
    }

    /// <summary>
    /// Keeps a saved model visible in a picker even when the provider's current list does
    /// not contain it. Without this, removing the free-text box would silently drop a model
    /// the user saved earlier (a retired id, a private fine-tune, a model the key can call
    /// through another route) and the next Save would write an empty model.
    /// </summary>
    /// <remarks>
    /// The synthesized row carries the id as its name and says where it came from, so it
    /// reads as "your saved choice", not as something the provider advertised.
    /// </remarks>
    public static IReadOnlyList<AiModelOption> WithSavedModel(
        IReadOnlyList<AiModelOption> models,
        string? savedModelId)
    {
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
            new(saved, saved, "Saved in settings - not in the list this key returned."),
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
    public static IReadOnlyList<AiProviderOption> GetProviderOptions() => new[]
    {
        new AiProviderOption(
            "BuiltIn",
            "Built-in Web Knowledge (Free, No Key)",
            "Offline heuristics and a curated catalog. Nothing leaves this PC."),
        new AiProviderOption(
            "OpenRouter",
            "OpenRouter (Universal / Multi-Model)",
            "One key, many hosted models. The file summary is sent to OpenRouter."),
        new AiProviderOption(
            "Gemini",
            "Google Gemini (Free API Key)",
            "Google AI Studio free tier. The file summary is sent to Google."),
        new AiProviderOption(
            "Groq",
            "Groq (Ultra-Fast Free Tier)",
            "Very fast free tier. The file summary is sent to Groq."),
        new AiProviderOption(
            "OpenAI",
            "OpenAI (GPT-5.4 mini)",
            "Paid, highest quality. The file summary is sent to OpenAI."),
        new AiProviderOption(
            "Ollama",
            "Ollama (Local & Private)",
            "Runs a model on this PC. Nothing leaves the machine."),
    };

    /// <summary>
    /// The label for the model a request will actually use: the provider default when the
    /// box is empty, the typed identifier when it is not.
    /// </summary>
    public static string DescribeModel(string? providerName, string? userModelName)
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
    /// The status line a settings surface shows after asking the provider for its models.
    /// </summary>
    /// <remarks>
    /// The count alone was not enough. OpenRouter answers a zero-credit account with HTTP
    /// 402 for every paid model, so a user whose saved model was paid saw "460 models
    /// available to this key" and then a failed Test with no connection between the two.
    /// When the provider returned free models AND the model that is about to be used is
    /// one of the paid ones, the line says so and points at the FREE rows.
    ///
    /// Deliberately a hint, not a rewrite: the saved model is the user's choice, and
    /// silently swapping it would hide which model actually answered.
    /// </remarks>
    public static string DescribeFetchedModels(
        string? providerName,
        IReadOnlyList<AiModelOption> models,
        string? selectedModelId)
    {
        if (models.Count == 0)
        {
            return $"{providerName} returned no models for this key. " +
                   "Check the key's permissions, then press Refresh.";
        }

        string status = $"{models.Count} model{(models.Count == 1 ? string.Empty : "s")} available to this key.";

        bool hasFree = models.Any(model => string.Equals(model.Badge, "FREE", StringComparison.Ordinal));
        if (!hasFree || string.IsNullOrWhiteSpace(selectedModelId))
        {
            return status;
        }

        AiModelOption? selected = models.FirstOrDefault(
            model => string.Equals(model.Id, selectedModelId.Trim(), StringComparison.OrdinalIgnoreCase));

        // Unknown ids are not called out: they may be a model this key can use through
        // another route, and guessing "paid" would be wrong more often than right.
        if (selected is null || string.Equals(selected.Badge, "FREE", StringComparison.Ordinal))
        {
            return status;
        }

        return status + " The selected model is paid; rows marked FREE need no credit.";
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
