using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using clawapp.Services;
using clawapp.ViewModels;
using clawapp.Views;

namespace clawapp;

public partial class App : Application
{
    private IServiceProvider? _services;

    /// <summary>
    /// Gets the application's service provider.
    /// Available after OnFrameworkInitializationCompleted is called.
    /// </summary>
    public static IServiceProvider? Services => (Current as App)?._services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Setup global exception handlers before initializing services
        SetupGlobalExceptionHandlers();
        
        DisableAvaloniaDataAnnotationValidation();
        _services = ConfigureServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Create MainWindow with DI-provided MainViewModel
            var mainViewModel = _services.GetRequiredService<MainViewModel>();
            var mainWindow = new MainWindow(mainViewModel);
            
            desktop.MainWindow = mainWindow;
            desktop.Exit += (_, _) =>
            {
                if (_services is IDisposable disposable)
                    disposable.Dispose();
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            var mainView = _services.GetRequiredService<MainView>();
            singleView.MainView = mainView;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Configures the dependency injection container.
    /// </summary>
    private IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Configuration
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";
        
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .Build();
        
        services.AddSingleton<IConfiguration>(config);

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConfiguration(config.GetSection("Logging"));
            builder.AddDebug();
            #if DEBUG
            builder.SetMinimumLevel(LogLevel.Debug);
            #endif
        });

        // Services - Singleton lifetime for application-wide services
        services.AddSingleton<IPushNotificationService>(provider =>
            PushNotificationServiceFactory.Create());
        services.AddSingleton<IOpenClawService>(provider =>
            new OpenClawService(
                provider.GetRequiredService<ILogger<OpenClawService>>(),
                provider.GetRequiredService<IPushNotificationService>()));
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IAudioRecorderService>(provider =>
            AudioRecorderServiceFactory.Create());
        services.AddSingleton<INotificationService>(provider =>
            NotificationServiceFactory.Create());
        services.AddSingleton<IAppLifecycleService, AppLifecycleService>();

        // Views
        // MainWindow is created manually in OnFrameworkInitializationCompleted with MainViewModel dependency
        // MainView is for mobile/single-view platforms (Android/iOS)
        // ChatView, ConnectionView, SettingsView are created dynamically as needed
        services.AddSingleton<MainView>();
        services.AddTransient<ChatView>();
        services.AddTransient<ConnectionView>();
        services.AddTransient<SettingsView>();

        // ViewModels
        services.AddSingleton<ConnectionViewModel>();
        services.AddSingleton<ChatViewModel>();
        services.AddTransient<SettingsViewModel>();
        
        // MainViewModel requires IServiceProvider for creating child ViewModels
        services.AddSingleton<MainViewModel>(provider =>
            new MainViewModel(provider));

        // Navigation (if implementing NavigationService later)
        // services.AddSingleton<INavigationService, NavigationService>();

        return services.BuildServiceProvider();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    /// <summary>
    /// Sets up global exception handlers for unhandled exceptions and task exceptions.
    /// Per Avalonia Book Chapter 4.
    /// </summary>
    private static void SetupGlobalExceptionHandlers()
    {
        // Handle unhandled exceptions on the AppDomain
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                Debug.WriteLine($"[FATAL] Unhandled AppDomain exception: {ex.Message}");
                Debug.WriteLine($"{ex.StackTrace}");
            }
        };

        // Handle unobserved task exceptions
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            Debug.WriteLine($"[ERROR] Unobserved task exception: {e.Exception?.Message}");
            Debug.WriteLine($"{e.Exception?.StackTrace}");
            e.SetObserved();
        };
    }
}