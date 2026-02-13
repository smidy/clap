using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using clawapp.Input;
using clawapp.Services;

namespace clawapp.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOpenClawService _openClawService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private ViewModelBase? _currentViewModel;

    [ObservableProperty]
    private bool _isDarkTheme = true;

    public ConnectionViewModel ConnectionViewModel { get; }
    public ChatViewModel ChatViewModel { get; }

    /// <summary>
    /// Initializes MainViewModel with dependency injection container.
    /// </summary>
    /// <param name="serviceProvider">The DI container for creating services and viewmodels.</param>
    public MainViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _openClawService = serviceProvider.GetRequiredService<IOpenClawService>();
        _settingsService = serviceProvider.GetRequiredService<ISettingsService>();

        // Create ViewModels via DI - allows for better testability and loose coupling
        ConnectionViewModel = serviceProvider.GetRequiredService<ConnectionViewModel>();
        ChatViewModel = serviceProvider.GetRequiredService<ChatViewModel>();

        // Wire up ChatViewModel with MainViewModel reference for theme sync
        ChatViewModel.SetMainViewModel(this);

        // Listen for connection events
        ConnectionViewModel.ConnectionSucceeded += OnConnectionSucceeded;
        _openClawService.OnDisconnected += OnDisconnected;

        CurrentViewModel = ConnectionViewModel;
        _ = ConnectionViewModel.LoadSavedSettingsAsync();
        _ = LoadThemeAsync();
    }

    /// <summary>
    /// Legacy constructor for backward compatibility. Use dependency injection instead.
    /// </summary>
    [Obsolete("Use DI constructor that accepts IServiceProvider instead")]
    public MainViewModel(IOpenClawService openClawService, ISettingsService settingsService, IAudioRecorderService audioRecorderService, ILoggerFactory loggerFactory)
        : this(CreateFallbackServiceProvider(openClawService, settingsService, audioRecorderService, loggerFactory))
    {
    }

    /// <summary>
    /// Creates a fallback service provider for the legacy constructor.
    /// </summary>
    private static IServiceProvider CreateFallbackServiceProvider(
        IOpenClawService openClawService,
        ISettingsService settingsService,
        IAudioRecorderService audioRecorderService,
        ILoggerFactory loggerFactory)
    {
        var services = new ServiceCollection();
        services.AddSingleton(openClawService);
        services.AddSingleton(settingsService);
        services.AddSingleton(audioRecorderService);
        services.AddSingleton(loggerFactory);
        services.AddLogging();
        services.AddSingleton<IAppLifecycleService, AppLifecycleService>();
        services.AddSingleton<INotificationService>(sp =>
            NotificationServiceFactory.Create());
        services.AddSingleton(sp =>
            new ConnectionViewModel(
                sp.GetRequiredService<IOpenClawService>(),
                sp.GetRequiredService<ISettingsService>(),
                loggerFactory.CreateLogger<ConnectionViewModel>()));
        services.AddSingleton(sp =>
            new ChatViewModel(
                sp.GetRequiredService<IOpenClawService>(),
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<IAudioRecorderService>(),
                sp.GetRequiredService<INotificationService>(),
                sp.GetRequiredService<IAppLifecycleService>(),
                loggerFactory.CreateLogger<ChatViewModel>()));
        return services.BuildServiceProvider();
    }

    private void OnConnectionSucceeded(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            CurrentViewModel = ChatViewModel;
            _ = ChatViewModel.InitializeAsync();
        });
    }

    private void OnDisconnected(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            CurrentViewModel = ConnectionViewModel;
        });
    }

    private async Task LoadThemeAsync()
    {
        var saved = await _settingsService.LoadThemeVariantAsync().ConfigureAwait(false);
        Dispatcher.UIThread.Post(() =>
        {
            if (string.Equals(saved, "Light", StringComparison.OrdinalIgnoreCase))
            {
                IsDarkTheme = false;
                ApplyTheme(ThemeVariant.Light);
            }
            else if (string.Equals(saved, "Dark", StringComparison.OrdinalIgnoreCase))
            {
                IsDarkTheme = true;
                ApplyTheme(ThemeVariant.Dark);
            }
            else
            {
                var isDark = Avalonia.Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
                IsDarkTheme = isDark;
            }
        });
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        // Notify ChatViewModel's Settings of the theme change
        ChatViewModel.Settings.IsDarkTheme = value;
    }

    /// <summary>
    /// Toggles the theme between Dark and Light.
    /// Called from SettingsViewModel when user toggles the theme switch.
    /// </summary>
    public async Task ToggleThemeAsync(CancellationToken cancellationToken = default)
    {
        IsDarkTheme = !IsDarkTheme;
        var variant = IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        ApplyTheme(variant);
        var value = IsDarkTheme ? "Dark" : "Light";
        await _settingsService.SaveThemeVariantAsync(value).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the theme explicitly.
    /// </summary>
    public async Task SetThemeAsync(bool isDarkTheme, CancellationToken cancellationToken = default)
    {
        if (IsDarkTheme != isDarkTheme)
        {
            IsDarkTheme = isDarkTheme;
            var variant = IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
            ApplyTheme(variant);
            var value = IsDarkTheme ? "Dark" : "Light";
            await _settingsService.SaveThemeVariantAsync(value).ConfigureAwait(false);
        }
    }

    private static void ApplyTheme(ThemeVariant variant)
    {
        if (Avalonia.Application.Current is { } app)
            app.RequestedThemeVariant = variant;
    }
}
