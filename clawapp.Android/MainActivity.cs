using System;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia;
using Avalonia.Android;
using clawapp.Android.Services;
using clawapp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Plugin.Firebase.CloudMessaging;

namespace clawapp.Android;

[Activity(
    Label = "clawapp.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    WindowSoftInputMode = SoftInput.AdjustResize,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    private IAppLifecycleService? _lifecycleService;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        try
        {
            // Initialize Firebase Cloud Messaging
            System.Diagnostics.Debug.WriteLine("[FIREBASE] Checking if valid in OnCreate...");
            CrossFirebaseCloudMessaging.Current.CheckIfValidAsync();
            System.Diagnostics.Debug.WriteLine("[FIREBASE] CheckIfValidAsync completed.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FIREBASE] Initialization failed in OnCreate: {ex}");
        }

        // Create notification channel for Cloud Messaging (Android 8.0+)
        CreateNotificationChannel();

        // Handle intent for notification tap
        if (Intent != null)
        {
            try 
            {
                FirebaseCloudMessagingImplementation.OnNewIntent(Intent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE] Failed to handle intent in OnCreate: {ex}");
            }
        }

        var result = this.CheckSelfPermission("android.permission.RECORD_AUDIO");
        if (result == Permission.Denied)
        {
            RequestPermissions(["android.permission.RECORD_AUDIO", ], 1004);
        }
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        if (intent != null)
        {
            try
            {
                FirebaseCloudMessagingImplementation.OnNewIntent(intent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FIREBASE] Failed to handle intent in OnNewIntent: {ex}");
            }
        }
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channelId = $"{ApplicationContext?.PackageName}.general";
            var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
            
            if (notificationManager != null)
            {
                var channel = new NotificationChannel(channelId, "General", NotificationImportance.Default);
                notificationManager.CreateNotificationChannel(channel);
                FirebaseCloudMessagingImplementation.ChannelId = channelId;
            }
        }
    }

    protected override void OnResume()
    {
        base.OnResume();
        _lifecycleService?.SetForegroundState(true);
    }

    protected override void OnPause()
    {
        base.OnPause();
        _lifecycleService?.SetForegroundState(false);
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // Register platform-specific services
        AudioRecorderServiceFactory.SetFactory(() => new AndroidAudioRecorderService());
        NotificationServiceFactory.SetFactory(() => new AndroidNotificationService(this));
        
        // Use debug logger for now to ensure we see logs
        var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug().SetMinimumLevel(LogLevel.Debug));
        var logger = loggerFactory.CreateLogger<PushNotificationService>();
        
        PushNotificationServiceFactory.SetFactory(() => new PushNotificationService(logger));

        var appBuilder = base.CustomizeAppBuilder(builder)
            .WithInterFont();

        // Get lifecycle service after app is built
        appBuilder.AfterSetup(_ =>
        {
            // Mobile - get lifecycle service from DI container
            _lifecycleService = clawapp.App.Services?.GetService<IAppLifecycleService>();
            _lifecycleService?.SetForegroundState(true); // App starts in foreground
        });

        return appBuilder;
    }
}
