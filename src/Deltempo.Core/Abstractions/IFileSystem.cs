using System.Collections.Generic;
using System.IO;

namespace WinTempCleaner.Core.Abstractions;

/// <summary>
/// Abstraction over file system operations enabling sandboxed isolation and deterministic testing.
/// </summary>
public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    long GetFileSize(string path);
    DateTime GetLastWriteTimeUtc(string path);
    void DeleteFile(string path);
    void DeleteDirectory(string path, bool recursive);
    IEnumerable<string> EnumerateFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);
    IEnumerable<string> EnumerateDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);
}

/// <summary>
/// Direct physical file system implementation.
/// </summary>
public class PhysicalFileSystem : IFileSystem
{
    public static readonly PhysicalFileSystem Instance = new();

    public bool FileExists(string path) => File.Exists(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public long GetFileSize(string path) => new FileInfo(path).Length;
    public DateTime GetLastWriteTimeUtc(string path) => File.GetLastWriteTimeUtc(path);
    public void DeleteFile(string path) => File.Delete(path);
    public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);
    public IEnumerable<string> EnumerateFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly) =>
        Directory.EnumerateFiles(path, searchPattern, searchOption);
    public IEnumerable<string> EnumerateDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly) =>
        Directory.EnumerateDirectories(path, searchPattern, searchOption);
}
