using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;

namespace clawapp.Converters;

/// <summary>
/// Converts a boolean to ScrollBarVisibility value.
/// When word wrap is enabled (true), horizontal scrolling should be disabled.
/// When word wrap is disabled (false), horizontal scrolling should be enabled.
/// </summary>
public sealed class BoolToScrollBarVisibilityConverter : IValueConverter
{
    public static readonly BoolToScrollBarVisibilityConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // When IsWordWrapEnabled = true, return Disabled (no horizontal scroll)
        // When IsWordWrapEnabled = false, return Auto (allow horizontal scroll)
        if (value is bool isWordWrapEnabled && isWordWrapEnabled)
            return ScrollBarVisibility.Disabled;
        return ScrollBarVisibility.Auto;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
