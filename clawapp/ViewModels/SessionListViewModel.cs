using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using clawapp.Input;
using clawapp.Models;
using clawapp.Services;

namespace clawapp.ViewModels;

public partial class SessionListViewModel : ViewModelBase
{
    private readonly IOpenClawService _openClawService;
    private readonly Func<Session?, Task>? _onSessionSelected;

    [ObservableProperty]
    private Session? _selectedSession;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<Session> Sessions { get; } = new();

    /// <summary>
    /// Command to refresh the session list. Uses AvaloniaAsyncRelayCommand to ensure
    /// CanExecuteChanged is raised on the UI thread.
    /// </summary>
    public IAsyncRelayCommand RefreshCommand { get; }

    public SessionListViewModel(IOpenClawService openClawService, Func<Session?, Task>? onSessionSelected = null)
    {
        _openClawService = openClawService;
        _onSessionSelected = onSessionSelected;

        RefreshCommand = new AvaloniaAsyncRelayCommand(RefreshAsync);
    }

    partial void OnSelectedSessionChanged(Session? value)
    {
        if (_onSessionSelected != null && value != null)
            _ = _onSessionSelected(value);
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (!_openClawService.IsConnected) return;

        IsLoading = true;
        try
        {
            var list = await _openClawService.GetSessionsAsync(limit: 50).ConfigureAwait(false);

            // Marshal back to UI thread for collection updates
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Sessions.Clear();
                foreach (var s in list)
                    Sessions.Add(s);
                if (Sessions.Count > 0 && SelectedSession == null)
                    SelectedSession = Sessions[0];
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
        }
    }
}
