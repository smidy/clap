using System;
using Microsoft.Extensions.Logging;

namespace clawapp.Services;

/// <summary>
/// Default implementation of IAppLifecycleService.
/// Tracks foreground/background state and raises events for lifecycle transitions.
/// </summary>
public class AppLifecycleService : IAppLifecycleService
{
    private readonly ILogger<AppLifecycleService> _logger;
    private bool _isForeground = true;
    private readonly object _lock = new();

    public bool IsForeground
    {
        get
        {
            lock (_lock)
            {
                return _isForeground;
            }
        }
    }

    public event EventHandler? ForegroundEntering;
    public event EventHandler? ForegroundEntered;
    public event EventHandler? BackgroundEntering;

    public AppLifecycleService(ILogger<AppLifecycleService> logger)
    {
        _logger = logger;
    }

    public void SetForegroundState(bool isForeground)
    {
        lock (_lock)
        {
            if (_isForeground == isForeground)
            {
                return; // No change
            }

            _isForeground = isForeground;

            if (isForeground)
            {
                _logger.LogDebug("App entering foreground");
                ForegroundEntering?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                _logger.LogDebug("App entering background");
                BackgroundEntering?.Invoke(this, EventArgs.Empty);
            }
        }

        // Raise ForegroundEntered after lock release to avoid blocking
        if (isForeground)
        {
            ForegroundEntered?.Invoke(this, EventArgs.Empty);
        }
    }
}
