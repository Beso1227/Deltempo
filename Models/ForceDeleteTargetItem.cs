using System.ComponentModel;
using System.Runtime.CompilerServices;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;

namespace WinTempCleaner.Models;

public class ForceDeleteTargetItem : INotifyPropertyChanged
{
    private string _statusText = "Ready";
    private bool _isActionInProgress;
    private bool _isSelected = true;

    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public bool IsDirectory { get; set; }
    public string IssueSummary { get; set; } = string.Empty;
    public string LockingProcessName { get; set; } = string.Empty;
    public int LockingProcessId { get; set; }
    public ForceDeleteTier GateTier { get; set; } = ForceDeleteTier.Allowed;

    public string FormattedSize => TargetFolderInfo.FormatBytes(SizeBytes);

    public string KindLabel => IsDirectory ? "Directory" : "File";

    public string DisplayProcessInfo => LockingProcessId > 0
        ? $"{LockingProcessName} (PID {LockingProcessId})"
        : (!string.IsNullOrEmpty(LockingProcessName) ? LockingProcessName : string.Empty);

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

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
