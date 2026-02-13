using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace clawapp.Controls;

/// <summary>
/// A collapsible control for displaying assistant thinking/reasoning blocks.
/// Shows "Thinking..." when streaming, and the full text when complete.
/// </summary>
public class ThinkingBlockView : TemplatedControl
{
    public static readonly StyledProperty<string?> ThinkingTextProperty =
        AvaloniaProperty.Register<ThinkingBlockView, string?>(nameof(ThinkingText));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ThinkingBlockView, bool>(nameof(IsExpanded), defaultValue: false);

    public static readonly StyledProperty<bool> IsStreamingProperty =
        AvaloniaProperty.Register<ThinkingBlockView, bool>(nameof(IsStreaming), defaultValue: false);

    private Button? _toggleButton;

    /// <summary>
    /// The thinking/reasoning text content.
    /// </summary>
    public string? ThinkingText
    {
        get => GetValue(ThinkingTextProperty);
        set => SetValue(ThinkingTextProperty, value);
    }

    /// <summary>
    /// Whether the thinking block is expanded to show content.
    /// </summary>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>
    /// Whether the thinking block is currently streaming (show "Thinking..." indicator).
    /// </summary>
    public bool IsStreaming
    {
        get => GetValue(IsStreamingProperty);
        set => SetValue(IsStreamingProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        // Unsubscribe from old button if exists
        if (_toggleButton != null)
        {
            _toggleButton.Click -= OnToggleButtonClick;
        }

        // Find and subscribe to new button
        _toggleButton = e.NameScope.Find<Button>("PART_ToggleButton");
        if (_toggleButton != null)
        {
            _toggleButton.Click += OnToggleButtonClick;
        }
    }

    private void OnToggleButtonClick(object? sender, RoutedEventArgs e)
    {
        // Don't toggle when streaming
        if (!IsStreaming)
        {
            // Set this message as selected before expanding to prevent scroll jumping to other selected items
            var listBox = this.FindAncestorOfType<ListBox>();
            if (listBox != null)
            {
                // Find the ChatBubble or ListBoxItem containing this thinking block
                var listBoxItem = this.FindAncestorOfType<ListBoxItem>();
                if (listBoxItem != null)
                {
                    // Set selection to this item's data context (the ChatMessage)
                    listBox.SelectedItem = listBoxItem.DataContext;
                }
            }
            
            IsExpanded = !IsExpanded;
        }
    }
}
