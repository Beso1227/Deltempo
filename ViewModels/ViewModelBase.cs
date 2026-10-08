using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WinTempCleaner.ViewModels;

/// <summary>
/// ViewModel base on CommunityToolkit.Mvvm's <see cref="ObservableObject"/>:
/// it supplies INotifyPropertyChanged and the raise-only-on-change
/// <c>SetProperty&lt;T&gt;</c> helper, so this type carries no hand-rolled
/// notification plumbing of its own.
/// </summary>
/// <remarks>
/// <para><c>RelayCommand</c> / <c>AsyncRelayCommand</c> below are intentionally NOT the
/// toolkit's types of the same name. The toolkit versions are <c>sealed</c>, expose no
/// <c>Action&lt;object?&gt;</c> execute constructor, and name their refresh method
/// <c>NotifyCanExecuteChanged()</c> — while <c>ViewModelCommandTests</c> constructs the
/// parameterised overload and calls <c>RaiseCanExecuteChanged()</c> directly.</para>
/// </remarks>
public abstract class ViewModelBase : ObservableObject
{
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
