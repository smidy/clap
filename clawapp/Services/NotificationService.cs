using System;
using System.Threading.Tasks;

namespace clawapp.Services;

/// <summary>
/// Base notification service implementation (no-op for unsupported platforms).
/// </summary>
public class NotificationService : INotificationService
{
    /// <inheritdoc/>
    public virtual bool IsSupported => false;
    
    /// <inheritdoc/>
    public virtual Task PlayNotificationSoundAsync()
    {
        return Task.CompletedTask;
    }
    
    /// <inheritdoc/>
    public virtual Task VibrateAsync(TimeSpan duration)
    {
        return Task.CompletedTask;
    }
}
