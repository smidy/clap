using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace clawapp.Converters;

public sealed class ChatBubbleBrushConverter : IValueConverter
{
    public static readonly ChatBubbleBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // User messages: accent; assistant: subtle gray
        if (value is true) // user
            return new SolidColorBrush(Color.FromRgb(0x0d, 0x47, 0xa1)); // blue-ish
        return new SolidColorBrush(Color.FromRgb(0x37, 0x37, 0x38)); // dark gray
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
