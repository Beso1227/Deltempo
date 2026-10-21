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
public sealed record AiModelOption(string Id, string DisplayName, string? Note = null);

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
/// The settings model box stays free-text on purpose. A catalog can go stale between
/// releases in a way user-typed input cannot; this supplies a sane default and a
/// suggestion list, never a restriction.
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
            ["OpenAI"] = new("OpenAI", RequiresApiKey: true, DefaultModelId: "gpt-5-mini", HostedModelTimeout),
            ["Groq"] = new("Groq", RequiresApiKey: true, DefaultModelId: "llama-3.3-70b-versatile", HostedModelTimeout),
            ["OpenRouter"] = new("OpenRouter", RequiresApiKey: true, DefaultModelId: "meta-llama/llama-3.3-70b-instruct", HostedModelTimeout),
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
                new AiModelOption("gemini-3.5-flash-lite", "Gemini 3.5 Flash Lite", "Cheapest hosted option"),
                new AiModelOption("gemini-3.5-pro", "Gemini 3.5 Pro", "Slowest, most accurate"),
            },
            "openai" => new[]
            {
                new AiModelOption("gpt-5-mini", "GPT-5 mini", "Default - good quality per token"),
                new AiModelOption("gpt-5-nano", "GPT-5 nano", "Cheapest"),
                new AiModelOption("gpt-5", "GPT-5", "Highest quality"),
            },
            "groq" => new[]
            {
                new AiModelOption("llama-3.3-70b-versatile", "Llama 3.3 70B", "Enterprise tier only"),
                new AiModelOption("openai/gpt-oss-120b", "GPT-OSS 120B", "Open weights"),
            },
            "openrouter" => new[]
            {
                new AiModelOption("meta-llama/llama-3.3-70b-instruct", "Llama 3.3 70B", "Default"),
                new AiModelOption("qwen/qwen3-32b", "Qwen3 32B", "Strong multilingual"),
            },
            "ollama" => new[]
            {
                new AiModelOption("llama3.2", "Llama 3.2", "Default - runs on most laptops"),
                new AiModelOption("qwen2.5:7b", "Qwen2.5 7B", "Good JSON, modest VRAM"),
                new AiModelOption("mistral-nemo", "Mistral Nemo", "Fast local option"),
            },
            _ => Array.Empty<AiModelOption>(),
        };
}
