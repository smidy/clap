using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using clawapp.Models;

namespace clawapp.Converters;

/// <summary>
/// Converts a List<ContentBlock> to plain text for display.
/// </summary>
public sealed class ContentBlocksToTextConverter : IValueConverter
{
    public static readonly ContentBlocksToTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is List<ContentBlock> contentBlocks)
        {
            return string.Join("\n", contentBlocks
                .Where(c => c.Type == "text" && !string.IsNullOrEmpty(c.Text))
                .Select(c => c.Text));
        }
        return value?.ToString();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
