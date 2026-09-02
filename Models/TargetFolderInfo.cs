using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WinTempCleaner.Models;

public class TargetFolderInfo : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private long _sizeBytes;
    private int _fileCount;
    private int _folderCount;
    private bool _isScanning;
    private bool _isCleaning;
    private string _statusMessage = "Pending Scan";
    private bool _requiresAdmin;
    private bool _hasAccess = true;
    private List<JunkFileItem> _topFiles = new();
    private string _safetyBadge = "🟢 100% Safe Cache";
    private string _safetyBadgeColor = "#10B981";

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string CategoryColor { get; set; } = "#3B82F6";
    public string Description { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = string.Empty;
    public bool IsSpecialShellTarget { get; set; }
    public bool IsSafeModeEligible { get; set; } = true;
    public bool IsOrphanedAppFolder { get; set; }

    public string SafetyBadge
    {
        get => _safetyBadge;
        set { _safetyBadge = value; OnPropertyChanged(); }
    }

    public string SafetyBadgeColor
    {
        get => _safetyBadgeColor;
        set { _safetyBadgeColor = value; OnPropertyChanged(); }
    }

    public bool RequiresAdmin
    {
        get => _requiresAdmin;
        set { _requiresAdmin = value; OnPropertyChanged(); }
    }

    public bool HasAccess
    {
        get => _hasAccess;
        set { _hasAccess = value; OnPropertyChanged(); }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public long SizeBytes
    {
        get => _sizeBytes;
        set
        {
            _sizeBytes = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FormattedSize));
        }
    }

    public int FileCount
    {
        get => _fileCount;
        set
        {
            _fileCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FormattedStats));
        }
    }

    public int FolderCount
    {
        get => _folderCount;
        set
        {
            _folderCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FormattedStats));
        }
    }

    public bool IsScanning
    {
        get => _isScanning;
        set { _isScanning = value; OnPropertyChanged(); }
    }

    public bool IsCleaning
    {
        get => _isCleaning;
        set { _isCleaning = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public List<JunkFileItem> TopFiles
    {
        get => _topFiles;
        set { _topFiles = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasTopFiles)); }
    }

    public bool HasTopFiles => _topFiles.Count > 0;

    public string FormattedSize => FormatBytes(SizeBytes);

    public string FormattedStats => IsSpecialShellTarget 
        ? $"{FileCount:N0} items" 
        : $"{FileCount:N0} files, {FolderCount:N0} folders";

    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
            if (counter >= suffixes.Length - 1) break;
        }
        return $"{number:n1} {suffixes[counter]}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
