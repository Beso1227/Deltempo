using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WinTempCleaner.Models;

/// <summary>
/// One application that is holding cleanup files open, and how many of them it holds.
/// </summary>
/// <remarks>
/// The cleanup used to report only a COUNT of files that stayed behind ("77 in use"),
/// which leaves the user with a number and no action: the files are still there and
/// the app has told the user nothing about why. The holder is the actionable part -
/// "close chrome and run it again" is something the user can act on, "77 in use" is
/// not.
/// </remarks>
public class InUseApp : INotifyPropertyChanged
{
    private int _fileCount;

    /// <summary>Process name of the holder, e.g. "chrome".</summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>Process id of the holder, when known.</summary>
    public int ProcessId { get; set; }

    /// <summary>How many cleanup files this application is holding open.</summary>
    public int FileCount
    {
        get => _fileCount;
        set { _fileCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayText)); }
    }

    /// <summary>Total bytes held open by this application.</summary>
    public long Bytes { get; set; }

    /// <summary>"chrome (42 files)" - the line the popup shows.</summary>
    public string DisplayText => ProcessId > 0
        ? $"{ProcessName} ({FileCount:N0} file{(FileCount == 1 ? string.Empty : "s")}, PID {ProcessId})"
        : $"{ProcessName} ({FileCount:N0} file{(FileCount == 1 ? string.Empty : "s")})";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
