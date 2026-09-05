using System.IO;

namespace WinTempCleaner.Services.Providers.CacheResolvers;

public static class MessagingAppCacheResolver
{
    public static List<string> Resolve()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var packagesRoot = Path.Combine(localAppData, "Packages");
        var dirs = new List<string>();

        // 1. WhatsApp Desktop (MSIX / Store App)
        if (Directory.Exists(packagesRoot))
        {
            try
            {
                foreach (var pkg in Directory.EnumerateDirectories(packagesRoot, "*WhatsAppDesktop*"))
                {
                    var ebRoot = Path.Combine(pkg, "LocalCache", "EBWebView");
                    if (Directory.Exists(ebRoot))
                    {
                        StoreAppCacheResolver.AddEbWebViewSafeCaches(ebRoot, dirs);
                    }
                    dirs.Add(Path.Combine(pkg, "AC", "INetCache"));
                    dirs.Add(Path.Combine(pkg, "AC", "Temp"));
                    dirs.Add(Path.Combine(pkg, "TempState"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        }

        // WhatsApp Win32 / Electron (legacy or standalone)
        dirs.Add(Path.Combine(roamingAppData, "WhatsApp", "Cache"));
        dirs.Add(Path.Combine(roamingAppData, "WhatsApp", "Code Cache"));
        dirs.Add(Path.Combine(roamingAppData, "WhatsApp", "GPUCache"));
        dirs.Add(Path.Combine(roamingAppData, "WhatsApp", "DawnCache"));
        dirs.Add(Path.Combine(roamingAppData, "WhatsApp", "Service Worker", "CacheStorage"));
        dirs.Add(Path.Combine(roamingAppData, "WhatsApp", "Crashpad", "reports"));

        // 2. Telegram Desktop (MSIX / Store App & Win32)
        if (Directory.Exists(packagesRoot))
        {
            try
            {
                foreach (var pkg in Directory.EnumerateDirectories(packagesRoot, "*TelegramDesktop*"))
                {
                    var uwpTData = Path.Combine(pkg, "LocalCache", "Roaming", "Telegram Desktop UWP", "tdata");
                    dirs.Add(Path.Combine(uwpTData, "user_data", "cache"));
                    dirs.Add(Path.Combine(uwpTData, "temp"));
                    dirs.Add(Path.Combine(uwpTData, "dumps"));
                    dirs.Add(Path.Combine(pkg, "AC", "Temp"));
                    dirs.Add(Path.Combine(pkg, "TempState"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        }

        // Win32 / Standalone Telegram
        var win32TData = Path.Combine(roamingAppData, "Telegram Desktop", "tdata");
        dirs.Add(Path.Combine(win32TData, "user_data", "cache"));
        dirs.Add(Path.Combine(win32TData, "temp"));
        dirs.Add(Path.Combine(win32TData, "dumps"));

        // 3. Discord / Discord Canary / Discord PTB
        dirs.Add(Path.Combine(roamingAppData, "discord", "Cache"));
        dirs.Add(Path.Combine(roamingAppData, "discord", "Code Cache"));
        dirs.Add(Path.Combine(roamingAppData, "discord", "GPUCache"));
        dirs.Add(Path.Combine(roamingAppData, "discord", "DawnCache"));
        dirs.Add(Path.Combine(roamingAppData, "discord", "Crashpad", "reports"));
        dirs.Add(Path.Combine(roamingAppData, "discordcanary", "Cache"));
        dirs.Add(Path.Combine(roamingAppData, "discordcanary", "Code Cache"));
        dirs.Add(Path.Combine(roamingAppData, "discordcanary", "GPUCache"));
        dirs.Add(Path.Combine(roamingAppData, "discordptb", "Cache"));
        dirs.Add(Path.Combine(roamingAppData, "discordptb", "Code Cache"));
        dirs.Add(Path.Combine(roamingAppData, "discordptb", "GPUCache"));

        // 4. Slack (Desktop & Store)
        dirs.Add(Path.Combine(roamingAppData, "Slack", "Cache"));
        dirs.Add(Path.Combine(roamingAppData, "Slack", "Code Cache"));
        dirs.Add(Path.Combine(roamingAppData, "Slack", "GPUCache"));
        dirs.Add(Path.Combine(roamingAppData, "Slack", "DawnCache"));
        dirs.Add(Path.Combine(roamingAppData, "Slack", "Service Worker", "CacheStorage"));
        dirs.Add(Path.Combine(roamingAppData, "Slack", "logs"));
        if (Directory.Exists(packagesRoot))
        {
            try
            {
                foreach (var pkg in Directory.EnumerateDirectories(packagesRoot, "*Slack*"))
                {
                    var ebRoot = Path.Combine(pkg, "LocalCache", "EBWebView");
                    if (Directory.Exists(ebRoot)) StoreAppCacheResolver.AddEbWebViewSafeCaches(ebRoot, dirs);
                    dirs.Add(Path.Combine(pkg, "AC", "INetCache"));
                    dirs.Add(Path.Combine(pkg, "AC", "Temp"));
                    dirs.Add(Path.Combine(pkg, "TempState"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        }

        // 5. Microsoft Teams (Classic & New Teams)
        dirs.Add(Path.Combine(roamingAppData, "Microsoft", "Teams", "Cache"));
        dirs.Add(Path.Combine(roamingAppData, "Microsoft", "Teams", "blob_storage"));
        dirs.Add(Path.Combine(roamingAppData, "Microsoft", "Teams", "GPUCache"));
        dirs.Add(Path.Combine(roamingAppData, "Microsoft", "Teams", "tmp"));
        dirs.Add(Path.Combine(localAppData, "Microsoft", "Teams", "Cache"));
        if (Directory.Exists(packagesRoot))
        {
            try
            {
                foreach (var pkg in Directory.EnumerateDirectories(packagesRoot, "*Teams*"))
                {
                    var ebRoot = Path.Combine(pkg, "LocalCache", "Microsoft", "MSTeams", "EBWebView");
                    if (Directory.Exists(ebRoot)) StoreAppCacheResolver.AddEbWebViewSafeCaches(ebRoot, dirs);
                    var ebRoot2 = Path.Combine(pkg, "LocalCache", "EBWebView");
                    if (Directory.Exists(ebRoot2)) StoreAppCacheResolver.AddEbWebViewSafeCaches(ebRoot2, dirs);
                    dirs.Add(Path.Combine(pkg, "LocalCache", "Microsoft", "MSTeams", "Logs"));
                    dirs.Add(Path.Combine(pkg, "LocalCache", "Microsoft", "MSTeams", "tmp"));
                    dirs.Add(Path.Combine(pkg, "AC", "INetCache"));
                    dirs.Add(Path.Combine(pkg, "AC", "Temp"));
                    dirs.Add(Path.Combine(pkg, "TempState"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        }

        // 6. Signal Desktop
        dirs.Add(Path.Combine(roamingAppData, "Signal", "Cache"));
        dirs.Add(Path.Combine(roamingAppData, "Signal", "Code Cache"));
        dirs.Add(Path.Combine(roamingAppData, "Signal", "GPUCache"));
        dirs.Add(Path.Combine(roamingAppData, "Signal", "DawnCache"));
        dirs.Add(Path.Combine(roamingAppData, "Signal", "Crashpad", "reports"));

        // 7. Skype (Win32 & Store)
        dirs.Add(Path.Combine(roamingAppData, "Microsoft", "Skype for Desktop", "Cache"));
        dirs.Add(Path.Combine(roamingAppData, "Microsoft", "Skype for Desktop", "Code Cache"));
        dirs.Add(Path.Combine(roamingAppData, "Microsoft", "Skype for Desktop", "GPUCache"));
        dirs.Add(Path.Combine(roamingAppData, "Microsoft", "Skype for Desktop", "DawnCache"));
        if (Directory.Exists(packagesRoot))
        {
            try
            {
                foreach (var pkg in Directory.EnumerateDirectories(packagesRoot, "*SkypeApp*"))
                {
                    var ebRoot = Path.Combine(pkg, "LocalCache", "EBWebView");
                    if (Directory.Exists(ebRoot)) StoreAppCacheResolver.AddEbWebViewSafeCaches(ebRoot, dirs);
                    dirs.Add(Path.Combine(pkg, "AC", "INetCache"));
                    dirs.Add(Path.Combine(pkg, "AC", "Temp"));
                    dirs.Add(Path.Combine(pkg, "TempState"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[Deltempo] Suppressed exception: {ex.Message}");
            }
        }

        // 8. Viber
        dirs.Add(Path.Combine(localAppData, "ViberPC", "Cache"));
        dirs.Add(Path.Combine(localAppData, "ViberPC", "avatars"));
        dirs.Add(Path.Combine(localAppData, "ViberPC", "temp"));

        // 9. Element / Matrix
        dirs.Add(Path.Combine(roamingAppData, "Element", "Cache"));
        dirs.Add(Path.Combine(roamingAppData, "Element", "Code Cache"));
        dirs.Add(Path.Combine(roamingAppData, "Element", "GPUCache"));

        // 10. Zoom
        dirs.Add(Path.Combine(roamingAppData, "Zoom", "data"));
        dirs.Add(Path.Combine(localAppData, "Zoom", "temp"));
        dirs.Add(Path.Combine(roamingAppData, "Zoom", "logs"));

        // 11. WeChat & LINE
        dirs.Add(Path.Combine(roamingAppData, "Tencent", "WeChat", "All Users", "Cache"));
        dirs.Add(Path.Combine(localAppData, "LINE", "Cache"));

        return dirs;
    }
}
