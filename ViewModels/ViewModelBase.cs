using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace WinTempCleaner.ViewModels;

/// <summary>
/// INotifyPropertyChanged base for the Phase 2 ViewModel migration.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// Sets the backing field and raises PropertyChanged only when the value
    /// actually changed. Returns true when a notification was raised.
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

/// <summary>
/// Basic synchronous ICommand for the MVVM migration: routes Execute/CanExecute
/// to delegates; CanExecuteChanged is raised explicitly (no CommandManager
/// magic), keeping re-evaluation deterministic and testable.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute == null ? null : new Predicate<object?>(_ => canExecute()))
    {
        if (execute == null) throw new ArgumentNullException(nameof(execute));
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);

    public void Execute(object? parameter) => _execute(parameter);

    /// <summary>Re-evaluates CanExecute for all bound controls.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Async ICommand with an in-flight guard: CanExecute is false while the work is
/// running, preventing double-execution of long operations (scan/clean/boost).
/// Exceptions propagate to the global dispatcher handler (crash log).
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Predicate<object?>? _canExecute;
    private bool _isRunning;

    public AsyncRelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute == null ? null : new Predicate<object?>(_ => canExecute()))
    {
        if (execute == null) throw new ArgumentNullException(nameof(execute));
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) =>
        !_isRunning && (_canExecute == null || _canExecute(parameter));

    // async void is the ICommand contract; exceptions surface to the dispatcher.
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;

        _isRunning = true;
        RaiseCanExecuteChanged();
        try
        {
            await _execute(parameter).ConfigureAwait(true);
        }
        finally
        {
            _isRunning = false;
            RaiseCanExecuteChanged();
        }
    }

    /// <summary>Re-evaluates CanExecute for all bound controls.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
