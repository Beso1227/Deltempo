using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WinTempCleaner.Models;

public class LockedFileItem : INotifyPropertyChanged
{
    private string _statusText = "Locked";
    private bool _isActionInProgress;

    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string LockingProcessName { get; set; } = string.Empty;
    public int LockingProcessId { get; set; }
    public string LockReason { get; set; } = string.Empty;

    public string FormattedSize => TargetFolderInfo.FormatBytes(SizeBytes);

    public string DisplayProcessInfo => LockingProcessId > 0
        ? $"{LockingProcessName} (PID {LockingProcessId})"
        : (!string.IsNullOrEmpty(LockingProcessName) ? LockingProcessName : "Active Application");

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public bool IsActionInProgress
    {
        get => _isActionInProgress;
        set { _isActionInProgress = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
