using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;

namespace clawapp.Input;

/// <summary>
/// A wrapper around <see cref="AsyncRelayCommand"/> that marshals 
/// <see cref="ICommand.CanExecuteChanged"/> and <see cref="INotifyPropertyChanged.PropertyChanged"/>
/// events to the Avalonia UI thread.
/// </summary>
/// <remarks>
/// This is necessary because CommunityToolkit.Mvvm's AsyncRelayCommand raises these events
/// on whatever thread the async operation completes on, which causes "Call from invalid thread"
/// exceptions in Avalonia when the Button tries to access its Command property.
/// See: https://github.com/CommunityToolkit/dotnet/issues/777
/// </remarks>
public sealed class AvaloniaAsyncRelayCommand : IAsyncRelayCommand, INotifyPropertyChanged
{
    private readonly AsyncRelayCommand _innerCommand;

    public AvaloniaAsyncRelayCommand(Func<Task> execute)
    {
        _innerCommand = new AsyncRelayCommand(execute);
        SubscribeToInnerCommand();
    }

    public AvaloniaAsyncRelayCommand(Func<CancellationToken, Task> execute)
    {
        _innerCommand = new AsyncRelayCommand(execute);
        SubscribeToInnerCommand();
    }

    private void SubscribeToInnerCommand()
    {
        _innerCommand.CanExecuteChanged += (s, e) =>
        {
            if (Dispatcher.UIThread.CheckAccess())
                CanExecuteChanged?.Invoke(this, e);
            else
                Dispatcher.UIThread.Post(() => CanExecuteChanged?.Invoke(this, e));
        };

        _innerCommand.PropertyChanged += (s, e) =>
        {
            if (Dispatcher.UIThread.CheckAccess())
                PropertyChanged?.Invoke(this, e);
            else
                Dispatcher.UIThread.Post(() => PropertyChanged?.Invoke(this, e));
        };
    }

    public event EventHandler? CanExecuteChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public Task? ExecutionTask => _innerCommand.ExecutionTask;
    public bool IsRunning => _innerCommand.IsRunning;
    public bool CanBeCanceled => _innerCommand.CanBeCanceled;
    public bool IsCancellationRequested => _innerCommand.IsCancellationRequested;

    public bool CanExecute(object? parameter) => _innerCommand.CanExecute(parameter);

    public void Execute(object? parameter) => _innerCommand.Execute(parameter);

    public Task ExecuteAsync(object? parameter) => _innerCommand.ExecuteAsync(parameter);

    public void NotifyCanExecuteChanged()
    {
        if (Dispatcher.UIThread.CheckAccess())
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        else
            Dispatcher.UIThread.Post(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
    }

    public void Cancel() => _innerCommand.Cancel();
}

/// <summary>
/// A wrapper around <see cref="AsyncRelayCommand{T}"/> that marshals events to the Avalonia UI thread.
/// </summary>
public sealed class AvaloniaAsyncRelayCommand<T> : IAsyncRelayCommand<T>, INotifyPropertyChanged
{
    private readonly AsyncRelayCommand<T> _innerCommand;

    public AvaloniaAsyncRelayCommand(Func<T?, Task> execute)
    {
        _innerCommand = new AsyncRelayCommand<T>(execute);
        SubscribeToInnerCommand();
    }

    public AvaloniaAsyncRelayCommand(Func<T?, CancellationToken, Task> execute)
    {
        _innerCommand = new AsyncRelayCommand<T>(execute);
        SubscribeToInnerCommand();
    }

    private void SubscribeToInnerCommand()
    {
        _innerCommand.CanExecuteChanged += (s, e) =>
        {
            if (Dispatcher.UIThread.CheckAccess())
                CanExecuteChanged?.Invoke(this, e);
            else
                Dispatcher.UIThread.Post(() => CanExecuteChanged?.Invoke(this, e));
        };

        _innerCommand.PropertyChanged += (s, e) =>
        {
            if (Dispatcher.UIThread.CheckAccess())
                PropertyChanged?.Invoke(this, e);
            else
                Dispatcher.UIThread.Post(() => PropertyChanged?.Invoke(this, e));
        };
    }

    public event EventHandler? CanExecuteChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public Task? ExecutionTask => _innerCommand.ExecutionTask;
    public bool IsRunning => _innerCommand.IsRunning;
    public bool CanBeCanceled => _innerCommand.CanBeCanceled;
    public bool IsCancellationRequested => _innerCommand.IsCancellationRequested;

    public bool CanExecute(object? parameter) => _innerCommand.CanExecute(parameter);
    public bool CanExecute(T? parameter) => _innerCommand.CanExecute(parameter);

    public void Execute(object? parameter) => _innerCommand.Execute(parameter);
    public void Execute(T? parameter) => _innerCommand.Execute(parameter);

    public Task ExecuteAsync(object? parameter) => _innerCommand.ExecuteAsync(parameter);
    public Task ExecuteAsync(T? parameter) => _innerCommand.ExecuteAsync(parameter);

    public void NotifyCanExecuteChanged()
    {
        if (Dispatcher.UIThread.CheckAccess())
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        else
            Dispatcher.UIThread.Post(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
    }

    public void Cancel() => _innerCommand.Cancel();
}
