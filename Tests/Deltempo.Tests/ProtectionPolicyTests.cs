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
    public void IsProtected_TextAndScriptFilesInTempAndWebCache_Permitted(string tempPath)
    {
        bool protectedFile = ProtectionPolicy.IsProtected(tempPath, out _);
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
