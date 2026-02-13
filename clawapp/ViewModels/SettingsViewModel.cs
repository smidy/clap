using System;
using System.Threading.Tasks;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using clawapp.Services;

namespace clawapp.ViewModels;

/// <summary>
/// ViewModel for the settings panel with display and media preferences.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private bool _isInitializing;
    private MainViewModel? _mainViewModel;

    [ObservableProperty]
    private bool _showThinkingBlocks;

    [ObservableProperty]
    private bool _showToolCalls = true;

    [ObservableProperty]
    private bool _showToolResults = true;

    [ObservableProperty]
    private bool _autoPlayAudio;

    [ObservableProperty]
    private bool _autoDownloadAttachments;

    [ObservableProperty]
    private bool _notificationSoundsEnabled = true;

    [ObservableProperty]
    private bool _notificationVibrationEnabled = true;

    [ObservableProperty]
    private bool _showStreamingMessages = true;

    [ObservableProperty]
    private bool _isDarkTheme = true;

    /// <summary>
    /// Reference to MainViewModel for theme synchronization.
    /// </summary>
    public MainViewModel? MainViewModel
    {
        get => _mainViewModel;
        set
        {
            if (_mainViewModel != null)
            {
                _mainViewModel.PropertyChanged -= OnMainViewModelPropertyChanged;
            }
            _mainViewModel = value;
            if (_mainViewModel != null)
            {
                _mainViewModel.PropertyChanged += OnMainViewModelPropertyChanged;
                // Sync initial state
                IsDarkTheme = _mainViewModel.IsDarkTheme;
            }
        }
    }

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    private void OnMainViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsDarkTheme))
        {
            // Only update if different to avoid loops
            var newValue = _mainViewModel?.IsDarkTheme ?? true;
            if (IsDarkTheme != newValue)
            {
                _isInitializing = true;
                IsDarkTheme = newValue;
                _isInitializing = false;
            }
        }
    }

    /// <summary>
    /// Loads all settings from storage. Call this when the view is loaded.
    /// </summary>
    public async Task InitializeAsync()
    {
        _isInitializing = true;
        try
        {
            ShowThinkingBlocks = await _settingsService.LoadShowThinkingBlocksAsync().ConfigureAwait(false);
            ShowToolCalls = await _settingsService.LoadShowToolCallsAsync().ConfigureAwait(false);
            ShowToolResults = await _settingsService.LoadShowToolResultsAsync().ConfigureAwait(false);
            AutoPlayAudio = await _settingsService.LoadAutoPlayAudioAsync().ConfigureAwait(false);
            AutoDownloadAttachments = await _settingsService.LoadAutoDownloadAttachmentsAsync().ConfigureAwait(false);
            NotificationSoundsEnabled = await _settingsService.LoadNotificationSoundsEnabledAsync().ConfigureAwait(false);
            NotificationVibrationEnabled = await _settingsService.LoadNotificationVibrationEnabledAsync().ConfigureAwait(false);
            ShowStreamingMessages = await _settingsService.LoadShowStreamingMessagesAsync().ConfigureAwait(false);

            // Theme is loaded/saved via MainViewModel, but we need to sync our local copy
            var savedTheme = await _settingsService.LoadThemeVariantAsync().ConfigureAwait(false);
            IsDarkTheme = string.IsNullOrEmpty(savedTheme) || savedTheme.Equals("Dark", StringComparison.OrdinalIgnoreCase);

            // Also initialize the SettingsProvider singleton so it has the correct values
            await SettingsProvider.Instance.InitializeAsync(_settingsService).ConfigureAwait(false);
        }
        finally
        {
            _isInitializing = false;
        }
    }

    partial void OnShowThinkingBlocksChanged(bool value)
    {
        if (!_isInitializing)
        {
            _ = _settingsService.SaveShowThinkingBlocksAsync(value);
            NotifySettingChanged(nameof(ShowThinkingBlocks), value);
        }
    }

    partial void OnShowToolCallsChanged(bool value)
    {
        if (!_isInitializing)
        {
            _ = _settingsService.SaveShowToolCallsAsync(value);
            NotifySettingChanged(nameof(ShowToolCalls), value);
        }
    }

    partial void OnShowToolResultsChanged(bool value)
    {
        if (!_isInitializing)
        {
            _ = _settingsService.SaveShowToolResultsAsync(value);
            NotifySettingChanged(nameof(ShowToolResults), value);
        }
    }

    partial void OnAutoPlayAudioChanged(bool value)
    {
        if (!_isInitializing)
        {
            _ = _settingsService.SaveAutoPlayAudioAsync(value);
        }
    }

    partial void OnAutoDownloadAttachmentsChanged(bool value)
    {
        if (!_isInitializing)
        {
            _ = _settingsService.SaveAutoDownloadAttachmentsAsync(value);
        }
    }

    partial void OnNotificationSoundsEnabledChanged(bool value)
    {
        if (!_isInitializing)
        {
            _ = _settingsService.SaveNotificationSoundsEnabledAsync(value);
        }
    }

    partial void OnNotificationVibrationEnabledChanged(bool value)
    {
        if (!_isInitializing)
        {
            _ = _settingsService.SaveNotificationVibrationEnabledAsync(value);
        }
    }

    partial void OnShowStreamingMessagesChanged(bool value)
    {
        if (!_isInitializing)
        {
            _ = _settingsService.SaveShowStreamingMessagesAsync(value);
            NotifySettingChanged(nameof(ShowStreamingMessages), value);
        }
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        if (!_isInitializing)
        {
            // Update MainViewModel's theme - this will trigger the actual theme change
            if (_mainViewModel != null)
            {
                _ = _mainViewModel.SetThemeAsync(value);
            }
            else
            {
                // No MainViewModel reference, save directly and apply
                _ = SaveThemeDirectlyAsync(value);
            }
        }
    }

    private async Task SaveThemeDirectlyAsync(bool isDarkTheme)
    {
        var variant = isDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        if (Avalonia.Application.Current is { } app)
            app.RequestedThemeVariant = variant;
        await _settingsService.SaveThemeVariantAsync(isDarkTheme ? "Dark" : "Light").ConfigureAwait(false);
    }

    /// <summary>
    /// Static event that fires when any setting changes, allowing converters and other components to react.
    /// </summary>
    public static event EventHandler<SettingChangedEventArgs>? SettingChanged;

    /// <summary>
    /// Raises the SettingChanged event to notify listeners of setting changes.
    /// </summary>
    public static void NotifySettingChanged(string settingName, object? value)
    {
        SettingChanged?.Invoke(null, new SettingChangedEventArgs(settingName, value));
    }
}

/// <summary>
/// Event args for setting changes.
/// </summary>
public class SettingChangedEventArgs : EventArgs
{
    public string SettingName { get; }
    public object? Value { get; }

    public SettingChangedEventArgs(string settingName, object? value)
    {
        SettingName = settingName;
        Value = value;
    }
}
