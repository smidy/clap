using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace clawapp.Services;

/// <summary>
/// Base audio recording service with a "not supported" stub implementation.
/// Platform-specific projects override this with native implementations.
/// </summary>
public class AudioRecorderService : IAudioRecorderService, IDisposable
{
    private readonly System.Timers.Timer _durationTimer;
    private DateTime _recordingStartTime;
    private bool _disposed;

    public virtual bool IsRecording { get; protected set; }
    public virtual bool IsSupported => false;
    public TimeSpan CurrentDuration { get; protected set; }

    public event EventHandler<TimeSpan>? DurationUpdated;

    public AudioRecorderService()
    {
        _durationTimer = new System.Timers.Timer(100); // Update every 100ms
        _durationTimer.Elapsed += OnDurationTimerElapsed;
    }

    private void OnDurationTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (IsRecording)
        {
            CurrentDuration = DateTime.UtcNow - _recordingStartTime;
            RaiseDurationUpdated(CurrentDuration);
        }
    }

    protected void RaiseDurationUpdated(TimeSpan duration)
    {
        DurationUpdated?.Invoke(this, duration);
    }

    protected void StartDurationTimer()
    {
        _recordingStartTime = DateTime.UtcNow;
        CurrentDuration = TimeSpan.Zero;
        _durationTimer.Start();
    }

    protected void StopDurationTimer()
    {
        _durationTimer.Stop();
    }

    protected TimeSpan GetFinalDuration()
    {
        return CurrentDuration;
    }

    public virtual Task StartRecordingAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Audio recording is not supported on this platform.");
    }

    public virtual Task<AudioRecordingResult> StopRecordingAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Audio recording is not supported on this platform.");
    }

    public virtual void CancelRecording()
    {
        // No-op for stub implementation
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                CancelRecording();
                _durationTimer.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
