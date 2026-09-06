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
}
