using WinTempCleaner.Models;
using WinTempCleaner.ViewModels;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Tests for the Phase 2 MVVM foundations: ViewModelBase change notification
/// and RelayCommand / AsyncRelayCommand semantics (gating, re-entrancy guard,
/// CanExecuteChanged signaling).
/// </summary>
public class ViewModelCommandTests
{
    private sealed class TestVm : ViewModelBase
    {
        private int _value;

        public int Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
    }

    [Fact]
    public void ViewModelBase_SetProperty_RaisesOnlyOnChange()
    {
        var vm = new TestVm();
        int count = 0;
        string? lastName = null;
        vm.PropertyChanged += (_, e) => { count++; lastName = e.PropertyName; };

        vm.Value = 5;

        Assert.Equal(1, count);
        Assert.Equal(nameof(TestVm.Value), lastName);

        vm.Value = 5;

        Assert.Equal(1, count);

        vm.Value = 7;

        Assert.Equal(2, count);
    }

    [Fact]
    public void RelayCommand_ExecutesAndGatesByCanExecute()
    {
        int executions = 0;
        bool allowed = false;
        var cmd = new RelayCommand(() => executions++, () => allowed);

        Assert.False(cmd.CanExecute(null));
        cmd.Execute(null);
        Assert.Equal(1, executions);

        allowed = true;
        Assert.True(cmd.CanExecute(null));
        cmd.Execute(null);
        Assert.Equal(2, executions);
    }

    [Fact]
    public void RelayCommand_PassesParameterThrough()
    {
        object? received = null;
        var cmd = new RelayCommand(p => received = p);

        cmd.Execute("payload");

        Assert.Equal("payload", received);
    }

    [Fact]
    public void RelayCommand_CanExecuteChanged_RaisedOnDemand()
    {
        int raised = 0;
        var cmd = new RelayCommand(() => { }, () => true);
        cmd.CanExecuteChanged += (_, _) => raised++;

        cmd.RaiseCanExecuteChanged();
        cmd.RaiseCanExecuteChanged();

        Assert.Equal(2, raised);
    }

    [Fact]
    public void RelayCommand_NullExecute_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new RelayCommand((Action<object?>)null!));
        Assert.Throws<ArgumentNullException>(() => new RelayCommand((Action)null!));
        Assert.Throws<ArgumentNullException>(() => new AsyncRelayCommand((Func<object?, Task>)null!));
        Assert.Throws<ArgumentNullException>(() => new AsyncRelayCommand((Func<Task>)null!));
    }

    [Fact]
    public async Task AsyncRelayCommand_GuardsReentrancyAndReenablesAfterCompletion()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var cmd = new AsyncRelayCommand(async () =>
        {
            started.SetResult();
            await release.Task;
        });

        cmd.Execute(null);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(cmd.CanExecute(null));
        Assert.False(cmd.CanExecute("anything"));

        release.SetResult();
        await Task.Delay(50);

        Assert.True(cmd.CanExecute(null));
    }

    [Fact]
    public async Task AsyncRelayCommand_CombinesCustomGateWithRunningState()
    {
        bool allowed = false;
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cmd = new AsyncRelayCommand(() => finished.Task, () => allowed);

        Assert.False(cmd.CanExecute(null));

        allowed = true;
        Assert.True(cmd.CanExecute(null));

        cmd.Execute(null);
        allowed = false;
        finished.SetResult();
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50);

        Assert.False(cmd.CanExecute(null));
    }

    [Fact]
    public void RelayCommand_NullExecute_Throws_ForConvenienceOverload()
    {
        // Convenience overloads validate the original delegate, not the wrapper.
        Assert.Throws<ArgumentNullException>(() => new RelayCommand((Action)null!));
        Assert.Throws<ArgumentNullException>(() => new AsyncRelayCommand((Func<Task>)null!));
    }
}
