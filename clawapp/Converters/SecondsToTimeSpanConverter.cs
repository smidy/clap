using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace clawapp.Converters;

/// <summary>
/// Converts a double representing seconds to a TimeSpan.
/// </summary>
public sealed class SecondsToTimeSpanConverter : IValueConverter
{
    public static readonly SecondsToTimeSpanConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double seconds && seconds > 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return TimeSpan.Zero;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TimeSpan timeSpan)
        {
            return timeSpan.TotalSeconds;
        }

        return 0.0;
    }
}
