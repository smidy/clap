using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace clawapp.Converters;

/// <summary>
/// Converts Unix timestamp (ms) to a short display string (e.g. "14:32" or "Yesterday").
/// </summary>
public sealed class UnixTimestampToDisplayConverter : IValueConverter
{
    public static readonly UnixTimestampToDisplayConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long ms)
            return null;
        var dt = DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime;
        var now = DateTime.Now;
        if (dt.Date == now.Date)
            return dt.ToString("HH:mm", culture);
        if (dt.Date == now.Date.AddDays(-1))
            return "Yesterday";
        if (dt.Year == now.Year)
            return dt.ToString("MMM d", culture);
        return dt.ToString("MMM d, yyyy", culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
