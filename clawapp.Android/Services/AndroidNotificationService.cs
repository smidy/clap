using System;
using System.Threading.Tasks;
using Android.Content;
using Android.Media;
using Android.OS;
using clawapp.Services;

namespace clawapp.Android.Services;

/// <summary>
/// Android notification service using system notification sound and vibrator.
/// </summary>
public sealed class AndroidNotificationService : INotificationService
{
    private MediaPlayer? _mediaPlayer;
    private readonly Context _context;

    public AndroidNotificationService(Context context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public bool IsSupported => true;

    /// <inheritdoc/>
    public Task PlayNotificationSoundAsync()
    {
        try
        {
            // Clean up previous MediaPlayer if exists
            _mediaPlayer?.Release();
            
            // Get default notification sound URI
            var notificationUri = RingtoneManager.GetDefaultUri(RingtoneType.Notification);
            if (notificationUri == null)
            {
                // Fallback to default ringtone if notification sound not available
                notificationUri = RingtoneManager.GetDefaultUri(RingtoneType.Ringtone);
            }

            if (notificationUri == null)
                return Task.CompletedTask; // No system sound available

            // Create and play MediaPlayer
            _mediaPlayer = MediaPlayer.Create(_context, notificationUri);
            if (_mediaPlayer != null)
            {
                _mediaPlayer.SetAudioAttributes(
                    new AudioAttributes.Builder()
                        .SetUsage(AudioUsageKind.Notification)
                        .SetContentType(AudioContentType.Sonification)
                        .Build()
                );
                
                _mediaPlayer.Completion += (sender, args) =>
                {
                    _mediaPlayer?.Release();
                    _mediaPlayer = null;
                };
                
                _mediaPlayer.Start();
            }
        }
        catch (Exception)
        {
            // Silently fail if sound playback fails
            _mediaPlayer?.Release();
            _mediaPlayer = null;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task VibrateAsync(TimeSpan duration)
    {
        try
        {
            // Get vibrator - use VibratorManager on Android 12+ (API 31), legacy Vibrator otherwise
            Vibrator? vibrator;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.S) // API 31 (Android 12)
            {
                var vibratorManager = _context.GetSystemService(Context.VibratorManagerService) as VibratorManager;
                vibrator = vibratorManager?.DefaultVibrator;
            }
            else
            {
#pragma warning disable CA1422 // Validate platform compatibility
                vibrator = _context.GetSystemService(Context.VibratorService) as Vibrator;
#pragma warning restore CA1422
            }

            if (vibrator == null || !vibrator.HasVibrator)
                return Task.CompletedTask;

            var durationMs = (long)duration.TotalMilliseconds;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                // Android 8.0+ (API 26): Use VibrationEffect
                var effect = VibrationEffect.CreateOneShot(durationMs, VibrationEffect.DefaultAmplitude);
                vibrator.Vibrate(effect);
            }
            else
            {
                // Legacy API (deprecated but needed for older devices)
#pragma warning disable CS0618 // Type or member is obsolete
#pragma warning disable CA1422 // Validate platform compatibility
                vibrator.Vibrate(durationMs);
#pragma warning restore CA1422 // Validate platform compatibility
#pragma warning restore CS0618
            }
        }
        catch (Exception)
        {
            // Silently fail if vibration fails (e.g., permission denied)
        }

        return Task.CompletedTask;
    }
}
