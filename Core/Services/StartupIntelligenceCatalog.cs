using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WinTempCleaner.Services;

public enum StartupDisableVerdict
{
    SafeToDisable,   // App won't launch on boot; user can launch anytime manually. 0 negative consequence.
    Caution,         // Cloud sync, peripheral profiles, or audio panels won't run until manually launched.
    DoNotDisable     // Core Windows security, graphics driver control, or essential background service.
}

public class StartupCatalogEntry
{
    public string NamePattern { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string WhatIsIt { get; set; } = string.Empty;
    public string Description
    {
        get => WhatIsIt;
        set => WhatIsIt = value;
    }
    public StartupDisableVerdict Verdict { get; set; } = StartupDisableVerdict.SafeToDisable;
    public string ImpactIfDisabled { get; set; } = string.Empty;
    public string DisableImpact
    {
        get => ImpactIfDisabled;
        set => ImpactIfDisabled = value;
    }
    public string Recommendation { get; set; } = string.Empty;

    public void Deconstruct(out string description, out StartupDisableVerdict verdict, out string impact, out string recommendation)
    {
        description = WhatIsIt;
        verdict = Verdict;
        impact = ImpactIfDisabled;
        recommendation = Recommendation;
    }
}

public static class StartupIntelligenceCatalog
{
    private static readonly List<StartupCatalogEntry> Catalog = new()
    {
        // --- Gaming Launchers & Clients (100% Safe to Disable) ---
        new() {
            NamePattern = "steam",
            DisplayName = "Steam Client Bootstrapper",
            WhatIsIt = "Valve's digital gaming store and multiplayer platform.",
            Verdict = StartupDisableVerdict.SafeToDisable,
            ImpactIfDisabled = "Nothing will go wrong. Steam will not open on boot, but you can launch it manually anytime.",
            Recommendation = "Recommended to disable to speed up Windows boot."
        },
        new() {
            NamePattern = "epicgameslauncher",
            DisplayName = "Epic Games Launcher",
            WhatIsIt = "Epic Games digital distribution store and game launcher.",
            Verdict = StartupDisableVerdict.SafeToDisable,
            ImpactIfDisabled = "Nothing will go wrong. Games and the launcher will open normally when started manually.",
            Recommendation = "Recommended to disable."
        },
        new() {
            NamePattern = "discord",
            DisplayName = "Discord",
            WhatIsIt = "Voice, video, and text communication client for communities and gaming.",
            Verdict = StartupDisableVerdict.SafeToDisable,
            ImpactIfDisabled = "Nothing will go wrong. You will not receive notifications until you manually open Discord.",
            Recommendation = "Recommended to disable for faster boot."
        },
        new() {
            NamePattern = "spotify",
            DisplayName = "Spotify",
            WhatIsIt = "Music, podcast, and audio streaming client.",
            Verdict = StartupDisableVerdict.SafeToDisable,
            ImpactIfDisabled = "Nothing will go wrong. Spotify simply won't pop up when you turn on your PC.",
            Recommendation = "Recommended to disable."
        },
        new() {
            NamePattern = "battle.net",
            DisplayName = "Battle.net Desktop App",
            WhatIsIt = "Blizzard Entertainment gaming launcher and social client.",
            Verdict = StartupDisableVerdict.SafeToDisable,
            ImpactIfDisabled = "Nothing will go wrong. Launch Blizzard games directly or open Battle.net manually.",
            Recommendation = "Recommended to disable."
        },
        new() {
            NamePattern = "ea app",
            DisplayName = "Electronic Arts App",
            WhatIsIt = "EA game launcher and library manager.",
            Verdict = StartupDisableVerdict.SafeToDisable,
            ImpactIfDisabled = "Nothing will go wrong. Games launch normally when opened manually.",
            Recommendation = "Recommended to disable."
        },
        new() {
            NamePattern = "origin",
            DisplayName = "EA Origin (Legacy)",
            WhatIsIt = "Legacy Electronic Arts digital game client.",
            Verdict = StartupDisableVerdict.SafeToDisable,
            ImpactIfDisabled = "Nothing will go wrong. Manual launch remains 100% functional.",
            Recommendation = "Recommended to disable."
        },
        new() {
            NamePattern = "riot client",
            DisplayName = "Riot Client",
            WhatIsIt = "Game launcher for League of Legends, Valorant, and Teamfight Tactics.",
            Verdict = StartupDisableVerdict.SafeToDisable,
            ImpactIfDisabled = "Nothing will go wrong. Launches automatically when you start any Riot game.",
            Recommendation = "Recommended to disable."
        },
        new() {
            NamePattern = "gog galaxy",
            DisplayName = "GOG GALAXY",
            WhatIsIt = "DRM-free gaming library aggregator and store.",
            Verdict = StartupDisableVerdict.SafeToDisable,
            ImpactIfDisabled = "Nothing will go wrong. Games remain fully playable.",
            Recommendation = "Recommended to disable."
        },
        new() {
            NamePattern = "ubisoft connect",
            DisplayName = "Ubisoft Connect",
            WhatIsIt = "Ubisoft game launcher and ecosystem client.",
            Verdict = StartupDisableVerdict.SafeToDisable,
            ImpactIfDisabled = "Nothing will go wrong. Launches automatically when starting Ubisoft games.",
            Recommendation = "Recommended to disable."
        },

        // --- Updaters & Helper Services (Safe to Disable) ---
        new() {
            NamePattern = "microsoftedgeautolaunch",
            DisplayName = "Microsoft Edge Auto Launch",
            WhatIsIt = "Pre-launches Edge in the background to speed up subsequent browser launch.",
            Verdict = StartupDisableVerdict.SafeToDisable,
            ImpactIfDisabled = "Nothing will go wrong. Saves RAM and CPU cycles. Edge will open normally when clicked.",
            Recommendation = "Safe to disable."
        },
        new() {
            NamePattern = "microsoft edge update",
            DisplayName = "Microsoft Edge Update",
            WhatIsIt = "Background updater check for Microsoft Edge.",
            Verdict = StartupDisableVerdict.SafeToDisable,
            ImpactIfDisabled = "Nothing will go wrong. Edge checks for updates automatically while running.",
            Recommendation = "Safe to disable."
        },
        new() {
            NamePattern = "googleupdate",
            DisplayName = "Google Update",
            WhatIsIt = "Automatic background updater for Google Chrome and Google products.",
            Verdict = StartupDisableVerdict.SafeToDisable,
            ImpactIfDisabled = "Nothing will go wrong. Chrome checks for updates when opened.",
            Recommendation = "Safe to disable."
        },
        new() {
            NamePattern = "adobe creative cloud",
            DisplayName = "Adobe Creative Cloud Desktop",
            WhatIsIt = "Adobe suite license manager and cloud synchronization hub.",
            Verdict = StartupDisableVerdict.SafeToDisable,
            ImpactIfDisabled = "Nothing will go wrong. Photoshop, Premiere, and Illustrator will launch and verify licenses normally.",
            Recommendation = "Recommended to disable (Creative Cloud uses significant boot RAM)."
        },
        new() {
            NamePattern = "adobeccxprocess",
            DisplayName = "Adobe CCXProcess",
            WhatIsIt = "Background helper process for Adobe Creative Cloud extensions and templates.",
            Verdict = StartupDisableVerdict.SafeToDisable,
            ImpactIfDisabled = "Nothing will go wrong. Launches on demand when Adobe apps are opened.",
            Recommendation = "Recommended to disable."
        },
        new() {
            NamePattern = "ccleaner",
            DisplayName = "CCleaner Smart Cleaning",
            WhatIsIt = "Background system monitoring and pop-up notifications for CCleaner.",
            Verdict = StartupDisableVerdict.SafeToDisable,
            ImpactIfDisabled = "Nothing will go wrong. Manual system cleaning remains fully functional.",
            Recommendation = "Recommended to disable."
        },
        new() {
            NamePattern = "skype",
            DisplayName = "Skype",
            WhatIsIt = "Telecommunications voice and video calling application.",
            Verdict = StartupDisableVerdict.SafeToDisable,
            ImpactIfDisabled = "Nothing will go wrong. You will not receive calls until Skype is opened.",
            Recommendation = "Recommended to disable."
        },

        // --- Cloud Storage & Sync (Caution: Background Sync Affected) ---
        new() {
            NamePattern = "onedrive",
            DisplayName = "Microsoft OneDrive",
            WhatIsIt = "Cloud file storage and folder backup synchronization service.",
            Verdict = StartupDisableVerdict.Caution,
            ImpactIfDisabled = "Caution: Files in OneDrive folders will not sync automatically in the background until OneDrive is opened.",
            Recommendation = "Keep enabled if you rely on real-time cloud backup; disable if you only use OneDrive occasionally."
        },
        new() {
            NamePattern = "googledrive",
            DisplayName = "Google Drive for Desktop",
            WhatIsIt = "Desktop synchronization client for Google Drive cloud storage.",
            Verdict = StartupDisableVerdict.Caution,
            ImpactIfDisabled = "Caution: Files will not sync to Google Drive in real-time until you launch the app.",
            Recommendation = "Keep enabled if you use active sync; disable if manual sync is sufficient."
        },
        new() {
            NamePattern = "dropbox",
            DisplayName = "Dropbox",
            WhatIsIt = "Cloud storage and file sharing sync client.",
            Verdict = StartupDisableVerdict.Caution,
            ImpactIfDisabled = "Caution: Background synchronization stops until Dropbox is opened.",
            Recommendation = "Keep enabled for real-time file sharing."
        },
        new() {
            NamePattern = "megasync",
            DisplayName = "MEGAsync",
            WhatIsIt = "Cloud backup and file synchronizer for MEGA.",
            Verdict = StartupDisableVerdict.Caution,
            ImpactIfDisabled = "Caution: File sync will only resume when manually started.",
            Recommendation = "Optional to disable if daily sync is not required."
        },

        // --- Hardware & Peripheral Control Panels (Caution: Profiles / Hotkeys) ---
        new() {
            NamePattern = "lghub",
            DisplayName = "Logitech G HUB",
            WhatIsIt = "Device management software for Logitech G mice, keyboards, and headsets.",
            Verdict = StartupDisableVerdict.Caution,
            ImpactIfDisabled = "Caution: On-board mouse DPI and custom RGB profiles may not load on boot until G HUB is opened.",
            Recommendation = "Keep enabled if you use custom mouse/keyboard macros or dynamic lighting."
        },
        new() {
            NamePattern = "razer synapse",
            DisplayName = "Razer Synapse",
            WhatIsIt = "Configuration suite for Razer hardware, keybinds, and Chroma RGB.",
            Verdict = StartupDisableVerdict.Caution,
            ImpactIfDisabled = "Caution: Keybindings, custom DPI, and Chroma lighting will revert to default until Synapse runs.",
            Recommendation = "Keep enabled for custom gaming profiles."
        },
        new() {
            NamePattern = "icue",
            DisplayName = "Corsair iCUE",
            WhatIsIt = "Corsair hardware monitoring, fan curve control, and RGB lighting manager.",
            Verdict = StartupDisableVerdict.Caution,
            ImpactIfDisabled = "Caution: Custom fan speed curves and lighting effects won't apply on boot.",
            Recommendation = "Keep enabled if controlling liquid cooler fans or custom lighting."
        },
        new() {
            NamePattern = "msiafterburner",
            DisplayName = "MSI Afterburner",
            WhatIsIt = "Graphics card monitoring and overclocking utility.",
            Verdict = StartupDisableVerdict.Caution,
            ImpactIfDisabled = "Caution: Custom GPU overclocks, undervolts, and custom fan curves will not be applied automatically.",
            Recommendation = "Keep enabled if you rely on custom GPU fan profiles or overclocks."
        },
        new() {
            NamePattern = "realtek",
            DisplayName = "Realtek HD Audio Manager",
            WhatIsIt = "Audio driver control panel and 3.5mm jack insertion detection.",
            Verdict = StartupDisableVerdict.Caution,
            ImpactIfDisabled = "Caution: Sound will still play, but audio jack pop-up prompts and equalizer settings may not initialize.",
            Recommendation = "Usually safe to keep, but can be disabled if basic sound works fine."
        },
        new() {
            NamePattern = "rtkaudioservice",
            DisplayName = "Realtek Audio Universal Service",
            WhatIsIt = "Companion service for Realtek high-definition audio drivers.",
            Verdict = StartupDisableVerdict.Caution,
            ImpactIfDisabled = "Caution: May affect audio equalizer presets and headphone jack detection.",
            Recommendation = "Keep enabled."
        },

        // --- Essential Windows & Security Components (DO NOT DISABLE) ---
        new() {
            NamePattern = "securityhealth",
            DisplayName = "Windows Security Health Notification",
            WhatIsIt = "Windows Defender Security Center system tray icon and alert monitor.",
            Verdict = StartupDisableVerdict.DoNotDisable,
            ImpactIfDisabled = "Warning: Windows Defender antivirus alerts, threat warnings, and security status icons will be hidden.",
            Recommendation = "DO NOT DISABLE. Critical Windows Security component."
        },
        new() {
            NamePattern = "windowsdefender",
            DisplayName = "Windows Defender",
            WhatIsIt = "Microsoft Windows built-in antivirus and real-time security protection.",
            Verdict = StartupDisableVerdict.DoNotDisable,
            ImpactIfDisabled = "Warning: Modifying or disabling security protection puts your PC at risk of malware.",
            Recommendation = "DO NOT DISABLE."
        },
        new() {
            NamePattern = "nvidiashare",
            DisplayName = "NVIDIA ShadowPlay / Share",
            WhatIsIt = "NVIDIA GeForce in-game overlay and video recording capture server.",
            Verdict = StartupDisableVerdict.Caution,
            ImpactIfDisabled = "Caution: In-game overlay (Alt+Z) and ShadowPlay instant replay hotkeys will not function.",
            Recommendation = "Safe to disable if you do not record gameplay with ShadowPlay."
        },
        new() {
            NamePattern = "nvcontainermonitor",
            DisplayName = "NVIDIA Container Monitor",
            WhatIsIt = "Core NVIDIA GPU display driver management and telemetry container.",
            Verdict = StartupDisableVerdict.DoNotDisable,
            ImpactIfDisabled = "Warning: Can disrupt GPU display settings, resolution switching, and multi-monitor configurations.",
            Recommendation = "DO NOT DISABLE."
        },
        new() {
            NamePattern = "wavesmaxxaudio",
            DisplayName = "Waves MaxxAudio Service Application",
            WhatIsIt = "Hardware audio enhancement and jack detection utility on Dell, HP, and Alienware systems.",
            Verdict = StartupDisableVerdict.DoNotDisable,
            ImpactIfDisabled = "Warning: Disabling can cause audio playback failure, mute headset jacks, or crash the audio subsystem.",
            Recommendation = "DO NOT DISABLE. Required for internal laptop speakers and headphone switching."
        },
        new() {
            NamePattern = "amd noise suppression",
            DisplayName = "AMD Noise Suppression",
            WhatIsIt = "Audio background noise cancellation powered by AMD graphics hardware.",
            Verdict = StartupDisableVerdict.Caution,
            ImpactIfDisabled = "Caution: Microphone noise cancellation will not initialize until opened.",
            Recommendation = "Safe to disable if not using AMD mic filtering."
        }
    };

