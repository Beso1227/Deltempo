using WinTempCleaner.Core.Update;
using Xunit;

namespace Deltempo.Tests;

public class PatchMetadataValidatorTests
{
    [Fact]
    public void Validate_ValidManifest_ReturnsSuccess()
    {
        var manifest = new PatchManifest
        {
            Channel = "patch",
            Product = "Deltempo",
            CommitSha = "3b99da42eed6cd80f1b1a8c42c003ce522bede56",
            ShortSha = "3b99da4",
            Sha256 = "7c4ad1a7ae915ff29d87c52d0c6aa1547b467ee605d32ce96a785f9501b60578",
            FileSizeBytes = 67526580,
            DownloadUrl = "https://github.com/Beso1227/Deltempo/releases/download/patch/Deltempo.exe"
        };

        var result = PatchMetadataValidator.Validate(manifest);
        Assert.True(result.IsValid, result.ErrorMessage);
    }

    [Fact]
    public void Validate_NullManifest_ReturnsFailure()
    {
        var result = PatchMetadataValidator.Validate(null);
        Assert.False(result.IsValid);
        Assert.Contains("null", result.ErrorMessage);
    }

    [Theory]
    [InlineData("stable")]
    [InlineData("beta")]
    [InlineData("")]
    public void Validate_WrongChannel_ReturnsFailure(string channel)
    {
        var manifest = new PatchManifest
        {
            Channel = channel,
            CommitSha = "3b99da42eed6cd80f1b1a8c42c003ce522bede56",
            Sha256 = "7c4ad1a7ae915ff29d87c52d0c6aa1547b467ee605d32ce96a785f9501b60578",
            FileSizeBytes = 67526580,
            DownloadUrl = "https://github.com/Beso1227/Deltempo/releases/download/patch/Deltempo.exe"
        };

        var result = PatchMetadataValidator.Validate(manifest);
        Assert.False(result.IsValid);
        Assert.Contains("Invalid channel", result.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("not-a-valid-hex-sha!")]
    public void Validate_InvalidCommitSha_ReturnsFailure(string commitSha)
    {
        var manifest = new PatchManifest
        {
            Channel = "patch",
            CommitSha = commitSha,
            Sha256 = "7c4ad1a7ae915ff29d87c52d0c6aa1547b467ee605d32ce96a785f9501b60578",
            FileSizeBytes = 67526580,
            DownloadUrl = "https://github.com/Beso1227/Deltempo/releases/download/patch/Deltempo.exe"
        };

        var result = PatchMetadataValidator.Validate(manifest);
        Assert.False(result.IsValid);
        Assert.Contains("CommitSha", result.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("tooshort")]
    [InlineData("7c4ad1a7ae915ff29d87c52d0c6aa1547b467ee605d32ce96a785f9501b6057Z")] // invalid char 'Z'
    public void Validate_InvalidSha256_ReturnsFailure(string sha256)
    {
        var manifest = new PatchManifest
        {
            Channel = "patch",
            CommitSha = "3b99da42eed6cd80f1b1a8c42c003ce522bede56",
            Sha256 = sha256,
            FileSizeBytes = 67526580,
            DownloadUrl = "https://github.com/Beso1227/Deltempo/releases/download/patch/Deltempo.exe"
        };

        var result = PatchMetadataValidator.Validate(manifest);
        Assert.False(result.IsValid);
        Assert.Contains("Sha256", result.ErrorMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1024)] // 1 KB (truncated)
    [InlineData(600L * 1024 * 1024)] // 600 MB (implausible)
    public void Validate_PlausibleSizeBounds_RejectsOutOfRange(long size)
    {
        var manifest = new PatchManifest
        {
            Channel = "patch",
            CommitSha = "3b99da42eed6cd80f1b1a8c42c003ce522bede56",
            Sha256 = "7c4ad1a7ae915ff29d87c52d0c6aa1547b467ee605d32ce96a785f9501b60578",
            FileSizeBytes = size,
            DownloadUrl = "https://github.com/Beso1227/Deltempo/releases/download/patch/Deltempo.exe"
        };

        var result = PatchMetadataValidator.Validate(manifest);
        Assert.False(result.IsValid);
        Assert.Contains("bounds", result.ErrorMessage);
    }

    [Theory]
    [InlineData("http://github.com/Beso1227/Deltempo/releases/download/patch/Deltempo.exe")] // Plain HTTP
    [InlineData("https://evil-hacker.com/Deltempo.exe")] // Untrusted domain
    [InlineData("https://github.com.evil.com/Deltempo.exe")] // Subdomain spoofing
    [InlineData("ftp://github.com/file.exe")] // FTP
    public void Validate_UntrustedDownloadUrl_ReturnsFailure(string untrustedUrl)
    {
        var manifest = new PatchManifest
        {
            Channel = "patch",
            CommitSha = "3b99da42eed6cd80f1b1a8c42c003ce522bede56",
            Sha256 = "7c4ad1a7ae915ff29d87c52d0c6aa1547b467ee605d32ce96a785f9501b60578",
            FileSizeBytes = 67526580,
            DownloadUrl = untrustedUrl
        };

        var result = PatchMetadataValidator.Validate(manifest);
        Assert.False(result.IsValid);
        Assert.Contains("DownloadUrl", result.ErrorMessage);
    }
}
