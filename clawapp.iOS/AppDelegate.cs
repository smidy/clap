using Foundation;
using UIKit;
using Avalonia;
using Avalonia.Controls;
using Avalonia.iOS;
using Avalonia.Media;
using clawapp.iOS.Services;
using clawapp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace clawapp.iOS;

// The UIApplicationDelegate for the application. This class is responsible for launching the 
// User Interface of the application, as well as listening (and optionally responding) to 
// application events from iOS.
[Register("AppDelegate")]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
public partial class AppDelegate : AvaloniaAppDelegate<App>
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
{
    private IAppLifecycleService? _lifecycleService;

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // Register platform-specific audio recorder service
        AudioRecorderServiceFactory.SetFactory(() => new iOSAudioRecorderService());
        
        var appBuilder = base.CustomizeAppBuilder(builder)
            .WithInterFont();

        // Get lifecycle service after app is built
        appBuilder.AfterSetup(_ =>
        {
            _lifecycleService = clawapp.App.Services?.GetService<IAppLifecycleService>();
            _lifecycleService?.SetForegroundState(true); // App starts in foreground
        });

        return appBuilder;
    }

    public override void DidEnterBackground(UIApplication application)
    {
        base.DidEnterBackground(application);
        _lifecycleService?.SetForegroundState(false);
    }

    public override void WillEnterForeground(UIApplication application)
    {
        base.WillEnterForeground(application);
        _lifecycleService?.SetForegroundState(true);
    }
}
