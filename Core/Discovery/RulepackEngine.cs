using System.IO;
using System.Text.Json;
using WinTempCleaner.Models;

namespace WinTempCleaner.Core.Discovery;

/// <summary>
/// Declarative model representing a cache/temp rule definition.
/// </summary>
public class RulepackDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string CategoryColor { get; set; } = "#3B82F6";
    public string SafetyBadge { get; set; } = "✓ SAFE • Cache";
    public string SafetyBadgeColor { get; set; } = "#10B981";
    public string Description { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = "\uE8B7";
    public bool RequiresAdmin { get; set; }
    public List<string> PathTemplates { get; set; } = new();
}

/// <summary>
/// Engine for loading, expanding, and converting declarative JSON rulepacks into Deltempo target scopes.
/// </summary>
public static class RulepackEngine
{
    /// <summary>
    /// Expands path templates with multi-drive and environment tokens (%STEAM_LIBRARIES%, %ALL_DRIVES%, %LOCALAPPDATA%, etc.)
    /// </summary>
    public static List<string> ExpandPathTemplates(IEnumerable<string> templates)
    {
        var resolvedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var steamLibraries = new Lazy<List<string>>(MultiDriveDetector.DiscoverSteamLibraries);
        var driveRoots = new Lazy<List<string>>(MultiDriveDetector.GetReadyDriveRoots);

        foreach (var template in templates)
        {
            if (string.IsNullOrWhiteSpace(template)) continue;

            if (template.Contains("%STEAM_LIBRARIES%", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var lib in steamLibraries.Value)
                {
                    string expanded = template.Replace("%STEAM_LIBRARIES%", lib, StringComparison.OrdinalIgnoreCase);
                    expanded = Environment.ExpandEnvironmentVariables(expanded);
                    AddExpandedPath(expanded, resolvedPaths);
                }
            }
            else if (template.Contains("%ALL_DRIVES%", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var drive in driveRoots.Value)
                {
                    string driveTrimmed = drive.TrimEnd('\\', '/');
                    string expanded = template.Replace("%ALL_DRIVES%", driveTrimmed, StringComparison.OrdinalIgnoreCase);
                    expanded = Environment.ExpandEnvironmentVariables(expanded);
                    AddExpandedPath(expanded, resolvedPaths);
                }
            }
            else
            {
                string expanded = Environment.ExpandEnvironmentVariables(template);
                AddExpandedPath(expanded, resolvedPaths);
            }
        }

        return resolvedPaths.ToList();
    }

    private static void AddExpandedPath(string path, HashSet<string> resolvedPaths)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        if (!path.Contains('*') && !path.Contains('?'))
        {
            if (Directory.Exists(path))
            {
                resolvedPaths.Add(path);
            }
            return;
        }

