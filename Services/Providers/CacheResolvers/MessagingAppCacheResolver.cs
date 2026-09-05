using System.IO;

namespace WinTempCleaner.Services.Providers.CacheResolvers;

public static class MessagingAppCacheResolver
{
    public static List<string> Resolve()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dirs = new List<string>();

        // ZERO-TOUCH POLICY FOR MESSAGING APP PACKAGES:
        // Modern Windows Store / MSIX apps (WhatsApp, Telegram, Teams, Slack, Skype) store their active
        // authentication tokens, cryptographic keys, and SQLite session states inside their package sandboxes.
        // Deltempo NEVER sweeps inside Packages\<ChatApp>\ to guarantee 100% login session preservation.

        // 1. WhatsApp Win32 / Standalone (GPU & Crashpad only, strictly zero-touch on session/service worker)
        dirs.Add(Path.Combine(roamingAppData, "WhatsApp", "GPUCache"));
        dirs.Add(Path.Combine(roamingAppData, "WhatsApp", "DawnCache"));
        dirs.Add(Path.Combine(roamingAppData, "WhatsApp", "Crashpad", "reports"));

        // 2. Telegram Desktop Win32 / Standalone (Isolated media cache & dumps only, strictly preserving auth keys in tdata)
        var win32TData = Path.Combine(roamingAppData, "Telegram Desktop", "tdata");
        dirs.Add(Path.Combine(win32TData, "user_data", "cache"));
        dirs.Add(Path.Combine(win32TData, "dumps"));

        // 3. Discord / Discord Canary / Discord PTB (GPU, shader & crash reports only, strictly preserving tokens & LevelDB)
        dirs.Add(Path.Combine(roamingAppData, "discord", "GPUCache"));
        dirs.Add(Path.Combine(roamingAppData, "discord", "DawnCache"));
        dirs.Add(Path.Combine(roamingAppData, "discord", "Crashpad", "reports"));
        dirs.Add(Path.Combine(roamingAppData, "discordcanary", "GPUCache"));
        dirs.Add(Path.Combine(roamingAppData, "discordcanary", "DawnCache"));
        dirs.Add(Path.Combine(roamingAppData, "discordptb", "GPUCache"));
        dirs.Add(Path.Combine(roamingAppData, "discordptb", "DawnCache"));

        // 4. Slack Standalone (GPU shaders & crash logs only)
        dirs.Add(Path.Combine(roamingAppData, "Slack", "GPUCache"));
        dirs.Add(Path.Combine(roamingAppData, "Slack", "DawnCache"));
        dirs.Add(Path.Combine(roamingAppData, "Slack", "logs"));

        // 5. Microsoft Teams Standalone (GPU cache only, strictly preserving SlimCore/SkypeRT configs & sessions)
        dirs.Add(Path.Combine(roamingAppData, "Microsoft", "Teams", "GPUCache"));
        dirs.Add(Path.Combine(roamingAppData, "Microsoft", "Teams", "tmp"));

        // 6. Signal Desktop Standalone (GPU shaders & crash reports only)
        dirs.Add(Path.Combine(roamingAppData, "Signal", "GPUCache"));
        dirs.Add(Path.Combine(roamingAppData, "Signal", "DawnCache"));
        dirs.Add(Path.Combine(roamingAppData, "Signal", "Crashpad", "reports"));

        // 7. Skype Standalone (GPU shaders only)
        dirs.Add(Path.Combine(roamingAppData, "Microsoft", "Skype for Desktop", "GPUCache"));
        dirs.Add(Path.Combine(roamingAppData, "Microsoft", "Skype for Desktop", "DawnCache"));

        // 8. Viber Standalone (Avatars & temp)
        dirs.Add(Path.Combine(localAppData, "ViberPC", "avatars"));
        dirs.Add(Path.Combine(localAppData, "ViberPC", "temp"));

        // 9. Element / Matrix (GPU shaders only)
        dirs.Add(Path.Combine(roamingAppData, "Element", "GPUCache"));

        // 10. Zoom (Logs & temp only, preserving meeting credentials)
        dirs.Add(Path.Combine(localAppData, "Zoom", "temp"));
        dirs.Add(Path.Combine(roamingAppData, "Zoom", "logs"));

        // 11. WeChat & LINE
        dirs.Add(Path.Combine(roamingAppData, "Tencent", "WeChat", "All Users", "Cache"));
        dirs.Add(Path.Combine(localAppData, "LINE", "Cache"));

        // 12. Mattermost & Rocket.Chat (GPU & shader cache only)
        dirs.Add(Path.Combine(roamingAppData, "Mattermost", "GPUCache"));
        dirs.Add(Path.Combine(roamingAppData, "Mattermost", "DawnCache"));
        dirs.Add(Path.Combine(roamingAppData, "Mattermost", "Crashpad", "reports"));
        dirs.Add(Path.Combine(roamingAppData, "Rocket.Chat", "GPUCache"));
        dirs.Add(Path.Combine(roamingAppData, "Rocket.Chat", "DawnCache"));

        // 13. Cisco Webex & RingCentral (GPU & temp only)
        dirs.Add(Path.Combine(localAppData, "Cisco-Spark", "temp"));
        dirs.Add(Path.Combine(roamingAppData, "RingCentral", "GPUCache"));
        dirs.Add(Path.Combine(roamingAppData, "RingCentral", "logs"));

        // 14. Session, Threema, Wire, Keybase (GPU shaders & isolated caches only)
        dirs.Add(Path.Combine(roamingAppData, "Session", "GPUCache"));
        dirs.Add(Path.Combine(roamingAppData, "Session", "DawnCache"));
        dirs.Add(Path.Combine(roamingAppData, "Threema", "GPUCache"));
        dirs.Add(Path.Combine(roamingAppData, "Wire", "GPUCache"));
        dirs.Add(Path.Combine(localAppData, "Keybase", "cache"));

        // 15. KakaoTalk & Meta Messenger Desktop
        dirs.Add(Path.Combine(localAppData, "Kakao", "KakaoTalk", "temp"));
        dirs.Add(Path.Combine(roamingAppData, "Messenger", "GPUCache"));

        return dirs;
    }
}
