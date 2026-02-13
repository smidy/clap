using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace clawapp.Converters;

/// <summary>
/// Converts a boolean IsSupported value to a tooltip for the mic button.
/// True = "Record audio message", False = "Audio recording not supported (install ffmpeg)".
/// </summary>
public sealed class BoolToRecordingTooltipConverter : IValueConverter
{
    public static readonly BoolToRecordingTooltipConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSupported && isSupported)
            return "Record audio message";
        return "Audio recording not supported (install ffmpeg)";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
