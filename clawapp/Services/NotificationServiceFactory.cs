using System;

namespace clawapp.Services;

/// <summary>
/// Factory for creating platform-specific notification services.
/// Platform projects register their implementation via SetFactory().
/// </summary>
public static class NotificationServiceFactory
{
    private static Func<INotificationService>? _factory;

    /// <summary>
    /// Register a factory function for creating the platform-specific notification service.
    /// Call this from each platform's startup (Program.cs/MainActivity/AppDelegate).
    /// </summary>
    public static void SetFactory(Func<INotificationService> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// Creates the platform-specific notification service.
    /// Falls back to the base (unsupported) implementation if no factory is registered.
    /// </summary>
    public static INotificationService Create()
    {
        return _factory?.Invoke() ?? new NotificationService();
    }
}
