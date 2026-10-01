using System;
using System.IO;
using WinTempCleaner.Core.Safety;
using Xunit;

namespace Deltempo.Tests;

public class ProtectionPolicyTests
{
    [Theory]
    [InlineData(@"C:\pagefile.sys")]
    [InlineData(@"C:\hiberfil.sys")]
    [InlineData(@"C:\swapfile.sys")]
    [InlineData(@"C:\bootmgr")]
    public void IsProtected_WindowsRootSystemFiles_AlwaysProtected(string rootPath)
    {
        bool protectedFile = ProtectionPolicy.IsProtected(rootPath, out string reason);
        Assert.True(protectedFile);
        Assert.Contains("critical OS root", reason);
    }

    /// <summary>
    /// C:\Users\Public holds shared user content. The guard that covers the current user's
    /// Documents/Desktop/etc. previously skipped it entirely, leaving every Public library
    /// reachable by a cleanup scope or by Force Delete.
    /// </summary>
    [Fact]
    public void IsProtected_PublicProfileSharedLibraries_AlwaysProtected()
    {
        string publicRoot = ResolvePublicRoot();

        string[] sharedLibraries =
            ["Documents", "Desktop", "Pictures", "Music", "Videos", "Downloads", "Libraries"];

        foreach (string library in sharedLibraries)
        {
            string probe = Path.Combine(publicRoot, library, "shared-file.txt");
            bool isProtected = ProtectionPolicy.IsProtected(probe, out string reason);

            Assert.True(isProtected, $"Public\\{library} must be protected, but '{probe}' was allowed.");
            Assert.Contains("personal library", reason, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// The protection above must not be so broad that legitimate cleanup targets become
    /// unreachable: Public\Temp is disposable scratch space, and an uninstalled application's
    /// user-data folder parked in Public is residue rather than a shared library.
    /// </summary>
    [Fact]
    public void IsProtected_PublicTempAndAppResidue_NotBlockedByLibraryGuard()
    {
        string publicRoot = ResolvePublicRoot();

        string tempProbe = Path.Combine(publicRoot, "Temp", "scratch-cache.dat");
        Assert.False(
            ProtectionPolicy.IsProtected(tempProbe, out string tempReason),
            $"Public\\Temp should stay cleanable. Blocked with: {tempReason}");

        string residueProbe = Path.Combine(publicRoot, "SomeUninstalledApp", "WebView2Cache", "cache_data_1");
        Assert.False(
            ProtectionPolicy.IsProtected(residueProbe, out string residueReason),
            $"Orphaned app residue in Public should be reachable. Blocked with: {residueReason}");
    }

    /// <summary>
    /// Derives the Public profile root from the machine's own folder ids so the tests assert the
    /// real resolved locations instead of assuming a C:\Users\Public layout.
    /// </summary>
    private static string ResolvePublicRoot()
    {
        string commonDocs = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);
        Assert.False(string.IsNullOrEmpty(commonDocs), "CommonDocuments must resolve on a Windows host.");

        string? root = Path.GetDirectoryName(commonDocs);
        Assert.False(string.IsNullOrEmpty(root), "Public profile root must be derivable from CommonDocuments.");
        return root!;
    }

    [Theory]
    [InlineData(@"C:\Windows\System32\ntoskrnl.exe")]
    [InlineData(@"C:\Windows\System32\drivers\etc\hosts")]
    [InlineData(@"C:\Windows\SysWOW64\cmd.exe")]
    [InlineData(@"C:\Windows\WinSxS\manifest.xml")]
    public void IsProtected_WindowsCoreDirectories_AlwaysProtected(string sysPath)
    {
        bool protectedFile = ProtectionPolicy.IsProtected(sysPath, out string reason);
        Assert.True(protectedFile);
        Assert.Contains("kernel/system directory", reason);
    }

    [Theory]
    [InlineData(@"C:\Windows\System32\DriverStore\Temp\setup.log", false)]
    [InlineData(@"C:\Windows\System32\DriverStore\Temp\staging.tmp", false)]
    [InlineData(@"C:\Windows\System32\DriverState\cache.dat", false)]
    [InlineData(@"C:\Windows\System32\DriverStore\Temp\driver.sys", true)]
    [InlineData(@"C:\Windows\System32\DriverStore\Temp\installer.exe", true)]
    [InlineData(@"C:\Windows\System32\DriverStore\Temp\payload.dll", true)]
    [InlineData(@"C:\Windows\System32\DriverStore\Temp\driver.inf", true)]
    [InlineData(@"C:\Windows\System32\DriverStore\Temp\catalog.cat", true)]
    [InlineData(@"C:\Windows\System32\DriverStore\FileRepository\nv_dispi.inf_amd64\nv_dispi.inf", true)]
    [InlineData(@"C:\Windows\System32\drivers\etc\hosts", true)]
    [InlineData(@"C:\Windows\System32\kernel32.dll", true)]
    public void ProtectionPolicy_System32SubpathSafety_HandlesSafeExceptionsCorrectly(string path, bool expectedProtected)
    {
        bool isProtected = ProtectionPolicy.IsProtected(path, out _);
        Assert.Equal(expectedProtected, isProtected);
    }

    [Theory]
    [InlineData(@"C:\Users\user\.ssh\id_rsa")]
    [InlineData(@"C:\Users\user\.ssh\id_ed25519")]
    [InlineData(@"C:\Users\user\.ssh\known_hosts")]
    [InlineData(@"C:\Users\user\.aws\credentials")]
    [InlineData(@"C:\Users\user\.kube\config")]
    [InlineData(@"C:\Users\user\.gnupg\secring.gpg")]
    public void IsProtected_DeveloperAndCloudCredentials_AlwaysProtected(string credPath)
    {
        bool protectedFile = ProtectionPolicy.IsProtected(credPath, out string reason);
        Assert.True(protectedFile);
        Assert.Contains("Developer / SSH / Cloud", reason);
    }

    [Theory]
    [InlineData(@"C:\Users\user\AppData\Local\Google\Chrome\User Data\Default\Login Data")]
    [InlineData(@"C:\Users\user\AppData\Local\Google\Chrome\User Data\Default\Cookies")]
    [InlineData(@"C:\Users\user\AppData\Local\Microsoft\Edge\User Data\Default\Web Data")]
    public void IsProtected_BrowserSavedPasswordsAndCookies_AlwaysProtected(string browserPath)
    {
        bool protectedFile = ProtectionPolicy.IsProtected(browserPath, out string reason);
        Assert.True(protectedFile);
        Assert.Contains("saved login credentials", reason);
    }

    [Theory]
    [InlineData(@"D:\SteamLibrary\steamapps\common\Cyberpunk 2077\bin\x64\Cyberpunk2077.exe")]
    [InlineData(@"C:\Program Files\Epic Games\Fortnite\FortniteGame\Binaries\Win64\FortniteClient.exe")]
    public void IsProtected_GamePlatformLibraries_AlwaysProtected(string gamePath)
    {
        bool protectedFile = ProtectionPolicy.IsProtected(gamePath, out string reason);
        Assert.True(protectedFile);
        Assert.Contains("Gaming Platform Assets", reason);
    }

    [Theory]
    [InlineData(@"D:\Models\llama-3-8b.Q4_K_M.gguf")]
    [InlineData(@"D:\StableDiffusion\models\sd_xl_base_1.0.safetensors")]
    [InlineData(@"D:\ML\model.onnx")]
    [InlineData(@"D:\PyTorch\checkpoint.pt")]
    public void IsProtected_AiWeightsAndModels_AlwaysProtected(string modelPath)
    {
        bool protectedFile = ProtectionPolicy.IsProtected(modelPath, out string reason);
        Assert.True(protectedFile);
        Assert.Contains("AI Model Weights", reason);
    }

    [Theory]
    [InlineData(@"C:\Users\user\Documents\passwords.kdbx")]
    [InlineData(@"C:\Users\user\Downloads\corporate_key.pem")]
    [InlineData(@"C:\Users\user\Desktop\financial_report.xlsx")]
    [InlineData(@"C:\Users\user\AppData\Local\Temp\my_contract.pdf")]
    public void IsProtected_SensitiveExtensions_AlwaysProtected(string docPath)
    {
        bool protectedFile = ProtectionPolicy.IsProtected(docPath, out string reason);
        Assert.True(protectedFile);
        Assert.True(reason.Contains("Protected sensitive file type") || reason.Contains("Cryptographic Key"));
    }

    [Theory]
    [InlineData(@"C:\Users\user\AppData\Local\npm-cache\_cacache\content-v2\sha512\index.js")]
    [InlineData(@"C:\Users\user\AppData\Local\pip\cache\wheels\script.py")]
    [InlineData(@"C:\Users\user\.gradle\caches\modules-2\files-2.1\module.ts")]
    [InlineData(@"C:\Users\user\.bun\install\cache\module.ts")]
    [InlineData(@"C:\Users\user\AppData\Local\Google\Chrome\User Data\Default\Code Cache\js\cache.js")]
    public void IsProtected_PackageAndScriptCaches_Permitted(string cacheFilePath)
    {
        bool protectedFile = ProtectionPolicy.IsProtected(cacheFilePath, out _);
        Assert.False(protectedFile);
    }

    [Theory]
    [InlineData(@"C:\Users\user\AppData\Local\npm-cache\_cacache\secret.kdbx")]
    [InlineData(@"C:\Users\user\AppData\Local\npm-cache\_cacache\key.pem")]
    [InlineData(@"C:\Users\user\.gradle\caches\modules-2\id_rsa")]
    public void IsProtected_CredentialsInsideCaches_StillProtected(string credentialInCachePath)
    {
        bool protectedFile = ProtectionPolicy.IsProtected(credentialInCachePath, out string reason);
        Assert.True(protectedFile);
        Assert.True(reason.Contains("Cryptographic Key") || reason.Contains("Developer / SSH / Cloud"));
    }

    [Theory]
    [InlineData(@"C:\Users\user\AppData\Local\Temp\install.txt")]
    [InlineData(@"C:\Users\user\AppData\Local\Temp\setup_log.csv")]
    [InlineData(@"C:\Users\user\AppData\Local\Temp\bundle.js")]
    [InlineData(@"C:\Windows\Temp\setup.log.txt")]
    [InlineData(@"C:\Users\user\AppData\Local\Microsoft\Windows\INetCache\style.css")]
    [InlineData(@"C:\Users\user\AppData\Local\Google\Chrome\User Data\Default\Cache\Cache_Data\index.html")]
    [InlineData(@"C:\Users\user\AppData\Local\Microsoft\Edge\User Data\Default\EBWebView\Default\Cache\data_0")]
    [InlineData(@"C:\Users\user\AppData\Local\Packages\SpotifyAB.SpotifyMusic_zpdnekdrzrea0\LocalCache\Spotify\Data\index.json")]
    [InlineData(@"C:\Users\user\AppData\Local\Discord\Cache\Cache_Data\f_000001")]
    [InlineData(@"C:\Users\user\AppData\Local\D3DSCache\hash\cache.bin")]
    public void IsProtected_TextAndScriptFilesInTempAndWebCache_Permitted(string tempPath)
    {
        bool protectedFile = ProtectionPolicy.IsProtected(tempPath, out _);
        Assert.False(protectedFile);
    }

    [Theory]
    [InlineData(@"C:\Program Files (x86)\Steam\steamapps\downloading\1086940\chunk.pak")]
    [InlineData(@"D:\SteamLibrary\steamapps\shadercache\1086940\DX12.bin")]
    [InlineData(@"C:\Program Files (x86)\Steam\steamapps\temp\staging.tmp")]
    [InlineData(@"C:\Program Files\Epic Games\Launcher\Portal\Saved\webcache\data_0")]
    [InlineData(@"C:\Program Files\Epic Games\Launcher\Portal\Saved\Logs\launcher.log")]
    public void IsProtected_GamingLauncherCachesAndDownloads_Permitted(string gamingCachePath)
    {
        bool protectedFile = ProtectionPolicy.IsProtected(gamingCachePath, out _);
        Assert.False(protectedFile);
    }

    private static readonly string[] SampleCustomPathExclusions = [@"C:\MyProtectedFolder"];
    private static readonly string[] SampleCustomExtensionExclusions = [".mycustomext", "backup"];

    [Fact]
    public void IsProtected_CustomPathExclusions_ProtectsCustomFolder()
    {
        try
        {
            ProtectionPolicy.SetCustomExclusions(SampleCustomPathExclusions, null);
            bool isProtected = ProtectionPolicy.IsProtected(@"C:\MyProtectedFolder\SubFolder\temp.tmp", out string reason);
            Assert.True(isProtected);
            Assert.Contains("User custom excluded path", reason);
        }
        finally
        {
            ProtectionPolicy.SetCustomExclusions(null, null);
        }
    }

    [Fact]
    public void IsProtected_CustomExtensionExclusions_ProtectsMatchingExtension()
    {
        try
        {
            ProtectionPolicy.SetCustomExclusions(null, SampleCustomExtensionExclusions);
            bool isProtected1 = ProtectionPolicy.IsProtected(@"C:\Users\user\AppData\Local\Temp\data.mycustomext", out string reason1);
            Assert.True(isProtected1);
            Assert.Contains("User custom excluded file extension", reason1);

            bool isProtected2 = ProtectionPolicy.IsProtected(@"C:\Users\user\AppData\Local\Temp\data.backup", out string reason2);
            Assert.True(isProtected2);
            Assert.Contains("User custom excluded file extension", reason2);

            bool notProtected = ProtectionPolicy.IsProtected(@"C:\Users\user\AppData\Local\Temp\data.tmp", out _);
            Assert.False(notProtected);
        }
        finally
        {
            ProtectionPolicy.SetCustomExclusions(null, null);
        }
    }
}
