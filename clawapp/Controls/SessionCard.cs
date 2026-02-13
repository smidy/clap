using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Metadata;

namespace clawapp.Controls;

/// <summary>
/// A lookless control for displaying a session item in the session list with theme-aware styling.
/// </summary>
public class SessionCard : TemplatedControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<SessionCard, string>(nameof(Title), defaultValue: string.Empty);

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<SessionCard, string?>(nameof(Subtitle));

    [Content]
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }
}
