using System;
using System.Threading.Tasks;

namespace clawapp.Services;

/// <summary>
/// Service for playing notification sounds and triggering haptic feedback.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Whether notification features are supported on this platform.
    /// </summary>
    bool IsSupported { get; }
    
    /// <summary>
    /// Plays the default system notification sound.
    /// </summary>
    Task PlayNotificationSoundAsync();
    
    /// <summary>
    /// Triggers haptic vibration for the specified duration.
    /// </summary>
    /// <param name="duration">Duration of vibration (typically 100-500ms)</param>
    Task VibrateAsync(TimeSpan duration);
}