    /// <summary>
    /// Searches curated catalog for a matching startup item.
    /// </summary>
    public static StartupCatalogEntry? FindCatalogEntry(string name, string exePath, string command)
    {
        string n = (name ?? string.Empty).ToLowerInvariant();
        string e = (exePath ?? string.Empty).ToLowerInvariant();
        string c = (command ?? string.Empty).ToLowerInvariant();

        foreach (var entry in Catalog)
        {
            string np = entry.NamePattern.ToLowerInvariant();
            string dp = entry.DisplayName.ToLowerInvariant();

            if (n.Contains(np) || np.Contains(n) ||
                dp.Contains(n) || n.Contains(dp) ||
                e.Contains(np) || c.Contains(np))
            {
                return entry;
            }
        }

        return null;
    }

    public static bool TryGetKnownStartup(string name, out StartupCatalogEntry? entry)
    {
        entry = FindCatalogEntry(name, "", "");
        return entry != null;
    }

    public static StartupCatalogEntry ClassifyUnknownStartup(string name, string publisher, string path)
    {
        return ClassifyUnknownStartup(name, path, "", publisher);
    }

    /// <summary>
    /// Intelligently classifies unknown startup items using executable naming, command flags, and path patterns.
    /// </summary>
    public static StartupCatalogEntry ClassifyUnknownStartup(string name, string exePath, string command, string publisher)
    {
        string n = (name ?? string.Empty).ToLowerInvariant();
        string e = (exePath ?? string.Empty).ToLowerInvariant();
        string c = (command ?? string.Empty).ToLowerInvariant();
        string pub = (publisher ?? string.Empty).ToLowerInvariant();

        // 1. Windows Security & Core Drivers (Do Not Disable)
        if (n.Contains("security") || n.Contains("defender") || n.Contains("antivirus") ||
            pub.Contains("microsoft windows") || e.Contains("system32") && (n.Contains("drv") || n.Contains("audio")))
        {
            return new StartupCatalogEntry
            {
                DisplayName = !string.IsNullOrWhiteSpace(name) ? name : "Security / System Component",
                WhatIsIt = $"Core system or security process published by {publisher}.",
                Verdict = StartupDisableVerdict.DoNotDisable,
                ImpactIfDisabled = "Warning: Disabling this entry may disable active system protection, hardware functionality, or Windows security warnings.",
                Recommendation = "DO NOT DISABLE."
            };
        }

        // 2. Updaters & Helpers (Safe to Disable)
        if (n.Contains("update") || n.Contains("updater") || n.Contains("helper") ||
            c.Contains("--autostart") || c.Contains("/background") || c.Contains("-autorun"))
        {
            return new StartupCatalogEntry
            {
                DisplayName = !string.IsNullOrWhiteSpace(name) ? name : "Background Helper / Updater",
                WhatIsIt = $"Automatic background updater or launch accelerator published by {publisher}.",
                Verdict = StartupDisableVerdict.SafeToDisable,
                ImpactIfDisabled = "Nothing will go wrong. The parent application will still check for updates normally when opened manually.",
                Recommendation = "Safe to disable to speed up Windows boot."
            };
        }

        // 3. Cloud Synchronization (Caution)
        if (n.Contains("sync") || n.Contains("cloud") || n.Contains("drive") || n.Contains("backup") ||
            pub.Contains("dropbox") || pub.Contains("google") || pub.Contains("mega"))
        {
            return new StartupCatalogEntry
            {
                DisplayName = !string.IsNullOrWhiteSpace(name) ? name : "Cloud / Sync Service",
                WhatIsIt = $"Data backup or cloud file synchronization service published by {publisher}.",
                Verdict = StartupDisableVerdict.Caution,
                ImpactIfDisabled = "Caution: Real-time background file synchronization will not run until you open the program manually.",
                Recommendation = "Keep enabled if you require continuous automated file backup."
            };
        }

        // 4. Hardware Management (Caution)
        if (n.Contains("rgb") || n.Contains("mouse") || n.Contains("keyboard") || n.Contains("headset") ||
            n.Contains("audio") || n.Contains("sound") || n.Contains("display") ||
            pub.Contains("logitech") || pub.Contains("razer") || pub.Contains("corsair") || pub.Contains("steelseries"))
        {
            return new StartupCatalogEntry
            {
                DisplayName = !string.IsNullOrWhiteSpace(name) ? name : "Hardware Utility",
                WhatIsIt = $"Hardware configuration or peripheral tuning utility published by {publisher}.",
                Verdict = StartupDisableVerdict.Caution,
                ImpactIfDisabled = "Caution: Custom hardware profiles, macro keybindings, or custom lighting may not apply on boot until launched.",
                Recommendation = "Keep enabled if using custom gaming or audio profiles."
            };
        }

        // 5. Gaming / Chat / Entertainment (Safe to Disable)
        if (n.Contains("game") || n.Contains("chat") || n.Contains("play") || n.Contains("media") ||
            e.Contains("steamapps") || e.Contains("games"))
        {
            return new StartupCatalogEntry
            {
                DisplayName = !string.IsNullOrWhiteSpace(name) ? name : "Application",
                WhatIsIt = $"Desktop gaming, communication, or media application published by {publisher}.",
                Verdict = StartupDisableVerdict.SafeToDisable,
                ImpactIfDisabled = "Nothing will go wrong. The application won't launch automatically at boot, but functions normally when started manually.",
                Recommendation = "Recommended to disable for faster boot times."
            };
        }

        // 6. Generic Windows Desktop Software
        string pubDisplay = !string.IsNullOrWhiteSpace(publisher) && !publisher.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
            ? $" published by {publisher}"
            : string.Empty;

        return new StartupCatalogEntry
        {
            DisplayName = !string.IsNullOrWhiteSpace(name) ? name : "Desktop Application",
            WhatIsIt = $"Windows desktop application{pubDisplay}.",
            Verdict = StartupDisableVerdict.SafeToDisable,
            ImpactIfDisabled = "Nothing will go wrong. Disabling stops this app from auto-starting at boot. You can still open and use it manually anytime.",
            Recommendation = "Safe to disable if you do not need it starting immediately when your PC boots."
        };
    }
}
