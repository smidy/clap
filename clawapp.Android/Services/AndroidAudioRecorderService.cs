using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Android.Content;
using Android.Media;
using Android.OS;
using clawapp.Services;

namespace clawapp.Android.Services;

/// <summary>
/// Android audio recording service using MediaRecorder.
/// Records as OGG/Opus at 16 kHz on Android 10+ (for Speech-to-Text); falls back to M4A (AAC) on older devices.
/// </summary>
public sealed class AndroidAudioRecorderService : AudioRecorderService
{
    private MediaRecorder? _mediaRecorder;
    private string? _tempFilePath;
    private bool _useOggOpus; // true if current recording is OGG/Opus (Android 10+)

    public override bool IsRecording { get; protected set; }
    public override bool IsSupported => true; // Android always supports audio recording

    private static bool SupportsOggOpus => Build.VERSION.SdkInt >= BuildVersionCodes.Q; // Android 10 / API 29

    public override Task StartRecordingAsync(CancellationToken cancellationToken = default)
    {
        if (IsRecording)
            throw new InvalidOperationException("Recording is already in progress.");

        var context = global::Android.App.Application.Context;
        var tempDir = context.CacheDir?.AbsolutePath ?? Path.GetTempPath();
        _useOggOpus = SupportsOggOpus;
        var extension = _useOggOpus ? "ogg" : "m4a";
        _tempFilePath = Path.Combine(tempDir, $"clawapp_recording_{Guid.NewGuid():N}.{extension}");

        try
        {
            _mediaRecorder = Build.VERSION.SdkInt >= BuildVersionCodes.S
                ? CreateModernMediaRecorder(context)
                : CreateLegacyMediaRecorder();
            _mediaRecorder.SetAudioSource(AudioSource.Mic);

            if (_useOggOpus)
            {
                _mediaRecorder.SetOutputFormat(OutputFormat.Ogg);
                _mediaRecorder.SetAudioEncoder(AudioEncoder.Opus);
                _mediaRecorder.SetAudioSamplingRate(16000); // Recommended for Speech-to-Text
            }
            else
            {
                _mediaRecorder.SetOutputFormat(OutputFormat.Mpeg4);
                _mediaRecorder.SetAudioEncoder(AudioEncoder.Aac);
                _mediaRecorder.SetAudioEncodingBitRate(128000);
                _mediaRecorder.SetAudioSamplingRate(44100);
            }

            _mediaRecorder.SetOutputFile(_tempFilePath);

            _mediaRecorder.Prepare();
            _mediaRecorder.Start();

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
        if (!IsRecording || _mediaRecorder == null)
            throw new InvalidOperationException("No recording is in progress.");

        StopDurationTimer();
        var duration = GetFinalDuration();
        IsRecording = false;

        try
        {
            _mediaRecorder.Stop();
            _mediaRecorder.Release();
        }
        finally
        {
            _mediaRecorder = null;
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
            _mediaRecorder?.Stop();
        }
        catch
        {
            // Ignore
        }

        try
        {
            _mediaRecorder?.Release();
        }
        catch
        {
            // Ignore
        }
        finally
        {
            _mediaRecorder = null;
        }
    }

#pragma warning disable CA1416 // Platform compatibility is handled at runtime with version check
    private static MediaRecorder CreateModernMediaRecorder(Context context) => new MediaRecorder(context);
#pragma warning restore CA1416

#pragma warning disable CA1422 // Obsolete API usage is handled at runtime with version check
    private static MediaRecorder CreateLegacyMediaRecorder() => new MediaRecorder();
#pragma warning restore CA1422

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CancelRecording();
        }

        base.Dispose(disposing);
    }
}
