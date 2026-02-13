using System;
using System.Threading;
using System.Threading.Tasks;

namespace clawapp.Services;

/// <summary>
/// Platform-specific push notification service for FCM (Android) and APNs (iOS).
/// </summary>
public interface IPushNotificationService
{
    /// <summary>
    /// Initialize push notifications and request permissions.
    /// </summary>
    Task<bool> InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current FCM/APNs token.
    /// </summary>
    Task<string?> GetTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Register the push token with the gateway.
    /// </summary>
    Task<bool> RegisterTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unregister the push token from the gateway.
    /// </summary>
    Task<bool> UnregisterTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Platform type ("fcm" for Android, "apns" for iOS).
    /// </summary>
    string Platform { get; }

    /// <summary>
    /// Fired when a new token is received (refresh).
    /// </summary>
    event EventHandler<string>? OnTokenRefreshed;
}
