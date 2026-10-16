using System;
using System.Linq;
using WinTempCleaner.Core.Update;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// The canonical signed payload must bind the artifact descriptors, not just the metadata.
///
/// This is the security-critical property of the whole manifest-signature scheme. If the
/// signed payload omits <c>artifact</c> / <c>cliArtifact</c>, the signature only authenticates
/// "version 9.9.9 on the stable channel" and says nothing about <i>which file will be
/// downloaded</i>. Anyone able to modify the manifest in transit could then point
/// <c>downloadUrl</c> at their own host, set <c>sha256</c> to the digest of their own binary,
/// and the signature would still verify - so the attacker-supplied binary would be downloaded
/// and pass its own hash check.
///
/// Signing the artifact closes that substitution path.
/// </summary>
public class ManifestSignatureBindingTests
{
    private static UpdateManifest Sample() => new()
    {
        // Pinned: PublishedAtUtc defaults to UtcNow, which would make two samples differ
        // and mask what these tests are actually asserting.
        PublishedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        Product = "Deltempo",
        Channel = "stable",
        Version = "9.9.9",
        CommitSha = "abc123",
        KeyId = "k1",
        Architecture = "win-x64",
        Artifact = new UpdateArtifactInfo
        {
            Name = "Deltempo.exe",
            SizeBytes = 1234,
            Sha256 = new string('a', 64),
            DownloadUrl = "https://github.com/Beso1227/Deltempo/releases/download/v9.9.9/Deltempo.exe",
        },
        CliArtifact = new UpdateArtifactInfo
        {
            Name = "deltempo_cli.exe",
            SizeBytes = 999,
            Sha256 = new string('c', 64),
            DownloadUrl = "https://github.com/Beso1227/Deltempo/releases/download/v9.9.9/deltempo_cli.exe",
        },
    };

    [Fact]
    public void CanonicalJson_IncludesArtifactDescriptor()
    {
        string json = Sample().ToCanonicalJson();

        Assert.Contains("\"artifact\"", json);
        Assert.Contains("\"cliArtifact\"", json);
    }

    [Fact]
    public void CanonicalJson_BindsArtifactSha256()
    {
        var m = Sample();
        m.Artifact!.Sha256 = new string('a', 64);
        string before = m.ToCanonicalJson();

        m.Artifact.Sha256 = new string('b', 64);
        string after = m.ToCanonicalJson();

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void CanonicalJson_BindsArtifactDownloadUrl()
    {
        var m = Sample();
        string before = m.ToCanonicalJson();

        m.Artifact!.DownloadUrl = "https://evil.example.com/Deltempo.exe";
        string after = m.ToCanonicalJson();

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void CanonicalJson_BindsArtifactSize()
    {
        var m = Sample();
        string before = m.ToCanonicalJson();

        m.Artifact!.SizeBytes = 1;
        string after = m.ToCanonicalJson();

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void CanonicalJson_BindsCliArtifact()
    {
        var m = Sample();
        string before = m.ToCanonicalJson();

        m.CliArtifact!.DownloadUrl = "https://evil.example.com/cli.exe";
        Assert.NotEqual(before, m.ToCanonicalJson());
    }

    /// <summary>
    /// The exact substitution attack: keep every signed field intact, repoint the download,
    /// and restate the hash. The canonical payload must change.
    /// </summary>
    [Fact]
    public void CanonicalJson_ResistsArtifactSubstitutionAttack()
    {
        var genuine = Sample();
        string genuineJson = genuine.ToCanonicalJson();

        var forged = Sample();
        forged.Artifact!.DownloadUrl = "https://evil.example.com/Deltempo.exe";
        forged.Artifact.Sha256 = new string('f', 64); // digest of the attacker's binary
        forged.Artifact.SizeBytes = 4321;
        string forgedJson = forged.ToCanonicalJson();

        Assert.NotEqual(genuineJson, forgedJson);
    }

    [Fact]
    public void CanonicalJson_ExcludesTheSignatureItself()
    {
        var m = Sample();
        m.Signature = "AAAA";
        Assert.DoesNotContain(m.Signature, m.ToCanonicalJson());
    }

    [Fact]
    public void CanonicalJson_IsDeterministic()
    {
        var m = Sample();
        Assert.Equal(m.ToCanonicalJson(), m.ToCanonicalJson());
        Assert.Equal(Sample().ToCanonicalJson(), Sample().ToCanonicalJson());
    }

    [Fact]
    public void CanonicalJson_IsValidJson()
    {
        string json = Sample().ToCanonicalJson();
        var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("artifact", out _));
        Assert.True(doc.RootElement.TryGetProperty("cliArtifact", out _));
    }

    [Fact]
    public void CanonicalJson_EscapesQuotesSoValuesCannotForgeStructure()
    {
        var m = Sample();
        m.Artifact!.Name = "Deltempo\",\"injected\":\"yes";

        // Must still parse as one object with the expected shape, not as injected structure.
        var doc = System.Text.Json.JsonDocument.Parse(m.ToCanonicalJson());
        Assert.Equal("Deltempo\",\"injected\":\"yes",
            doc.RootElement.GetProperty("artifact").GetProperty("name").GetString());
        Assert.False(doc.RootElement.TryGetProperty("injected", out _));
    }

    [Fact]
    public void CanonicalJson_HandlesNullArtifacts()
    {
        var m = Sample();
        m.Artifact = null;
        m.CliArtifact = null;

        string json = m.ToCanonicalJson();
        var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, doc.RootElement.GetProperty("artifact").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, doc.RootElement.GetProperty("cliArtifact").ValueKind);
    }
}
