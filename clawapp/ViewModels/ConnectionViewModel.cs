using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using clawapp.Input;
using clawapp.Models;
using clawapp.Services;

namespace clawapp.ViewModels;

public partial class ConnectionViewModel : ViewModelBase
{
    private readonly IOpenClawService _openClawService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<ConnectionViewModel> _logger;

    [ObservableProperty]
    private string _gatewayHost = "127.0.0.1";

    [ObservableProperty]
    private string _gatewayPort = "18789";

    [ObservableProperty]
    private string _gatewayToken = "";

    [ObservableProperty]
    private bool _useSecureWebSocket = false;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string _errorMessage = "";

    // Connection state UI properties
    [ObservableProperty]
    private ConnectionState _connectionState = ConnectionState.Disconnected;

    [ObservableProperty]
    private int _currentRetryAttempt;

    [ObservableProperty]
    private string _connectionStateText = "Disconnected";

    [ObservableProperty]
    private string _stateIndicatorColor = "#808080"; // Gray for disconnected

    [ObservableProperty]
    private bool _showManualReconnect;

    [ObservableProperty]
    private string _retryAttemptText = "";

    public event EventHandler? ConnectionSucceeded;

    /// <summary>
    /// Command to connect to the gateway. Uses AvaloniaAsyncRelayCommand to ensure
    /// CanExecuteChanged is raised on the UI thread.
    /// </summary>
    public ICommand ConnectCommand { get; }

    /// <summary>
    /// Command to manually reconnect to the gateway.
    /// </summary>
    public ICommand ReconnectCommand { get; }

    /// <summary>
    /// Command to disconnect from the gateway.
    /// </summary>
    public ICommand DisconnectCommand { get; }

    public ConnectionViewModel(IOpenClawService openClawService, ISettingsService settingsService, ILogger<ConnectionViewModel> logger)
    {
        _openClawService = openClawService;
        _settingsService = settingsService;
        _logger = logger;

        ConnectCommand = new AvaloniaAsyncRelayCommand(ConnectAsync);
        ReconnectCommand = new AvaloniaAsyncRelayCommand(ReconnectAsync);
        DisconnectCommand = new AvaloniaAsyncRelayCommand(DisconnectAsync);

        // Subscribe to service events
        _openClawService.OnConnectionStateChanged += OnServiceConnectionStateChanged;
        _openClawService.OnReconnectAttempt += OnServiceReconnectAttempt;
        _openClawService.OnReconnected += OnServiceReconnected;
        _openClawService.OnDisconnected += OnServiceDisconnected;
    }

    private void OnServiceConnectionStateChanged(object? sender, ConnectionState state)
    {
        _logger.LogDebug("Connection state changed to {State}", state);
        Dispatcher.UIThread.Post(() => UpdateConnectionStateUI(state));
    }

    private void OnServiceReconnectAttempt(object? sender, int attempt)
    {
        _logger.LogDebug("Reconnect attempt {Attempt}", attempt);
        Dispatcher.UIThread.Post(() =>
        {
            CurrentRetryAttempt = attempt;
            RetryAttemptText = $"Attempt {attempt}";
        });
    }

    private void OnServiceReconnected(object? sender, EventArgs e)
    {
        _logger.LogInformation("Successfully reconnected");
        Dispatcher.UIThread.Post(() =>
        {
            CurrentRetryAttempt = 0;
            RetryAttemptText = "";
            ShowManualReconnect = false;
            StatusMessage = "Reconnected successfully!";
        });
    }

    private void OnServiceDisconnected(object? sender, EventArgs e)
    {
        _logger.LogInformation("Disconnected from service");
        Dispatcher.UIThread.Post(() =>
        {
            // Show manual reconnect button if we were previously connected
            ShowManualReconnect = ConnectionState == ConnectionState.Connected ||
                                  ConnectionState == ConnectionState.Reconnecting;
            UpdateConnectionStateUI(ConnectionState.Disconnected);
        });
    }

