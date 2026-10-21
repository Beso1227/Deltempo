using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WinTempCleaner.Services;

public class AppCatalogEntry
{
    public string NamePattern { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string SafetyAdvice { get; set; } = string.Empty;
    public bool IsCoreRuntime { get; set; }
    public string SafetyVerdict => IsCoreRuntime ? "High Risk" : "Safe";
}

public static class AppDescriptionCatalog
{
    private static readonly List<AppCatalogEntry> Catalog = new()
    {
        // --- Web Browsers ---
        new() { NamePattern = "google chrome", Description = "Fast, secure, and widely-used web browser by Google.", Category = "Browsers" },
        new() { NamePattern = "mozilla firefox", Description = "Open-source privacy-oriented web browser by Mozilla.", Category = "Browsers" },
        new() { NamePattern = "brave", Description = "Privacy-focused web browser with built-in ad and tracker blocking.", Category = "Browsers" },
        new() { NamePattern = "microsoft edge", Description = "Chromium-based default web browser developed by Microsoft.", Category = "Browsers", SafetyAdvice = "Integrated with Windows OS features.", IsCoreRuntime = true },
        new() { NamePattern = "opera", Description = "Feature-rich web browser with built-in workspace and messaging tools.", Category = "Browsers" },
        new() { NamePattern = "vivaldi", Description = "Power-user customizable web browser with built-in tab stacking.", Category = "Browsers" },
        new() { NamePattern = "tor browser", Description = "Onion-routed anonymity and privacy browser.", Category = "Browsers" },

        // --- Gaming & Launchers ---
        new() { NamePattern = "steam", Description = "Valve's premier digital distribution platform, multiplayer hub, and game store.", Category = "Gaming" },
        new() { NamePattern = "epic games launcher", Description = "Digital game store and launcher by Epic Games, also hosting Unreal Engine.", Category = "Gaming" },
        new() { NamePattern = "riot client", Description = "Official game launcher for League of Legends, Valorant, and Riot Games.", Category = "Gaming" },
        new() { NamePattern = "valorant", Description = "Competitive tactical character-based first-person shooter by Riot Games.", Category = "Gaming" },
        new() { NamePattern = "battle.net", Description = "Blizzard Entertainment's gaming platform and launcher for Overwatch, WoW, and Diablo.", Category = "Gaming" },
        new() { NamePattern = "ea app", Description = "Electronic Arts gaming hub and digital distribution platform.", Category = "Gaming" },
        new() { NamePattern = "origin", Description = "Legacy game digital distribution platform by Electronic Arts.", Category = "Gaming" },
        new() { NamePattern = "ubisoft connect", Description = "Ecosystem and game launcher for Ubisoft titles.", Category = "Gaming" },
        new() { NamePattern = "gog galaxy", Description = "DRM-free digital gaming client and multi-platform library aggregator.", Category = "Gaming" },
        new() { NamePattern = "discord", Description = "Voice, video, and text communication platform for gaming and communities.", Category = "Gaming" },
        new() { NamePattern = "roblox", Description = "Online game creation platform and virtual sandbox universe.", Category = "Gaming" },
        new() { NamePattern = "minecraft", Description = "Sandbox block-building and adventure game by Mojang Studios.", Category = "Gaming" },
        new() { NamePattern = "vanguard", Description = "Kernel-level anti-cheat security system for Riot Games titles.", Category = "Gaming", SafetyAdvice = "Required to play Valorant and LoL." },

        // --- Developer Tools ---
        new() { NamePattern = "visual studio code", Description = "Lightweight, extensible code editor and IDE developed by Microsoft.", Category = "Development" },
        new() { NamePattern = "microsoft visual studio", Description = "Comprehensive enterprise integrated development environment (IDE) for .NET and C++.", Category = "Development" },
        new() { NamePattern = "git", Description = "Distributed version control system for tracking software code changes.", Category = "Development" },
        new() { NamePattern = "docker desktop", Description = "Containerization platform to build, share, and run containerized apps.", Category = "Development" },
        new() { NamePattern = "python", Description = "High-level, interpreted programming language and runtime environment.", Category = "Development" },
        new() { NamePattern = "nodejs", Description = "JavaScript runtime environment executing JS code outside a browser.", Category = "Development" },
        new() { NamePattern = "node.js", Description = "JavaScript runtime environment executing JS code outside a browser.", Category = "Development" },
        new() { NamePattern = "postman", Description = "API platform for building, testing, and debugging HTTP/REST endpoints.", Category = "Development" },
        new() { NamePattern = "jetbrains", Description = "Professional IDE and developer tooling suite by JetBrains.", Category = "Development" },
        new() { NamePattern = "pycharm", Description = "Dedicated Python IDE with intelligent code analysis by JetBrains.", Category = "Development" },
        new() { NamePattern = "intellij", Description = "Premier Java and Kotlin IDE developed by JetBrains.", Category = "Development" },
        new() { NamePattern = "android studio", Description = "Official IDE for Google Android app development.", Category = "Development" },
        new() { NamePattern = "notepad++", Description = "Fast, tabbed source code editor and text replacement utility.", Category = "Development" },
        new() { NamePattern = "sublime text", Description = "Sophisticated code and text editor designed for development and markup.", Category = "Development" },
        new() { NamePattern = "github desktop", Description = "Graphical user interface client for Git and GitHub workflows.", Category = "Development" },

        // --- Runtimes & Frameworks ---
        new() { NamePattern = "microsoft visual c++", Description = "Shared runtime libraries required by software and games compiled with MSVC.", Category = "Runtimes", SafetyAdvice = "Core system runtime: uninstalling will break dependent games and software.", IsCoreRuntime = true },
        new() { NamePattern = "visual c++ redistributable", Description = "C/C++ native runtime libraries required for Windows applications.", Category = "Runtimes", SafetyAdvice = "Core system runtime: uninstalling will break dependent software.", IsCoreRuntime = true },
        new() { NamePattern = "microsoft .net runtime", Description = "Managed runtime environment for executing .NET desktop applications.", Category = "Runtimes", SafetyAdvice = "Required by .NET applications.", IsCoreRuntime = true },
        new() { NamePattern = ".net desktop runtime", Description = "Windows desktop application runtime engine for modern .NET software.", Category = "Runtimes", SafetyAdvice = "Required by .NET applications.", IsCoreRuntime = true },
        new() { NamePattern = "directx", Description = "Multimedia and gaming graphics APIs developed by Microsoft.", Category = "Runtimes", SafetyAdvice = "Core 3D graphics runtime.", IsCoreRuntime = true },
        new() { NamePattern = "java", Description = "Java Runtime Environment (JRE) / SDK for executing Java applications.", Category = "Runtimes" },
        new() { NamePattern = "vulkan", Description = "Cross-platform 3D graphics and compute API runtime.", Category = "Runtimes", SafetyAdvice = "Required for Vulkan-accelerated games and GPU drivers.", IsCoreRuntime = true },

        // --- Productivity & Office ---
        new() { NamePattern = "microsoft 365", Description = "Cloud-powered productivity suite including Word, Excel, PowerPoint, and Outlook.", Category = "Productivity" },
        new() { NamePattern = "microsoft office", Description = "Productivity software suite featuring Word, Excel, and PowerPoint.", Category = "Productivity" },
        new() { NamePattern = "libreoffice", Description = "Free and open-source office suite alternative to Microsoft Office.", Category = "Productivity" },
        new() { NamePattern = "adobe acrobat", Description = "Industry-standard PDF reader, editor, and document signing suite.", Category = "Productivity" },
        new() { NamePattern = "notion", Description = "Connected workspace for notes, wiki documentation, and task management.", Category = "Productivity" },
        new() { NamePattern = "obsidian", Description = "Local Markdown-based personal knowledge base and second brain.", Category = "Productivity" },
        new() { NamePattern = "slack", Description = "Workplace team communication, messaging, and integrations platform.", Category = "Productivity" },
        new() { NamePattern = "microsoft teams", Description = "Enterprise collaboration and video conferencing platform by Microsoft.", Category = "Productivity" },
        new() { NamePattern = "zoom", Description = "Cloud video meeting, conferencing, and screen sharing application.", Category = "Productivity" },
        new() { NamePattern = "anydesk", Description = "High-speed remote desktop control and screen sharing application.", Category = "Productivity" },
        new() { NamePattern = "teamviewer", Description = "Remote access, screen sharing, and technical support tool.", Category = "Productivity" },

        // --- Media & Creativity ---
        new() { NamePattern = "adobe photoshop", Description = "Industry-leading raster graphics editor and digital imaging software.", Category = "Media" },
        new() { NamePattern = "adobe premiere", Description = "Professional timeline-based video editing and production software.", Category = "Media" },
        new() { NamePattern = "adobe creative cloud", Description = "Desktop hub managing Adobe creative applications, sync, and assets.", Category = "Media" },
        new() { NamePattern = "blender", Description = "Free and open-source 3D creation suite supporting modeling, rigging, and rendering.", Category = "Media" },
        new() { NamePattern = "obs studio", Description = "Free and open source software for video recording and live streaming.", Category = "Media" },
        new() { NamePattern = "vlc media player", Description = "Universal open-source multimedia player that plays virtually all video/audio codecs.", Category = "Media" },
        new() { NamePattern = "spotify", Description = "Digital music, podcast, and audio streaming service.", Category = "Media" },
        new() { NamePattern = "audacity", Description = "Open-source multi-track audio editor and recorder.", Category = "Media" },
        new() { NamePattern = "gimp", Description = "GNU Image Manipulation Program, open-source image retouching tool.", Category = "Media" },
        new() { NamePattern = "davinci resolve", Description = "Professional Hollywood-grade video editing, color grading, and VFX suite.", Category = "Media" },
        new() { NamePattern = "handbrake", Description = "Open-source video transcoder for converting videos between formats.", Category = "Media" },

        // --- System Utilities & Hardware ---
        new() { NamePattern = "7-zip", Description = "High-compression open-source file archiver supporting 7z, ZIP, and RAR.", Category = "Utilities" },
        new() { NamePattern = "winrar", Description = "Popular archive manager supporting RAR, ZIP, and extraction of diverse compressed formats.", Category = "Utilities" },
        new() { NamePattern = "msi afterburner", Description = "Graphics card overclocking, hardware monitoring, and fan speed tuning tool.", Category = "Utilities" },
        new() { NamePattern = "cpu-z", Description = "System profiling tool detailing CPU architecture, memory, and motherboard data.", Category = "Utilities" },
        new() { NamePattern = "hwmonitor", Description = "Hardware monitoring tool tracking voltages, temperatures, and fan speeds.", Category = "Utilities" },
        new() { NamePattern = "nvidia geforce experience", Description = "Companion utility for GeForce GPUs providing driver updates and game optimization.", Category = "System & Hardware", SafetyAdvice = "GPU management software." },
        new() { NamePattern = "nvidia app", Description = "Modern companion control center for NVIDIA graphics cards and driver updates.", Category = "System & Hardware" },
        new() { NamePattern = "nvidia graphics driver", Description = "Core display driver powering NVIDIA GeForce and RTX graphics cards.", Category = "System & Hardware", SafetyAdvice = "Core GPU driver: do not uninstall unless reinstalling display drivers.", IsCoreRuntime = true },
        new() { NamePattern = "amd software", Description = "Radeon graphics driver control panel and performance tuning suite.", Category = "System & Hardware", SafetyAdvice = "Core GPU software.", IsCoreRuntime = true },
        new() { NamePattern = "intel driver & support", Description = "Intel hardware scanning tool for driver and firmware updates.", Category = "System & Hardware" },
        new() { NamePattern = "realtek high definition audio", Description = "On-board audio driver and sound management package.", Category = "System & Hardware", SafetyAdvice = "Core sound hardware driver.", IsCoreRuntime = true },
        new() { NamePattern = "powertoys", Description = "Microsoft system utilities for power users (FancyZones, Text Extractor, Awake).", Category = "Utilities" },
        new() { NamePattern = "rufus", Description = "Utility that formats and creates bootable USB flash drives.", Category = "Utilities" },
        new() { NamePattern = "treesize", Description = "Hard disk space analyzer visualizing storage distribution across folders.", Category = "Utilities" },
        new() { NamePattern = "crystaldiskinfo", Description = "S.M.A.R.T. health and temperature monitoring utility for SSDs and HDDs.", Category = "Utilities" },
        new() { NamePattern = "ccleaner", Description = "System cleaning and registry modification tool.", Category = "Utilities" },
        new() { NamePattern = "bleachbit", Description = "Free and open-source disk space cleaner and privacy manager.", Category = "Utilities" },
        new() { NamePattern = "qbittorrent", Description = "Free, open-source, and ad-free BitTorrent peer-to-peer client.", Category = "Utilities" },
        new() { NamePattern = "utorrent", Description = "Peer-to-peer torrent downloading software.", Category = "Utilities" },
        new() { NamePattern = "nordvpn", Description = "Virtual Private Network client for encrypted internet privacy.", Category = "Utilities" },
        new() { NamePattern = "proton vpn", Description = "Swiss-based security and privacy-focused Virtual Private Network service.", Category = "Utilities" },

        // --- OEM & Brand Preinstalls ---
        new() { NamePattern = "hp support assistant", Description = "OEM diagnostic and support tool bundled with HP PCs.", Category = "Utilities" },
        new() { NamePattern = "lenovo vantage", Description = "Hardware management and system update utility for Lenovo laptops and PCs.", Category = "Utilities" },
        new() { NamePattern = "dell optimizer", Description = "AI-based performance and battery management tool for Dell systems.", Category = "Utilities" },
        new() { NamePattern = "acer care center", Description = "System diagnostic and driver maintenance application for Acer computers.", Category = "Utilities" },
        new() { NamePattern = "asus armoury crate", Description = "Hardware configuration and RGB lighting sync software for ASUS devices.", Category = "Utilities" },
        new() { NamePattern = "razer synapse", Description = "Hardware configuration tool for Razer peripherals, macros, and RGB lighting.", Category = "Utilities" },
        new() { NamePattern = "logitech g hub", Description = "Customization software for Logitech G gaming mice, keyboards, and headsets.", Category = "Utilities" },
        new() { NamePattern = "corsair icue", Description = "System monitoring and RGB illumination software for Corsair hardware.", Category = "Utilities" }
    };

