using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AudioToolbox;
using AVFoundation;
using clawapp.Services;
using Foundation;

namespace clawapp.iOS.Services;

/// <summary>
/// iOS audio recording service using AVAudioRecorder.
/// Records as OGG/Opus at 16 kHz (for Speech-to-Text); falls back to M4A (AAC) if Opus is unavailable.
/// </summary>
public sealed class iOSAudioRecorderService : AudioRecorderService
{
    /// <summary>Apple four-char code for Opus: 'opus' = 0x6f707573.</summary>
    private const int OpusFormatId = 0x6f707573;

    private AVAudioRecorder? _audioRecorder;
    private string? _tempFilePath;
    private bool _useOggOpus;

    public override bool IsRecording { get; protected set; }
    public override bool IsSupported => CheckSupport();

    private static bool CheckSupport()
    {
        try
        {
            // Check if microphone permission can be requested
            var session = AVAudioSession.SharedInstance();
            return session != null;
        }
        catch
        {
            return false;
        }
    }

    public override Task StartRecordingAsync(CancellationToken cancellationToken = default)
    {
        if (IsRecording)
            throw new InvalidOperationException("Recording is already in progress.");

        if (!IsSupported)
            throw new NotSupportedException("Audio recording is not supported on this device.");

        try
        {
            // Configure audio session
            var session = AVAudioSession.SharedInstance();
            var error = session.SetCategory(AVAudioSessionCategory.PlayAndRecord, AVAudioSessionCategoryOptions.DefaultToSpeaker);
            if (error != null)
                throw new InvalidOperationException($"Failed to set audio session category: {error.LocalizedDescription}");

            error = session.SetActive(true);
            if (error != null)
                throw new InvalidOperationException($"Failed to activate audio session: {error.LocalizedDescription}");

            // Prefer OGG/Opus at 16 kHz for Speech-to-Text; fall back to M4A (AAC) if Opus fails
            var tempDir = Path.GetTempPath();
            _useOggOpus = true;
            _tempFilePath = Path.Combine(tempDir, $"clawapp_recording_{Guid.NewGuid():N}.ogg");
            var url = NSUrl.FromFilename(_tempFilePath);

            var opusSettings = new AudioSettings
            {
                Format = (AudioFormatType)OpusFormatId,
                SampleRate = 16000,
                NumberChannels = 1,
                EncoderBitRate = 32000,
                AudioQuality = AVAudioQuality.High
            };

            _audioRecorder = AVAudioRecorder.Create(url, opusSettings, out error);
            if (_audioRecorder == null || error != null)
            {
                _useOggOpus = false;
                _tempFilePath = Path.Combine(tempDir, $"clawapp_recording_{Guid.NewGuid():N}.m4a");
                url = NSUrl.FromFilename(_tempFilePath);
                var aacSettings = new AudioSettings
                {
                    Format = AudioFormatType.MPEG4AAC,
                    SampleRate = 44100,
                    NumberChannels = 1,
                    EncoderBitRate = 128000,
                    AudioQuality = AVAudioQuality.High
                };
                _audioRecorder = AVAudioRecorder.Create(url, aacSettings, out error);
            }

            if (_audioRecorder == null || error != null)
                throw new InvalidOperationException($"Failed to create audio recorder: {error?.LocalizedDescription ?? "Unknown error"}");

            _audioRecorder.PrepareToRecord();
            
            if (!_audioRecorder.Record())
                throw new InvalidOperationException("Failed to start recording.");

            IsRecording = true;
            StartDurationTimer();

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            CleanupRecorder();
            throw new InvalidOperationException($"Failed to start recording: {ex.Message}", ex);
        }
    }

    public override async Task<AudioRecordingResult> StopRecordingAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRecording || _audioRecorder == null)
            throw new InvalidOperationException("No recording is in progress.");

        StopDurationTimer();
        var duration = GetFinalDuration();
        IsRecording = false;

        try
        {
            _audioRecorder.Stop();
            _audioRecorder.Dispose();
        }
        catch
        {
            // Ignore errors during stop
        }
        finally
        {
            _audioRecorder = null;
        }

        // Deactivate audio session
        try
        {
            AVAudioSession.SharedInstance().SetActive(false);
        }
        catch
        {
            // Ignore
        }

        // Read the recorded file
        if (!string.IsNullOrEmpty(_tempFilePath) && File.Exists(_tempFilePath))
        {
            var data = await File.ReadAllBytesAsync(_tempFilePath, cancellationToken);

            // Clean up temp file
            try { File.Delete(_tempFilePath); } catch { }
            _tempFilePath = null;

            var mimeType = _useOggOpus ? "audio/ogg" : "audio/mp4";
            return new AudioRecordingResult
            {
                Data = data,
                MimeType = mimeType,
                Duration = duration
            };
        }

        var fallbackMime = _useOggOpus ? "audio/ogg" : "audio/mp4";
        return new AudioRecordingResult
        {
            Data = Array.Empty<byte>(),
            MimeType = fallbackMime,
            Duration = TimeSpan.Zero
        };
    }

    public override void CancelRecording()
    {
        if (!IsRecording) return;

        StopDurationTimer();
        IsRecording = false;

        CleanupRecorder();

        // Clean up temp file
        if (!string.IsNullOrEmpty(_tempFilePath))
        {
            try { File.Delete(_tempFilePath); } catch { }
            _tempFilePath = null;
        }
    }

    private void CleanupRecorder()
    {
        try
        {
            _audioRecorder?.Stop();
        }
        catch
        {
            // Ignore
        }

        try
        {
            _audioRecorder?.Dispose();
        }
        catch
        {
            // Ignore
        }
        finally
        {
            _audioRecorder = null;
        }

        // Deactivate audio session
        try
        {
            AVAudioSession.SharedInstance().SetActive(false);
        }
        catch
        {
            // Ignore
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CancelRecording();
        }

        base.Dispose(disposing);
    }
}
