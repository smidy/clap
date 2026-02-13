using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace clawapp.Controls;

/// <summary>
/// Button control for audio recording with state indicators.
/// Shows recording indicator (red dot, timer) when recording.
/// </summary>
public class AudioRecorderButton : TemplatedControl
{
    /// <summary>
    /// Whether recording is currently in progress.
    /// </summary>
    public static readonly StyledProperty<bool> IsRecordingProperty =
        AvaloniaProperty.Register<AudioRecorderButton, bool>(nameof(IsRecording));

    public bool IsRecording
    {
        get => GetValue(IsRecordingProperty);
        set => SetValue(IsRecordingProperty, value);
    }

    /// <summary>
    /// Whether audio recording is supported on this platform.
    /// </summary>
    public static readonly StyledProperty<bool> IsSupportedProperty =
        AvaloniaProperty.Register<AudioRecorderButton, bool>(nameof(IsSupported), defaultValue: true);

    public bool IsSupported
    {
        get => GetValue(IsSupportedProperty);
        set => SetValue(IsSupportedProperty, value);
    }

    /// <summary>
    /// Current recording duration.
    /// </summary>
    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<AudioRecorderButton, TimeSpan>(nameof(Duration));

    public TimeSpan Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <summary>
    /// Formatted duration string (e.g., "00:15").
    /// </summary>
    public static readonly DirectProperty<AudioRecorderButton, string> DurationTextProperty =
        AvaloniaProperty.RegisterDirect<AudioRecorderButton, string>(
            nameof(DurationText),
            o => o.DurationText);

    private string _durationText = "00:00";
    public string DurationText
    {
        get => _durationText;
        private set => SetAndRaise(DurationTextProperty, ref _durationText, value);
    }

    /// <summary>
    /// Command to execute when the record button is clicked.
    /// </summary>
    public static readonly StyledProperty<ICommand?> RecordCommandProperty =
        AvaloniaProperty.Register<AudioRecorderButton, ICommand?>(nameof(RecordCommand));

    public ICommand? RecordCommand
    {
        get => GetValue(RecordCommandProperty);
        set => SetValue(RecordCommandProperty, value);
    }

    /// <summary>
    /// Command to execute when the cancel button is clicked during recording.
    /// </summary>
    public static readonly StyledProperty<ICommand?> CancelCommandProperty =
        AvaloniaProperty.Register<AudioRecorderButton, ICommand?>(nameof(CancelCommand));

    public ICommand? CancelCommand
    {
        get => GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }

    /// <summary>
    /// Command to execute when the send button is clicked after recording.
    /// </summary>
    public static readonly StyledProperty<ICommand?> SendCommandProperty =
        AvaloniaProperty.Register<AudioRecorderButton, ICommand?>(nameof(SendCommand));

    public ICommand? SendCommand
    {
        get => GetValue(SendCommandProperty);
        set => SetValue(SendCommandProperty, value);
    }

    /// <summary>
    /// Tooltip text for the button.
    /// </summary>
    public static readonly DirectProperty<AudioRecorderButton, string> TooltipTextProperty =
        AvaloniaProperty.RegisterDirect<AudioRecorderButton, string>(
            nameof(TooltipText),
            o => o.TooltipText);

    private string _tooltipText = "Record audio message";
    public string TooltipText
    {
        get => _tooltipText;
        private set => SetAndRaise(TooltipTextProperty, ref _tooltipText, value);
    }

    static AudioRecorderButton()
    {
        DurationProperty.Changed.AddClassHandler<AudioRecorderButton>((x, _) => x.UpdateDurationText());
        IsRecordingProperty.Changed.AddClassHandler<AudioRecorderButton>((x, _) => x.UpdateTooltip());
        IsSupportedProperty.Changed.AddClassHandler<AudioRecorderButton>((x, _) => x.UpdateTooltip());
    }

    private void UpdateDurationText()
    {
        var duration = Duration;
        DurationText = $"{(int)duration.TotalMinutes:D2}:{duration.Seconds:D2}";
    }

    private void UpdateTooltip()
    {
        if (!IsSupported)
            TooltipText = "Audio recording not supported (install ffmpeg)";
        else if (IsRecording)
            TooltipText = "Click to stop recording";
        else
            TooltipText = "Record audio message";
    }
}
