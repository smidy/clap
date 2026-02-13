using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace clawapp.Converters;

/// <summary>
/// Converts a boolean IsRecording value to a microphone icon.
/// True = ⏹ (stop), False = 🎤 (mic).
/// </summary>
public sealed class BoolToRecordingIconConverter : IValueConverter
{
    public static readonly BoolToRecordingIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isRecording && isRecording)
            return "⏹";
        return "🎤";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
