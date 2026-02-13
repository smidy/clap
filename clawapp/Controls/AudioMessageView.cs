using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace clawapp.Controls;

/// <summary>
/// A control for displaying and playing audio messages.
/// </summary>
public class AudioMessageView : TemplatedControl
{
    public static readonly StyledProperty<string?> AudioIdProperty =
        AvaloniaProperty.Register<AudioMessageView, string?>(nameof(AudioId));

    public static readonly StyledProperty<string?> MimeTypeProperty =
        AvaloniaProperty.Register<AudioMessageView, string?>(nameof(MimeType));

    public static readonly StyledProperty<object?> AudioDataProperty =
        AvaloniaProperty.Register<AudioMessageView, object?>(nameof(AudioData));

    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<AudioMessageView, TimeSpan>(nameof(Duration));

    public static readonly StyledProperty<TimeSpan> CurrentPositionProperty =
        AvaloniaProperty.Register<AudioMessageView, TimeSpan>(nameof(CurrentPosition));

    public static readonly StyledProperty<AudioPlaybackState> PlaybackStateProperty =
        AvaloniaProperty.Register<AudioMessageView, AudioPlaybackState>(nameof(PlaybackState), defaultValue: AudioPlaybackState.Stopped);

    public static readonly StyledProperty<string?> TranscriptProperty =
        AvaloniaProperty.Register<AudioMessageView, string?>(nameof(Transcript));

    public static readonly StyledProperty<bool> ShowTranscriptProperty =
        AvaloniaProperty.Register<AudioMessageView, bool>(nameof(ShowTranscript), defaultValue: false);

    public static readonly DirectProperty<AudioMessageView, string> DurationDisplayProperty =
        AvaloniaProperty.RegisterDirect<AudioMessageView, string>(
            nameof(DurationDisplay),
            o => o.DurationDisplay);

    public static readonly DirectProperty<AudioMessageView, string> CurrentPositionDisplayProperty =
        AvaloniaProperty.RegisterDirect<AudioMessageView, string>(
            nameof(CurrentPositionDisplay),
            o => o.CurrentPositionDisplay);

    public static readonly DirectProperty<AudioMessageView, double> ProgressPercentProperty =
        AvaloniaProperty.RegisterDirect<AudioMessageView, double>(
            nameof(ProgressPercent),
            o => o.ProgressPercent);

    public static readonly DirectProperty<AudioMessageView, string> PlayPauseIconProperty =
        AvaloniaProperty.RegisterDirect<AudioMessageView, string>(
            nameof(PlayPauseIcon),
            o => o.PlayPauseIcon);

    private Button? _playPauseButton;
    private Slider? _progressSlider;
    private Button? _transcriptToggleButton;
    private string _durationDisplay = "0:00";
    private string _currentPositionDisplay = "0:00";
    private double _progressPercent;
    private string _playPauseIcon = "▶";
    private bool _isDraggingSlider;

    static AudioMessageView()
    {
        DurationProperty.Changed.AddClassHandler<AudioMessageView>((x, _) => x.UpdateDurationDisplay());
        CurrentPositionProperty.Changed.AddClassHandler<AudioMessageView>((x, _) => x.UpdateCurrentPositionDisplay());
        CurrentPositionProperty.Changed.AddClassHandler<AudioMessageView>((x, _) => x.UpdateProgressPercent());
        DurationProperty.Changed.AddClassHandler<AudioMessageView>((x, _) => x.UpdateProgressPercent());
        PlaybackStateProperty.Changed.AddClassHandler<AudioMessageView>((x, _) => x.UpdatePlayPauseIcon());
        TranscriptProperty.Changed.AddClassHandler<AudioMessageView>((x, _) => x.UpdateTranscriptToggleVisibility());
    }

    /// <summary>
    /// Unique identifier for this audio message.
    /// </summary>
    public string? AudioId
    {
        get => GetValue(AudioIdProperty);
        set => SetValue(AudioIdProperty, value);
    }

    /// <summary>
    /// MIME type of the audio (e.g., "audio/ogg", "audio/wav").
    /// </summary>
    public string? MimeType
    {
        get => GetValue(MimeTypeProperty);
        set => SetValue(MimeTypeProperty, value);
    }

    /// <summary>
    /// Audio data (base64 string, URL, or byte array).
    /// </summary>
    public object? AudioData
    {
        get => GetValue(AudioDataProperty);
        set => SetValue(AudioDataProperty, value);
    }

    /// <summary>
    /// Total duration of the audio.
    /// </summary>
    public TimeSpan Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <summary>
    /// Current playback position.
    /// </summary>
    public TimeSpan CurrentPosition
    {
        get => GetValue(CurrentPositionProperty);
        set => SetValue(CurrentPositionProperty, value);
    }

    /// <summary>
    /// Current playback state.
    /// </summary>
    public AudioPlaybackState PlaybackState
    {
        get => GetValue(PlaybackStateProperty);
        set => SetValue(PlaybackStateProperty, value);
    }

    /// <summary>
    /// Transcript text (if available from transcription).
    /// </summary>
    public string? Transcript
    {
        get => GetValue(TranscriptProperty);
        set => SetValue(TranscriptProperty, value);
    }

    /// <summary>
    /// Whether to show the transcript section.
    /// </summary>
    public bool ShowTranscript
    {
        get => GetValue(ShowTranscriptProperty);
        set => SetValue(ShowTranscriptProperty, value);
    }

    /// <summary>
    /// Formatted duration string (e.g., "1:23").
    /// </summary>
    public string DurationDisplay
    {
        get => _durationDisplay;
        private set => SetAndRaise(DurationDisplayProperty, ref _durationDisplay, value);
    }