    /// <summary>
    /// Finds a matching catalog entry by analyzing the app display name and publisher.
    /// </summary>
    public static AppCatalogEntry? FindCatalogEntry(string displayName, string publisher = "")
    {
        if (string.IsNullOrWhiteSpace(displayName)) return null;

        string normalizedName = displayName.Trim().ToLowerInvariant();

        foreach (var entry in Catalog)
        {
            if (normalizedName.Contains(entry.NamePattern))
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>
    /// Convenient lookup returning true if the app is known in the offline catalog.
    /// </summary>
    public static bool TryGetKnownApp(string displayName, out AppCatalogEntry? entry)
    {
        entry = FindCatalogEntry(displayName);
        return entry != null;
    }

    /// <summary>
    /// Provides heuristic classification and a helpful contextual description for unknown software.
    /// </summary>
    public static (string Description, string Category, string SafetyAdvice, bool IsCoreRuntime) ClassifyUnknownApp(
        string displayName,
        string publisher = "",
        string installLocation = "",
        string uninstallEngine = "")
    {
        string name = (displayName ?? string.Empty).ToLowerInvariant();
        string pub = (publisher ?? string.Empty).ToLowerInvariant();
        string loc = (installLocation ?? string.Empty).ToLowerInvariant();

        // 1. Drivers & Hardware
        if (name.Contains("driver") || name.Contains("audio driver") || name.Contains("graphics driver") ||
            name.Contains("chipset") || name.Contains("bluetooth") || name.Contains("wireless lan") ||
            name.Contains("ethernet") || pub.Contains("realtek") || pub.Contains("intel corporation") ||
            pub.Contains("nvidia") || pub.Contains("advanced micro devices"))
        {
            return (
                $"Hardware driver or system device controller published by {publisher}.",
                "System & Hardware",
                "Caution: Device driver. Uninstalling may disable hardware functionality.",
                true
            );
        }

        // 2. Runtimes, Frameworks & SDKs
        if (name.Contains("redistributable") || name.Contains("runtime") || name.Contains("sdk") ||
            name.Contains("framework") || name.Contains("prerequisite") || name.Contains("vulkan") ||
            name.Contains("directx") || name.Contains("xna"))
        {
            return (
                $"Software runtime libraries or developer kit published by {publisher}.",
                "Runtimes",
                "Caution: Core runtime. Other applications and games may rely on these libraries.",
                true
            );
        }

        // 3. Gaming & Anti-Cheat
        if (name.Contains("anti-cheat") || name.Contains("easyanticheat") || name.Contains("battleye") ||
            name.Contains("game") || name.Contains("launcher") || name.Contains("client") ||
            name.Contains("apex") || name.Contains("cyberpunk") ||
            loc.Contains("steamapps") || loc.Contains("epic games") || loc.Contains(@"\games\") || loc.Contains("/games/"))
        {
            return (
                $"Video game, game launcher, or gaming anti-cheat utility.",
                "Gaming",
                "Safe to uninstall if you no longer play this game.",
                false
            );
        }

        // 4. Web Browsers
        if (name.Contains("browser") || name.Contains("chromium") || name.Contains("brave") || name.Contains("firefox") || name.Contains("chrome"))
        {
            return (
                $"Internet web browser application.",
                "Browsers",
                "Safe to uninstall if you use an alternative browser.",
                false
            );
        }

        // 5. Development
        if (name.Contains("developer") || name.Contains("compiler") || name.Contains("ide") ||
            name.Contains("editor") || name.Contains("database") || name.Contains("server") ||
            name.Contains("studio") || name.Contains("git") || name.Contains("python") ||
            name.Contains("sublime") || name.Contains("sdk") || pub.Contains("python"))
        {
            return (
                $"Software development or engineering tool published by {publisher}.",
                "Development",
                "Safe to uninstall if no longer used for development.",
                false
            );
        }

        // 6. Media & Audio/Video
        if (name.Contains("player") || name.Contains("audio") || name.Contains("video") ||
            name.Contains("music") || name.Contains("photo") || name.Contains("recorder") ||
            name.Contains("codec") || name.Contains("vlc"))
        {
            return (
                $"Multimedia, audio, or video processing tool.",
                "Media & Audio",
                "Safe to uninstall if you have alternative media players.",
                false
            );
        }

        // 7. Security & Networking / VPN
        if (name.Contains("antivirus") || name.Contains("security") || name.Contains("vpn") ||
            name.Contains("firewall") || name.Contains("protection") || name.Contains("nordvpn") || pub.Contains("security"))
        {
            return (
                $"System security, privacy, or Virtual Private Network (VPN) software.",
                "Networking & Security",
                "Review carefully before removing if this is your active security tool.",
                false
            );
        }

        // 8. Productivity & Office
        if (name.Contains("office") || name.Contains("libreoffice") || name.Contains("document") ||
            name.Contains("pdf") || name.Contains("reader") || name.Contains("writer") || name.Contains("calc") ||
            name.Contains("spreadsheet") || pub.Contains("document foundation"))
        {
            return (
                $"Productivity and document suite published by {publisher}.",
                "Productivity",
                "Safe to uninstall if you have an alternative office suite.",
                false
            );
        }

        // 9. General fallback with publisher and location transparency
        string locationText = !string.IsNullOrWhiteSpace(installLocation)
            ? $" installed in {Path.GetFileName(installLocation.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}"
            : string.Empty;

        string publisherText = !string.IsNullOrWhiteSpace(publisher)
            ? $" published by {publisher}"
            : string.Empty;

        return (
            $"Windows desktop application{publisherText}{locationText}.",
            "General",
            "Safe to uninstall if no longer needed.",
            false
        );
    }
}
