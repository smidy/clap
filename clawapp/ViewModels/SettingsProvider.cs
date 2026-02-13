using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using clawapp.Services;

namespace clawapp.ViewModels;

/// <summary>
/// A singleton provider that exposes settings as bindable properties.
/// This allows views to bind directly to settings values and receive updates.
/// </summary>
public sealed class SettingsProvider : INotifyPropertyChanged
{
    private static SettingsProvider? _instance;
    private static readonly object Lock = new();

    private bool _showThinkingBlocks;
    private bool _showToolCalls = true;
    private bool _showToolResults = true;
    private bool _showStreamingMessages = true;

    public static SettingsProvider Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (Lock)
                {
                    _instance ??= new SettingsProvider();
                }
            }
            return _instance;
        }
    }

    private SettingsProvider()
    {
        // Subscribe to setting changes from SettingsViewModel
        SettingsViewModel.SettingChanged += OnSettingChanged;
    }

    /// <summary>
    /// Shows or hides thinking blocks in the chat view.
    /// </summary>
    public bool ShowThinkingBlocks
    {
        get => _showThinkingBlocks;
        private set
        {
            if (_showThinkingBlocks != value)
            {
                _showThinkingBlocks = value;
                OnPropertyChanged(nameof(ShowThinkingBlocks));
            }
        }
    }

    /// <summary>
    /// Shows or hides tool calls in the chat view.
    /// </summary>
    public bool ShowToolCalls
    {
        get => _showToolCalls;
        private set
        {
            if (_showToolCalls != value)
            {
                _showToolCalls = value;
                OnPropertyChanged(nameof(ShowToolCalls));
            }
        }
    }

    /// <summary>
    /// Shows or hides tool results in the chat view.
    /// </summary>
    public bool ShowToolResults
    {
        get => _showToolResults;
        private set
        {
            if (_showToolResults != value)
            {
                _showToolResults = value;
                OnPropertyChanged(nameof(ShowToolResults));
            }
        }
    }

    /// <summary>
    /// Shows or hides streaming message content. When false, shows typing indicator instead.
    /// </summary>
    public bool ShowStreamingMessages
    {
        get => _showStreamingMessages;
        private set
        {
            if (_showStreamingMessages != value)
            {
                _showStreamingMessages = value;
                OnPropertyChanged(nameof(ShowStreamingMessages));
            }
        }
    }

    /// <summary>
    /// Initializes the provider with values from settings storage.
    /// Call this once at app startup after ISettingsService is available.
    /// </summary>
    public async Task InitializeAsync(ISettingsService settingsService)
    {
        ShowThinkingBlocks = await settingsService.LoadShowThinkingBlocksAsync();
        ShowToolCalls = await settingsService.LoadShowToolCallsAsync();
        ShowToolResults = await settingsService.LoadShowToolResultsAsync();
        ShowStreamingMessages = await settingsService.LoadShowStreamingMessagesAsync();
    }

    private void OnSettingChanged(object? sender, SettingChangedEventArgs e)
    {
        switch (e.SettingName)
        {
            case nameof(SettingsViewModel.ShowThinkingBlocks):
                ShowThinkingBlocks = e.Value is true;
                break;
            case nameof(SettingsViewModel.ShowToolCalls):
                ShowToolCalls = e.Value is true;
                break;
            case nameof(SettingsViewModel.ShowToolResults):
                ShowToolResults = e.Value is true;
                break;
            case nameof(SettingsViewModel.ShowStreamingMessages):
                ShowStreamingMessages = e.Value is true;
                break;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
