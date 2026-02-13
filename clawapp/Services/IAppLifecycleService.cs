using System;

namespace clawapp.Services;

/// <summary>
/// Tracks application lifecycle state (foreground/background) across platforms.
/// Used to buffer UI updates when the app is backgrounded.
/// </summary>
public interface IAppLifecycleService
{
    /// <summary>
    /// Gets whether the application is currently in the foreground.
    /// </summary>
    bool IsForeground { get; }

    /// <summary>
    /// Raised when the application enters the foreground.
    /// </summary>
    event EventHandler? ForegroundEntering;

    /// <summary>
    /// Raised when the application has entered the foreground and UI is ready.
    /// </summary>
    event EventHandler? ForegroundEntered;

    /// <summary>
    /// Raised when the application is leaving the foreground (entering background).
    /// </summary>
    event EventHandler? BackgroundEntering;

    /// <summary>
    /// Sets the foreground state. Called by platform-specific lifecycle handlers.
    /// </summary>
    void SetForegroundState(bool isForeground);
}
