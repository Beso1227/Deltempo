using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinTempCleaner.Core.Safety;
using WinTempCleaner.Models;
using WinTempCleaner.Services;

namespace Deltempo.App.WinUI.ViewModels;

/// <summary>
/// WinUI 3 port of the In-Use and Locked Files inspector.
///
/// Read-only by design, with one exception the WPF modal also has: the user can
/// register a held file with the Windows Session Manager (MoveFileEx) for
/// deletion on the next reboot. That registers a pending operation with the OS;
/// nothing is deleted in this session, and registration fails closed without
/// elevation rather than silently doing nothing.
/// </summary>
public sealed partial class LockedFilesViewModel : ObservableObject
{
    public LockedFilesViewModel()
    {
        CloseCommand = new RelayCommand(() =>
        {
            SoundService.PlayClickSound();
            CloseRequested?.Invoke();
        });

        ScheduleOneCommand = new AsyncRelayCommand<LockedFileRowViewModel>(row =>
        {
            SoundService.PlayClickSound();
            return ScheduleOneAsync(row);
        });

        ScheduleAllCommand = new AsyncRelayCommand(ScheduleAllAsync);
    }

    public event Action<string, LogLevel>? LogRequested;

    public event Action? CloseRequested;

    /// <summary>
    /// Set when ScheduleAll completes, so the view can raise a dialog. WinUI has no
    /// MessageBox, and ContentDialog needs the view's XamlRoot, so the VM decides
    /// and the view renders.
    /// </summary>
    public event Action<RebootScheduleOutcome>? RebootScheduleCompleted;

    public ObservableCollection<LockedFileRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    public partial string SummaryText { get; set; } = "0 in-use files held by active applications";

    [ObservableProperty]
    public partial string FooterNotice { get; set; } =
        "Reboot purge utilizes native Windows MoveFileExW Session Manager registration";

    public bool HasRows => Rows.Count > 0;

    public IRelayCommand CloseCommand { get; }

    public IAsyncRelayCommand<LockedFileRowViewModel> ScheduleOneCommand { get; }

    public IAsyncRelayCommand ScheduleAllCommand { get; }

    /// <summary>Replaces the rows, mirroring the WPF PopulateAndOpen path.</summary>
    public void Load(IEnumerable<LockedFileItem> lockedItems)
    {
        Rows.Clear();
        foreach (LockedFileItem item in lockedItems)
        {
            Rows.Add(new LockedFileRowViewModel(item, ScheduleOneCommand));
        }

        SummaryText = $"{Rows.Count:N0} in-use file{(Rows.Count == 1 ? "" : "s")} held by active applications";
        OnPropertyChanged(nameof(HasRows));
    }

    private Task ScheduleOneAsync(LockedFileRowViewModel? row)
    {
        // CommandParameter can be null if the button is invoked without a row.
        if (row is null)
        {
            return Task.CompletedTask;
        }

        try
        {
            if (RestartManagerService.ScheduleRebootDeletion(row.Item.FilePath))
            {
                row.SetStatus("Scheduled for Reboot");
                LogRequested?.Invoke(
                    $"Registered '{row.Item.FileName}' for deletion upon next system reboot",
                    LogLevel.Success);
            }
            else
            {
                row.SetStatus("Requires Admin");
                LogRequested?.Invoke(
                    $"Could not schedule '{row.Item.FileName}' for reboot deletion (Administrator rights required)",
                    LogLevel.Warning);
            }
        }
        catch (Exception ex)
        {
            row.SetStatus("Failed");
            LogRequested?.Invoke($"Error scheduling reboot deletion for '{row.Item.FileName}': {ex.Message}", LogLevel.Error);
        }

        return Task.CompletedTask;
    }

    private async Task ScheduleAllAsync()
    {
        int scheduled = 0;
        int failed = 0;

        SoundService.PlayClickSound();

        foreach (LockedFileRowViewModel row in Rows)
        {
            try
            {
                if (RestartManagerService.ScheduleRebootDeletion(row.Item.FilePath))
                {
                    row.SetStatus("Scheduled for Reboot");
                    scheduled++;
                }
                else
                {
                    row.SetStatus("Requires Admin");
                    failed++;
                }
            }
            catch
            {
                failed++;
            }
        }

        if (scheduled > 0)
        {
            LogRequested?.Invoke(
                $"Scheduled {scheduled:N0} in-use file(s) for deletion upon next system reboot via Session Manager",
                LogLevel.Success);

            RebootScheduleCompleted?.Invoke(RebootScheduleOutcome.Scheduled(scheduled));
        }
        else if (failed > 0)
        {
            RebootScheduleCompleted?.Invoke(RebootScheduleOutcome.ElevationRequiredResult);
        }

        await Task.CompletedTask;
    }
}

/// <summary>Result of a bulk reboot-schedule attempt, rendered by the view.</summary>
public sealed record RebootScheduleOutcome(int ScheduledCount, bool ElevationRequired)
{
    public static RebootScheduleOutcome Scheduled(int count) => new(count, ElevationRequired: false);

    public static RebootScheduleOutcome ElevationRequiredResult { get; } =
        new(0, ElevationRequired: true);
}

/// <summary>
/// One locked-file row.
///
/// Wraps <see cref="LockedFileItem"/> rather than binding straight to it so the row
/// owns its own Schedule command (x:Bind cannot reach a page-level command from
/// inside a DataTemplate) and can surface the status change as its own property
/// change, so the template updates without a manual refresh.
/// </summary>
public sealed partial class LockedFileRowViewModel : ObservableObject, IDisposable
{
    private readonly LockedFileItem _item;

    public LockedFileRowViewModel(LockedFileItem item, IAsyncRelayCommand<LockedFileRowViewModel> scheduleCommand)
    {
        _item = item;
        ScheduleCommand = scheduleCommand;

        _item.PropertyChanged += OnItemChanged;
    }

    public LockedFileItem Item => _item;

    public string FileName => _item.FileName;

    public string FilePath => _item.FilePath;

    public string FormattedSize => _item.FormattedSize;

    public string DisplayProcessInfo => _item.DisplayProcessInfo;

    [ObservableProperty]
    public partial string StatusText { get; set; }

    public IAsyncRelayCommand<LockedFileRowViewModel> ScheduleCommand { get; }

    internal void SetStatus(string status)
    {
        _item.StatusText = status;
        StatusText = status;
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LockedFileItem.StatusText))
        {
            StatusText = _item.StatusText;
        }
    }

    public void Dispose() => _item.PropertyChanged -= OnItemChanged;
}