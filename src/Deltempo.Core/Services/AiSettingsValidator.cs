using System;
using System.Collections.Generic;
using System.Linq;

namespace WinTempCleaner.Services;

/// <summary>How much an AI settings problem blocks the configuration.</summary>
public enum AiSettingsIssueSeverity
{
    /// <summary>The settings still work, but something will not behave as the user expects.</summary>
    Warning,

    /// <summary>The configuration cannot produce an AI answer until it is fixed.</summary>
    Error,
}

/// <summary>
/// One problem found in an AI settings snapshot. <paramref name="Field"/> is a stable
/// identifier so a surface can point at the row that needs attention.
/// </summary>
public sealed record AiSettingsIssue(string Field, string Message, AiSettingsIssueSeverity Severity)
{
    public bool IsError => Severity == AiSettingsIssueSeverity.Error;
}

/// <summary>
/// Validates the AI settings a user is about to save or test, without touching the
/// network or the settings store.
/// </summary>
/// <remarks>
/// WHY THIS EXISTS: both settings surfaces used to hand whatever was in the boxes to
/// <see cref="OnlineFileIntelligenceService.TestConnectionAsync"/> and let the request
/// fail. A missing key, a typo in the Ollama URL and an unknown provider all arrived as
/// the same "connection failed" line, after a network round trip that could take 45
/// seconds. The rules are pure so they can be tested and so WPF and WinUI cannot drift.
///
/// The rules are deliberately permissive where the catalog is permissive: a model name
/// is optional (the catalog supplies the provider default) and an empty Ollama endpoint
/// means "use the default", not "invalid".
/// </remarks>
public static class AiSettingsValidator
{
    public const string FieldProvider = "Provider";
    public const string FieldApiKey = "ApiKey";
    public const string FieldEndpoint = "Endpoint";
    public const string FieldModel = "Model";
    public const string FieldAutoQuery = "AutoQuery";

    /// <summary>
    /// Returns every issue in <paramref name="provider"/>'s configuration, errors first.
    /// An empty list means the settings are usable as they stand.
    /// </summary>
    public static IReadOnlyList<AiSettingsIssue> Validate(
        string? provider,
        string? apiKey,
        string? ollamaEndpoint,
        string? modelName = null,
        bool onlineAiEnabled = true,
        bool autoQueryLargeFiles = false)
    {
        var issues = new List<AiSettingsIssue>();
        string normalized = string.IsNullOrWhiteSpace(provider) ? "BuiltIn" : provider.Trim();

        if (!AiModelCatalog.TryGetProvider(normalized, out AiProviderDescriptor descriptor))
        {
            issues.Add(new AiSettingsIssue(
                FieldProvider,
                $"Unknown AI provider '{normalized}'. Pick one from the list.",
                AiSettingsIssueSeverity.Error));
            return issues;
        }

        // While the master switch is off nothing in this card runs, so a dormant provider
        // must not block saving unrelated settings: "disable AI, then press Save" used to
        // be refused because the provider that is no longer called still had no key.
        // Re-enabling AI runs these rules again.
        if (!onlineAiEnabled)
        {
            if (autoQueryLargeFiles)
            {
                issues.Add(new AiSettingsIssue(
                    FieldAutoQuery,
                    "Automatic large-file analysis only runs while Online file intelligence is on.",
                    AiSettingsIssueSeverity.Warning));
            }

            return issues;
        }

        if (descriptor.RequiresApiKey && string.IsNullOrWhiteSpace(apiKey))
        {
            issues.Add(new AiSettingsIssue(
                FieldApiKey,
                $"{descriptor.Name} needs an API key before it can answer. Paste the key or switch to the built-in engine.",
                AiSettingsIssueSeverity.Error));
        }

        // Only Ollama reads an endpoint; a stale value in the box must not block a cloud
        // provider that never uses it.
        if (normalized.Equals("Ollama", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(ollamaEndpoint))
        {
            string endpoint = ollamaEndpoint.Trim();
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                issues.Add(new AiSettingsIssue(
                    FieldEndpoint,
                    "Ollama endpoint must be an absolute http:// or https:// address, for example http://localhost:11434.",
                    AiSettingsIssueSeverity.Error));
            }
        }

        // A model identifier is a wire value; a space in it is always a typo, and the
        // provider answers with a 404 that reads like an outage.
        if (!string.IsNullOrWhiteSpace(modelName) && modelName.Trim().Contains(' '))
        {
            issues.Add(new AiSettingsIssue(
                FieldModel,
                "Model names cannot contain spaces. Pick a model from the list instead.",
                AiSettingsIssueSeverity.Warning));
        }

        return issues
            .OrderByDescending(issue => issue.Severity)
            .ToList();
    }

    /// <summary>True when nothing in the list blocks the configuration.</summary>
    public static bool IsValid(IEnumerable<AiSettingsIssue>? issues) =>
        issues is null || !issues.Any(issue => issue.IsError);

    /// <summary>
    /// The single line a settings surface should show: the first blocking error, else
    /// the first warning, else an empty string.
    /// </summary>
    public static string Describe(IEnumerable<AiSettingsIssue>? issues)
    {
        if (issues is null)
        {
            return string.Empty;
        }

        AiSettingsIssue? first = issues.FirstOrDefault();
        return first is null ? string.Empty : first.Message;
    }
}
