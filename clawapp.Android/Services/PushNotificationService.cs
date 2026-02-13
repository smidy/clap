using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Plugin.Firebase.CloudMessaging;

namespace clawapp.Android.Services;

/// <summary>
/// Android-specific push notification service using Firebase Cloud Messaging (FCM).
/// </summary>
public class PushNotificationService : clawapp.Services.IPushNotificationService
{
    private readonly ILogger<PushNotificationService> _logger;
    private string? _currentToken;

    public PushNotificationService(ILogger<PushNotificationService> logger)
    {
        _logger = logger;
    }

    public string Platform => "fcm";

    public event EventHandler<string>? OnTokenRefreshed;

    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Initializing FCM push notifications");
            System.Diagnostics.Debug.WriteLine("[FIREBASE] Initializing FCM push notifications");

            // Subscribe to token refresh events
            CrossFirebaseCloudMessaging.Current.TokenChanged += OnFirebaseTokenChanged;
            
            // Subscribe to notification received events
            CrossFirebaseCloudMessaging.Current.NotificationReceived += OnNotificationReceived;

            // Get current FCM token
            _currentToken = await CrossFirebaseCloudMessaging.Current.GetTokenAsync();
            
            if (string.IsNullOrEmpty(_currentToken))
            {
                _logger.LogWarning("FCM token is null or empty");
                return false;
            }

            _logger.LogInformation("FCM token received: {TokenPrefix}...", _currentToken.Substring(0, Math.Min(10, _currentToken.Length)));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FCM initialization failed");
            return false;
        }
    }

    public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_currentToken);
    }

    public Task<bool> RegisterTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        _currentToken = token;
        _logger.LogInformation("FCM token registered: {TokenPrefix}...", token.Substring(0, Math.Min(10, token.Length)));
        return Task.FromResult(true);
    }

    public Task<bool> UnregisterTokenAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("FCM token unregistered");
        _currentToken = null;
        
        // Unsubscribe from events
        CrossFirebaseCloudMessaging.Current.TokenChanged -= OnFirebaseTokenChanged;
        CrossFirebaseCloudMessaging.Current.NotificationReceived -= OnNotificationReceived;
        return Task.FromResult(true);
    }

    private void OnFirebaseTokenChanged(object? sender, EventArgs e)
    {
        // Token is available via GetTokenAsync
        _logger.LogInformation("FCM token changed event received");
        OnTokenRefreshed?.Invoke(this, _currentToken ?? string.Empty);
    }

    private void OnNotificationReceived(object? sender, EventArgs e)
    {
        _logger.LogInformation("FCM notification received");
        // Notification is automatically displayed by Plugin.Firebase
    }
}
