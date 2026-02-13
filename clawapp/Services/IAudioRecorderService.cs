using System;
using System.Threading;
using System.Threading.Tasks;

namespace clawapp.Services;

/// <summary>
/// Service for recording audio from the microphone.
/// </summary>
public interface IAudioRecorderService
{
    /// <summary>
    /// Whether recording is currently in progress.
    /// </summary>
    bool IsRecording { get; }
    
    /// <summary>
    /// Whether audio recording is supported on this platform.
    /// </summary>
    bool IsSupported { get; }
    
    /// <summary>
    /// Current recording duration (updated periodically while recording).
    /// </summary>
    TimeSpan CurrentDuration { get; }
    
    /// <summary>
    /// Fired periodically during recording with the current duration.
    /// </summary>
    event EventHandler<TimeSpan>? DurationUpdated;
    
    /// <summary>
    /// Starts recording audio from the default microphone.
    /// </summary>
    Task StartRecordingAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Stops recording and returns the recorded audio data.
    /// </summary>
    Task<AudioRecordingResult> StopRecordingAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Cancels the current recording without returning data.
    /// </summary>
    void CancelRecording();
}

/// <summary>
/// Result of an audio recording operation.
/// </summary>
public record AudioRecordingResult
{
    /// <summary>
    /// The recorded audio data (PCM or encoded format).
    /// </summary>
    public byte[] Data { get; init; } = Array.Empty<byte>();
    
    /// <summary>
    /// MIME type of the audio data (e.g., "audio/wav", "audio/webm").
    /// </summary>
    public string MimeType { get; init; } = "audio/wav";
    
    /// <summary>
    /// Duration of the recording.
    /// </summary>
    public TimeSpan Duration { get; init; }
    
    /// <summary>
    /// Suggested file name for the recording.
    /// </summary>
    public string FileName => $"recording_{DateTime.Now:yyyyMMdd_HHmmss}.{GetExtension()}";
    
    private string GetExtension() => MimeType switch
    {
        "audio/wav" => "wav",
        "audio/webm" => "webm",
        "audio/ogg" => "ogg",
        "audio/mpeg" => "mp3",
        "audio/mp4" => "m4a",
        _ => "bin"
    };
}