    private void UpdateConnectionStateUI(ConnectionState state)
    {
        ConnectionState = state;

        switch (state)
        {
            case ConnectionState.Disconnected:
                ConnectionStateText = "Disconnected";
                StateIndicatorColor = "#808080"; // Gray
                IsConnecting = false;
                StatusMessage = "";
                break;

            case ConnectionState.Connecting:
                ConnectionStateText = "Connecting...";
                StateIndicatorColor = "#2196F3"; // Blue
                IsConnecting = true;
                StatusMessage = "Connecting to gateway...";
                ShowManualReconnect = false;
                break;

            case ConnectionState.Connected:
                ConnectionStateText = "Connected";
                StateIndicatorColor = "#4CAF50"; // Green
                IsConnecting = false;
                StatusMessage = "Connected to gateway";
                ShowManualReconnect = false;
                CurrentRetryAttempt = 0;
                RetryAttemptText = "";
                break;

            case ConnectionState.Reconnecting:
                ConnectionStateText = "Reconnecting...";
                StateIndicatorColor = "#FF9800"; // Orange
                IsConnecting = true;
                StatusMessage = $"Reconnecting... (Attempt {CurrentRetryAttempt})";
                ShowManualReconnect = true;
                break;
        }
    }

    public async Task LoadSavedSettingsAsync()
    {
        var saved = await _settingsService.LoadGatewaySettingsAsync().ConfigureAwait(false);
        if (saved != null)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                GatewayHost = saved.Host;
                // Only show port if explicitly specified
                GatewayPort = saved.Port.HasValue ? saved.Port.Value.ToString() : "";
                GatewayToken = saved.Token ?? "";
                UseSecureWebSocket = saved.UseSecureWebSocket;
            });
        }
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (IsConnecting && ConnectionState != ConnectionState.Reconnecting) return;

        ErrorMessage = "";
        StatusMessage = "Connecting...";
        IsConnecting = true;

        try
        {
            // Parse port - if blank or invalid, use null (no port in URI, uses default 80/443)
            int? port = null;
            if (!string.IsNullOrWhiteSpace(GatewayPort) && int.TryParse(GatewayPort, out var p) && p > 0)
                port = p;

            await _openClawService.ConnectAsync(GatewayHost, port, GatewayToken, UseSecureWebSocket, cancellationToken).ConfigureAwait(false);
            await _settingsService.SaveGatewaySettingsAsync(new GatewayInfo
            {
                Host = GatewayHost,
                Port = port,
                Token = GatewayToken,
                UseSecureWebSocket = UseSecureWebSocket
            }, cancellationToken).ConfigureAwait(false);

            // Marshal back to UI thread for property updates
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusMessage = "Connected.";
                ConnectionSucceeded?.Invoke(this, EventArgs.Empty);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection failed: {Message}", ex.Message);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ErrorMessage = ex.Message;
                StatusMessage = "";
                ShowManualReconnect = true;
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (ConnectionState != ConnectionState.Connected)
                {
                    IsConnecting = false;
                }
            });
        }
    }

    private async Task ReconnectAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Manual reconnect triggered");

        // Cancel any existing reconnection attempts first
        await _openClawService.CancelReconnectionAsync().ConfigureAwait(false);

        ErrorMessage = "";
        CurrentRetryAttempt = 0;
        RetryAttemptText = "";

        await ConnectAsync(cancellationToken);
    }

    private async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Manual disconnect triggered");

        try
        {
            // Cancel any ongoing reconnection attempts
            await _openClawService.CancelReconnectionAsync().ConfigureAwait(false);

            // Disconnect from the service
            await _openClawService.DisconnectAsync(cancellationToken).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusMessage = "Disconnected.";
                ShowManualReconnect = true;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnect: {Message}", ex.Message);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ErrorMessage = $"Disconnect error: {ex.Message}";
            });
        }
    }
}
