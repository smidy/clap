using System;

namespace clawapp.Services;

/// <summary>
/// Factory for creating platform-specific push notification service instances.
/// </summary>
public static class PushNotificationServiceFactory
{
    private static Func<IPushNotificationService>? _factory;

    /// <summary>
    /// Sets the factory function for creating push notification service instances.
    /// Called by platform-specific code (Android/iOS).
    /// </summary>
    public static void SetFactory(Func<IPushNotificationService> factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Creates a push notification service instance using the registered factory,
    /// or returns a null service if no factory is registered (Desktop).
    /// </summary>
    public static IPushNotificationService Create()
    {
        return _factory?.Invoke() ?? new NullPushNotificationService();
    }
}
