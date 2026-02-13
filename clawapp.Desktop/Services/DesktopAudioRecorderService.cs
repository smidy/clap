using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Concentus;
using Concentus.Enums;
using Concentus.Oggfile;
using Concentus.Structs;
using clawapp.Services;
using PortAudioSharp;

namespace clawapp.Desktop.Services;

/// <summary>
/// Desktop audio recording service using PortAudioSharp2.
/// Records from the default input device and outputs OGG/Opus at 16 kHz for Speech-to-Text.
/// </summary>
public sealed class DesktopAudioRecorderService : AudioRecorderService
{
    private const int TargetSampleRate = 16000;
    private const int OpusBitrate = 32000;

    private PortAudioSharp.Stream? _stream;
    private readonly List<float> _recordedSamples = new();
    private readonly object _samplesLock = new();
    private bool _isInitialized;
    private int _sampleRate = 44100;
    private int _channels = 1;

    public override bool IsRecording { get; protected set; }
    public override bool IsSupported => CheckSupport();

    public DesktopAudioRecorderService()
    {
        TryInitialize();
    }

    private void TryInitialize()
    {
        if (_isInitialized) return;

        try
        {
            PortAudio.Initialize();
            _isInitialized = true;
        }
        catch (Exception)
        {
            // PortAudio not available on this system
            _isInitialized = false;
        }
    }

    private bool CheckSupport()
    {
        if (!_isInitialized) return false;

        try
        {
            var deviceIndex = PortAudio.DefaultInputDevice;
            return deviceIndex != PortAudio.NoDevice;
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
            throw new NotSupportedException("Audio recording is not supported on this platform.");

        TryInitialize();

        var deviceIndex = PortAudio.DefaultInputDevice;
        if (deviceIndex == PortAudio.NoDevice)
            throw new InvalidOperationException("No default input device found.");

        var deviceInfo = PortAudio.GetDeviceInfo(deviceIndex);
        _sampleRate = (int)deviceInfo.defaultSampleRate;
        if (_sampleRate <= 0) _sampleRate = 44100;

        lock (_samplesLock)
        {
            _recordedSamples.Clear();
        }

        var param = new StreamParameters
        {
            device = deviceIndex,
            channelCount = _channels,
            sampleFormat = SampleFormat.Float32,
            suggestedLatency = deviceInfo.defaultLowInputLatency,
            hostApiSpecificStreamInfo = IntPtr.Zero
        };

        PortAudioSharp.Stream.Callback callback = (IntPtr input, IntPtr output,
            uint frameCount,
            ref StreamCallbackTimeInfo timeInfo,
            StreamCallbackFlags statusFlags,
            IntPtr userData) =>
        {
            if (!IsRecording) return StreamCallbackResult.Complete;

            var samples = new float[frameCount];
            Marshal.Copy(input, samples, 0, (int)frameCount);

            lock (_samplesLock)
            {
                _recordedSamples.AddRange(samples);
            }

            return StreamCallbackResult.Continue;
        };

        _stream = new PortAudioSharp.Stream(
            inParams: param,
            outParams: null,
            sampleRate: _sampleRate,
            framesPerBuffer: 0,
            streamFlags: StreamFlags.ClipOff,
            callback: callback,
            userData: IntPtr.Zero
        );

        _stream.Start();
        IsRecording = true;
        StartDurationTimer();

        return Task.CompletedTask;
    }

    public override async Task<AudioRecordingResult> StopRecordingAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRecording || _stream == null)
            throw new InvalidOperationException("No recording is in progress.");

        StopDurationTimer();
        var duration = GetFinalDuration();
        IsRecording = false;

        try
        {
            _stream.Stop();
            _stream.Dispose();
        }
        catch
        {
            // Ignore errors during stop
        }
        finally
        {
            _stream = null;
        }

        float[] samples;
        lock (_samplesLock)
        {
            samples = _recordedSamples.ToArray();
            _recordedSamples.Clear();
        }

        var oggData = await Task.Run(() => ConvertToOggOpus(samples, _sampleRate, _channels), cancellationToken);

        return new AudioRecordingResult
        {
            Data = oggData,
            MimeType = "audio/ogg",
            Duration = duration
        };
    }

    public override void CancelRecording()
    {
        if (!IsRecording) return;

        StopDurationTimer();
        IsRecording = false;

        try
        {
            _stream?.Stop();
            _stream?.Dispose();
        }
        catch
        {
            // Ignore errors during cancellation
        }
        finally
        {
            _stream = null;
        }

        lock (_samplesLock)
        {
            _recordedSamples.Clear();
        }
    }

    /// <summary>
    /// Converts float samples (-1.0 to 1.0) to OGG/Opus at 16 kHz. Resamples if capture rate differs.
    /// </summary>
    private static byte[] ConvertToOggOpus(float[] samples, int sampleRate, int channels)
    {
        var encoder = CreateOpusEncoder(TargetSampleRate, channels);
        encoder.Bitrate = OpusBitrate;

        using var memoryStream = new MemoryStream();
        var oggStream = new OpusOggWriteStream(
            encoder,
            memoryStream,
            fileTags: null,
            inputSampleRate: sampleRate,
            resamplerQuality: 5,
            leaveOpen: true);

        oggStream.WriteSamples(samples, 0, samples.Length);
        oggStream.Finish();

        return memoryStream.ToArray();
    }

    private static IOpusEncoder CreateOpusEncoder(int sampleRate, int channels)
    {
        return OpusCodecFactory.CreateEncoder(sampleRate, channels, OpusApplication.OPUS_APPLICATION_AUDIO);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CancelRecording();
            
            if (_isInitialized)
            {
                try
                {
                    PortAudio.Terminate();
                }
                catch
                {
                    // Ignore termination errors
                }
            }
        }

        base.Dispose(disposing);
    }
}