    /// <summary>
    /// Formatted current position string (e.g., "0:45").
    /// </summary>
    public string CurrentPositionDisplay
    {
        get => _currentPositionDisplay;
        private set => SetAndRaise(CurrentPositionDisplayProperty, ref _currentPositionDisplay, value);
    }

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetAndRaise(ProgressPercentProperty, ref _progressPercent, value);
    }

    /// <summary>
    /// Play/pause button icon.
    /// </summary>
    public string PlayPauseIcon
    {
        get => _playPauseIcon;
        private set => SetAndRaise(PlayPauseIconProperty, ref _playPauseIcon, value);
    }

    /// <summary>
    /// Event raised when play/pause is requested.
    /// </summary>
    public event EventHandler<AudioPlaybackEventArgs>? PlaybackRequested;

    /// <summary>
    /// Event raised when seeking to a position.
    /// </summary>
    public event EventHandler<AudioSeekEventArgs>? SeekRequested;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        // Unsubscribe from old controls
        if (_playPauseButton != null)
            _playPauseButton.Click -= OnPlayPauseClick;
        if (_progressSlider != null)
        {
            _progressSlider.ValueChanged -= OnSliderValueChanged;
            _progressSlider.PointerPressed -= OnSliderPointerPressed;
            _progressSlider.PointerReleased -= OnSliderPointerReleased;
        }
        if (_transcriptToggleButton != null)
            _transcriptToggleButton.Click -= OnTranscriptToggleClick;

        // Find and subscribe to new controls
        _playPauseButton = e.NameScope.Find<Button>("PART_PlayPauseButton");
        if (_playPauseButton != null)
            _playPauseButton.Click += OnPlayPauseClick;

        _progressSlider = e.NameScope.Find<Slider>("PART_ProgressSlider");
        if (_progressSlider != null)
        {
            _progressSlider.ValueChanged += OnSliderValueChanged;
            _progressSlider.PointerPressed += OnSliderPointerPressed;
            _progressSlider.PointerReleased += OnSliderPointerReleased;
        }

        _transcriptToggleButton = e.NameScope.Find<Button>("PART_TranscriptToggleButton");
        if (_transcriptToggleButton != null)
            _transcriptToggleButton.Click += OnTranscriptToggleClick;

        UpdateDurationDisplay();
        UpdateCurrentPositionDisplay();
        UpdateProgressPercent();
        UpdatePlayPauseIcon();
        UpdateTranscriptToggleVisibility();
    }

    private void OnPlayPauseClick(object? sender, RoutedEventArgs e)
    {
        var requestedState = PlaybackState == AudioPlaybackState.Playing
            ? AudioPlaybackState.Paused
            : AudioPlaybackState.Playing;

        PlaybackRequested?.Invoke(this, new AudioPlaybackEventArgs(requestedState));

        // Update state locally if no external handler
        if (PlaybackRequested == null)
        {
            PlaybackState = requestedState;
        }
    }

    private void OnSliderPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        _isDraggingSlider = true;
    }

    private void OnSliderPointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        _isDraggingSlider = false;
        if (_progressSlider != null && Duration.TotalSeconds > 0)
        {
            var seekPosition = TimeSpan.FromSeconds(_progressSlider.Value / 100 * Duration.TotalSeconds);
            SeekRequested?.Invoke(this, new AudioSeekEventArgs(seekPosition));
        }
    }

    private void OnSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        // Update current position display while dragging
        if (_isDraggingSlider && Duration.TotalSeconds > 0)
        {
            var position = TimeSpan.FromSeconds(e.NewValue / 100 * Duration.TotalSeconds);
            CurrentPositionDisplay = FormatTimeSpan(position);
        }
    }

    private void OnTranscriptToggleClick(object? sender, RoutedEventArgs e)
    {
        ShowTranscript = !ShowTranscript;
    }

    private void UpdateDurationDisplay()
    {
        DurationDisplay = FormatTimeSpan(Duration);
    }

    private void UpdateCurrentPositionDisplay()
    {
        if (!_isDraggingSlider)
        {
            CurrentPositionDisplay = FormatTimeSpan(CurrentPosition);
        }
    }

    private void UpdateProgressPercent()
    {
        if (!_isDraggingSlider)
        {
            ProgressPercent = Duration.TotalSeconds > 0
                ? CurrentPosition.TotalSeconds / Duration.TotalSeconds * 100
                : 0;
        }
    }

    private void UpdatePlayPauseIcon()
    {
        PlayPauseIcon = PlaybackState == AudioPlaybackState.Playing ? "⏸" : "▶";
    }

    private void UpdateTranscriptToggleVisibility()
    {
        if (_transcriptToggleButton != null)
        {
            _transcriptToggleButton.IsVisible = !string.IsNullOrEmpty(Transcript);
        }
    }

    private static string FormatTimeSpan(TimeSpan time)
    {
        if (time.TotalHours >= 1)
            return $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}";
        return $"{(int)time.TotalMinutes}:{time.Seconds:D2}";
    }
}

/// <summary>
/// Audio playback states.
/// </summary>
public enum AudioPlaybackState
{
    Stopped,
    Playing,
    Paused,
    Loading
}

/// <summary>
/// Event args for playback state change requests.
/// </summary>
public class AudioPlaybackEventArgs : EventArgs
{
    public AudioPlaybackState RequestedState { get; }

    public AudioPlaybackEventArgs(AudioPlaybackState requestedState)
    {
        RequestedState = requestedState;
    }
}

/// <summary>
/// Event args for seek requests.
/// </summary>
public class AudioSeekEventArgs : EventArgs
{
    public TimeSpan Position { get; }

    public AudioSeekEventArgs(TimeSpan position)
    {
        Position = position;
    }
}
