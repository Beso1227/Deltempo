using System;
using System.Diagnostics;
using System.IO;
using WinTempCleaner.Core.Safety;
using Xunit;

namespace Deltempo.Tests;

public class PathSecurityEdgeTests
{
    private static string NewSandbox() =>
        Path.Combine(Path.GetTempPath(), "Deltempo_PathEdge_" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(@"\\?\UNC\server\share\file.txt", @"\\server\share\file.txt")]
    [InlineData(@"\\?\UNC\server\share\", @"\\server\share")]
    public void NormalizeCanonicalPath_FoldsTheUNCDevicePrefixIntoPlainUnc(string input, string expected)
    {
        // Canonicalized UNC must come back WITHOUT the \\?\UNC marker - the fold is
        // what makes a second normalization pass a no-op and lets IsSubpathOf compare
        // raw UNC and device-prefixed UNC as one shape.
        string normalized = PathSecurity.NormalizeCanonicalPath(input);

        Assert.Equal(expected, normalized, ignoreCase: true);
        // And the second pass must be stable (idempotence).
        Assert.Equal(expected, PathSecurity.NormalizeCanonicalPath(normalized), ignoreCase: true);
    }

    [Theory]
    [InlineData(@"\\?\C:\Temp\file.txt", @"C:\Temp\file.txt")]
    [InlineData(@"\\?\C:/Temp\file.txt", @"C:\Temp\file.txt")]
    public void NormalizeCanonicalPath_StripsTheDriveDevicePath(string input, string expected)
    {
        string normalized = PathSecurity.NormalizeCanonicalPath(input);

        Assert.Equal(expected, normalized, ignoreCase: true);
    }

    [Theory]
    [InlineData(@"C:\safe\...\file.txt")] // "..." segment strips to empty - rejected
    [InlineData(@"C:\safe\..\ \file.txt")] // ".. " strips to ".." - traversal resurrection, rejected
    public void NormalizeCanonicalPath_ReturnsEmpty_ForDotSpaceResurrectedSegments(string input)
    {
        // Win32 strips trailing dots/spaces, so these inputs would collapse into an
        // empty component or a resurrected ".." traversal. The normalizer must reject
        // them outright instead of returning a canonical shape that lies.
        Assert.Equal(string.Empty, PathSecurity.NormalizeCanonicalPath(input));
    }

    [Fact]
    public void NormalizeCanonicalPath_StabilizesAUrlInputWithoutThrowing()
    {
        // Observed behavior: GetFullPath resolves a URL-shaped string to a
        // cwd-relative location instead of throwing. The safety contract that still
        // matters is determinism - the result must be stable and non-empty.
        string first = PathSecurity.NormalizeCanonicalPath("https://evil.example/file");
        string second = PathSecurity.NormalizeCanonicalPath("https://evil.example/file");

        Assert.False(string.IsNullOrWhiteSpace(first));
        Assert.Equal(first, second, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalizeCanonicalPath_TreatsWhitespaceAsEmpty()
    {
        Assert.Equal(string.Empty, PathSecurity.NormalizeCanonicalPath(null));
        Assert.Equal(string.Empty, PathSecurity.NormalizeCanonicalPath(string.Empty));
        Assert.Equal(string.Empty, PathSecurity.NormalizeCanonicalPath("   "));
    }

    [Theory]
    [InlineData(null, @"C:\anything")]
    [InlineData("", @"C:\anything")]
    [InlineData("   ", @"C:\anything")]
    [InlineData(@"https://evil.example/x", @"C:\anything")] // candidate canonicalizes to ""
    [InlineData(@"C:\anything", null)]
    [InlineData(@"C:\anything", "")]
    [InlineData(@"C:\anything", @"https://evil.example")] // root canonicalizes to ""
    public void IsSubpathOf_FailsClosed_OnNullOrEmptyOrUncanonicalizableInputs(string? candidate, string? root)
    {
        Assert.False(PathSecurity.IsSubpathOf(candidate!, root!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsReparsePointOrLink_ReturnsFalse_ForNullOrWhitespaceOptionalPaths(string? path)
    {
        Assert.False(PathSecurity.IsReparsePointOrLink(path!));
    }

    [Theory]
    [InlineData(@"C:\definitely_nonexistent_delttempo_probe.file")]
    [InlineData(@"Z:\definitely_nonexistent_delttempo_probe.dir")]
    public void IsReparsePointOrLink_ReturnsFalse_ForPathsNotOnDisk(string path)
    {
        if (Directory.Exists(@"Z:\"))
        {
            // Z: is occasionally mapped by some machines; probe with a nonexistent
            // name on that drive instead so the assertion below stays valid.
            path = @"Z:\definitely_nonexistent_delttempo_probe_dir\" + Guid.NewGuid().ToString("N");
        }

        Assert.False(PathSecurity.IsReparsePointOrLink(path));
    }

    [Fact]
    public void IsReparsePointOrLink_NullFileInfo_ReturnsFalse()
    {
        Assert.False(PathSecurity.IsReparsePointOrLink((FileSystemInfo?)null));
    }

    [Fact]
    public void IsReparsePointOrLink_DetectsARealDirectoryJunction()
    {
        string sandbox = NewSandbox();
        try
        {
            Directory.CreateDirectory(sandbox);
            string targetDir = Path.Combine(sandbox, "target");
            string junctionPath = Path.Combine(sandbox, "junction");
            Directory.CreateDirectory(targetDir);

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c mklink /J \"{junctionPath}\" \"{targetDir}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi)!;
            proc.StandardOutput.ReadToEnd();
            proc.StandardError.ReadToEnd();
            proc.WaitForExit(15000);

            AssumeJunctionExists(junctionPath);

            // Both overloads must classify the junction as hazardous.
            Assert.True(PathSecurity.IsReparsePointOrLink(junctionPath));
            Assert.True(PathSecurity.IsReparsePointOrLink(new DirectoryInfo(junctionPath)));
        }
        finally
        {
            try { Directory.Delete(sandbox, recursive: true); } catch { }
        }
    }

    private static void AssumeJunctionExists(string junctionPath)
    {
        Assert.True(Directory.Exists(junctionPath) && PathSecurity.IsReparsePointOrLink(new DirectoryInfo(junctionPath)),
            $"mklink /J failed to create a junction at '{junctionPath}' in this environment.");
    }

    [Theory]
    [InlineData(@"\\?\C:\Temp", false)]      // device path, NOT a network path
    [InlineData(@"\\?\UNC\server\share", true)] // device-prefixed UNC marker is network
    [InlineData(@"\\?\UNC", false)]          // no share component: not a usable UNC
    [InlineData(@"//server/share", true)]    // forward-slash UNC parses to a file UNC URI
    [InlineData("not a path @#^", false)]
    public void IsUncPath_ClassifiesDeviceAndForwardSlashForms(string path, bool expected)
    {
        Assert.Equal(expected, PathSecurity.IsUncPath(path));
    }

    [Fact]
    public void HasPathTraversalSequences_AnswerFailsClosedOnEmptyInput()
    {
        Assert.True(PathSecurity.HasPathTraversalSequences(string.Empty));
        Assert.True(PathSecurity.HasPathTraversalSequences(null!));
        Assert.True(PathSecurity.HasPathTraversalSequences("trailing.."));
    }
}
