using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Layout;

namespace clawapp.Converters;

public sealed class ChatBubbleAlignmentConverter : IValueConverter
{
    public static readonly ChatBubbleAlignmentConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is HorizontalAlignment.Right;
    }
}
