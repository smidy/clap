using System;

namespace clawapp.Services;

/// <summary>
/// Factory for creating platform-specific audio recorder services.
/// Platform projects register their implementation via SetFactory().
/// </summary>
public static class AudioRecorderServiceFactory
{
    private static Func<IAudioRecorderService>? _factory;

    /// <summary>
    /// Register a factory function for creating the platform-specific audio recorder service.
    /// Call this from each platform's startup (Program.cs/MainActivity/AppDelegate).
    /// </summary>
    public static void SetFactory(Func<IAudioRecorderService> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// Creates the platform-specific audio recorder service.
    /// Falls back to the base (unsupported) implementation if no factory is registered.
    /// </summary>
    public static IAudioRecorderService Create()
    {
        return _factory?.Invoke() ?? new AudioRecorderService();
    }
}