        ExpandWildcardDirectory(path, resolvedPaths);
    }

    private static void ExpandWildcardDirectory(string pathWithWildcards, HashSet<string> resolvedPaths)
    {
        try
        {
            string? root = Path.GetPathRoot(pathWithWildcards);
            if (string.IsNullOrEmpty(root)) return;

            string relativePart = pathWithWildcards[root.Length..];
            var segments = relativePart.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

            var currentCandidates = new List<string> { root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar };

            foreach (var segment in segments)
            {
                var nextCandidates = new List<string>();

                foreach (var parent in currentCandidates)
                {
                    if (!Directory.Exists(parent)) continue;

                    if (segment.Contains('*') || segment.Contains('?'))
                    {
                        try
                        {
                            var matchedDirs = Directory.GetDirectories(parent, segment);
                            nextCandidates.AddRange(matchedDirs);
                        }
                        catch { }
                    }
                    else
                    {
                        string combined = Path.Combine(parent, segment);
                        if (Directory.Exists(combined))
                        {
                            nextCandidates.Add(combined);
                        }
                    }
                }

                currentCandidates = nextCandidates;
                if (currentCandidates.Count == 0) break;
            }

            foreach (var candidate in currentCandidates)
            {
                if (Directory.Exists(candidate))
                {
                    resolvedPaths.Add(candidate);
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// Converts a set of rule definitions into active TargetFolderInfo models.
    /// Rules with zero existing directories on the current machine are filtered out.
    /// </summary>
    public static List<TargetFolderInfo> BuildTargetFolderInfos(IEnumerable<RulepackDefinition> rules, bool isAdmin)
    {
        var targets = new List<TargetFolderInfo>();

        foreach (var rule in rules)
        {
            var resolvedDirs = ExpandPathTemplates(rule.PathTemplates);
            if (resolvedDirs.Count == 0) continue;

            string displayPath = resolvedDirs.Count == 1
                ? resolvedDirs[0]
                : $"{resolvedDirs[0]} (+{resolvedDirs.Count - 1} more locations)";

            targets.Add(new TargetFolderInfo
            {
                Id = rule.Id,
                Name = rule.Name,
                Category = rule.Category,
                CategoryColor = rule.CategoryColor,
                SafetyBadge = rule.SafetyBadge,
                SafetyBadgeColor = rule.SafetyBadgeColor,
                Description = rule.Description,
                FolderPath = displayPath,
                IconGlyph = rule.IconGlyph,
                RequiresAdmin = rule.RequiresAdmin,
                HasAccess = !rule.RequiresAdmin || isAdmin,
                IsSelected = !rule.RequiresAdmin || isAdmin,
                ResolvedDirectoriesOverride = resolvedDirs
            });
        }

        return targets;
    }

    /// <summary>
    /// Returns default high-value built-in declarative rules for cross-drive gaming, developer tools, and app caches.
    /// </summary>
    public static List<RulepackDefinition> GetBuiltInRulepacks()
    {
        return new List<RulepackDefinition>
        {
            new RulepackDefinition
            {
                Id = "SteamGameCaches",
                Name = "Steam Shader Caches & Staging (All Drives)",
                Category = "Gaming & Launchers",
                CategoryColor = "#8B5CF6",
                SafetyBadge = "✓ SAFE • Game Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Pre-compiled shader pipelines, incomplete download chunks, and staging extracts across all Steam drives",
                IconGlyph = "\uE7FC",
                RequiresAdmin = false,
                PathTemplates = new List<string>
                {
                    @"%STEAM_LIBRARIES%\steamapps\shadercache",
                    @"%STEAM_LIBRARIES%\steamapps\downloading",
                    @"%STEAM_LIBRARIES%\steamapps\temp"
                }
            },
            new RulepackDefinition
            {
                Id = "EpicGamesLauncherCache",
                Name = "Epic Games Launcher Caches & Downloads",
                Category = "Gaming & Launchers",
                CategoryColor = "#8B5CF6",
                SafetyBadge = "✓ SAFE • Game Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Epic Games Launcher web cache, pending chunks, and manifest temporary extracts",
                IconGlyph = "\uE7FC",
                RequiresAdmin = false,
                PathTemplates = new List<string>
                {
                    @"%LOCALAPPDATA%\EpicGamesLauncher\Saved\webcache",
                    @"%LOCALAPPDATA%\EpicGamesLauncher\Saved\webcache_4147",
                    @"%LOCALAPPDATA%\EpicGamesLauncher\Saved\webcache_4430",
                    @"%LOCALAPPDATA%\EpicGamesLauncher\Saved\Logs"
                }
            },
            new RulepackDefinition
            {
                Id = "CreativeAppCaches",
                Name = "Creative & Media Editing Caches (Adobe & DaVinci)",
                Category = "Media & Editing",
                CategoryColor = "#EC4899",
                SafetyBadge = "✓ SAFE • Scratchpad",
                SafetyBadgeColor = "#10B981",
                Description = "Peak files, waveform caches, and temporary media render extracts",
                IconGlyph = "\uE8B9",
                RequiresAdmin = false,
                PathTemplates = new List<string>
                {
                    @"%APPDATA%\Adobe\Common\Media Cache Files",
                    @"%APPDATA%\Adobe\Common\Media Cache",
                    @"%APPDATA%\Adobe\Common\Peak Files",
                    @"%APPDATA%\Blackmagic Design\DaVinci Resolve\Support\logs"
                }
            },
            new RulepackDefinition
            {
                Id = "JetBrainsCaches",
                Name = "JetBrains IDE System & Index Caches",
                Category = "Dev Environments",
                CategoryColor = "#10B981",
                SafetyBadge = "✓ SAFE • Dev Cache",
                SafetyBadgeColor = "#10B981",
                Description = "Temporary build indexes, compiler logs, and plugin caches across IntelliJ, Rider, CLion, PyCharm",
                IconGlyph = "\uE7BE",
                RequiresAdmin = false,
                PathTemplates = new List<string>
                {
                    @"%LOCALAPPDATA%\JetBrains\*\log",
                    @"%LOCALAPPDATA%\JetBrains\*\tmp"
                }
            }
        };
    }

    /// <summary>
    /// Loads user-defined JSON rulepacks from a directory (e.g. %APPDATA%\Deltempo\rules).
    /// </summary>
    public static List<RulepackDefinition> LoadUserRulepacks(string rulesDirectory)
    {
        var rules = new List<RulepackDefinition>();
        if (!Directory.Exists(rulesDirectory)) return rules;

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            foreach (var file in Directory.EnumerateFiles(rulesDirectory, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var def = JsonSerializer.Deserialize<RulepackDefinition>(json, options);
                    if (def != null && !string.IsNullOrWhiteSpace(def.Id))
                    {
                        rules.Add(def);
                    }
                }
                catch
                {
                    // Ignore corrupted user rules without crashing
                }
            }
        }
        catch { }

        return rules;
    }
}
