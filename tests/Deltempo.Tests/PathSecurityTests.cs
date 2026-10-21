using System;
using System.IO;
using WinTempCleaner.Core.Safety;
using Xunit;

namespace Deltempo.Tests;

public class PathSecurityTests
{
    [Theory]
    [InlineData(@"C:\Windows\System32\..\System32\notepad.exe", @"C:\Windows\System32\notepad.exe")]
    [InlineData(@"C:/Users/Test/AppData/Local/Temp", @"C:\Users\Test\AppData\Local\Temp")]
    public void NormalizeCanonicalPath_NormalizesSeparatorsAndDots(string input, string expected)
    {
        string normalized = PathSecurity.NormalizeCanonicalPath(input);
        Assert.Equal(expected, normalized, ignoreCase: true);
    }

    [Theory]
    [InlineData(@"C:\Temp\..\..\Windows\System32", true)]
    [InlineData(@"..\..\secret.txt", true)]
    [InlineData(@"C:\Temp\folder\..\file.txt", true)]
    [InlineData(@"C:\SafeFolder\subfolder\file.txt", false)]
    public void HasPathTraversalSequences_DetectsDoubleDots(string path, bool expected)
    {
        bool hasTraversal = PathSecurity.HasPathTraversalSequences(path);
        Assert.Equal(expected, hasTraversal);
    }

    [Theory]
    [InlineData(@"C:\Safe\Root\child.tmp", @"C:\Safe\Root", true)]
    [InlineData(@"C:\Safe\Root\sub\nested.tmp", @"C:\Safe\Root", true)]
    [InlineData(@"C:\Safe\RootOther\file.tmp", @"C:\Safe\Root", false)]
    [InlineData(@"C:\Windows\System32\file.dll", @"C:\Safe\Root", false)]
    public void IsSubpathOf_AccuratelyEnforcesRootBoundary(string target, string root, bool expected)
    {
        bool isContained = PathSecurity.IsSubpathOf(target, root);
        Assert.Equal(expected, isContained);
    }

    [Theory]
    [InlineData(@"\\server\share\file.txt", true)]
    [InlineData(@"\\?\UNC\server\share\file.txt", true)]
    [InlineData(@"C:\Local\file.txt", false)]
    [InlineData(@"D:\Data\file.tmp", false)]
    public void IsUncPath_AccuratelyDetectsNetworkShares(string path, bool expected)
    {
        bool isUnc = PathSecurity.IsUncPath(path);
        Assert.Equal(expected, isUnc);
    }
}
