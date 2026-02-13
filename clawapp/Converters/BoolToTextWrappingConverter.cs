using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace clawapp.Converters;

/// <summary>
/// Converts a boolean to TextWrapping value.
/// True = Wrap, False = NoWrap.
/// </summary>
public sealed class BoolToTextWrappingConverter : IValueConverter
{
    public static readonly BoolToTextWrappingConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isWordWrapEnabled && isWordWrapEnabled)
            return TextWrapping.Wrap;
        return TextWrapping.NoWrap;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
