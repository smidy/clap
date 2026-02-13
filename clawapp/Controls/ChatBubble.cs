using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Metadata;
using clawapp.Models;

namespace clawapp.Controls;

/// <summary>
/// A lookless control for displaying a single chat message bubble with theme-aware styling.
/// </summary>
public class ChatBubble : TemplatedControl
{
    public static readonly StyledProperty<string> MessageProperty =
        AvaloniaProperty.Register<ChatBubble, string>(nameof(Message), defaultValue: string.Empty);

    public static readonly StyledProperty<ObservableCollection<ContentBlock>?> ContentBlocksProperty =
        AvaloniaProperty.Register<ChatBubble, ObservableCollection<ContentBlock>?>(nameof(ContentBlocks));

    public static readonly StyledProperty<bool> IsFromUserProperty =
        AvaloniaProperty.Register<ChatBubble, bool>(nameof(IsFromUser));

    public static readonly StyledProperty<string?> TimestampProperty =
        AvaloniaProperty.Register<ChatBubble, string?>(nameof(Timestamp));

    public static readonly StyledProperty<string?> RoleProperty =
        AvaloniaProperty.Register<ChatBubble, string?>(nameof(Role));
    
    public static readonly StyledProperty<bool> IsLatestMessageProperty =
        AvaloniaProperty.Register<ChatBubble, bool>(nameof(IsLatestMessage));
    
    public static readonly StyledProperty<bool> IsTruncatedProperty =
        AvaloniaProperty.Register<ChatBubble, bool>(nameof(IsTruncated));

    private ItemsControl? _contentPresenter;
    private TextBlock? _legacyMessage;
    private Border? _contentContainer;
    private StackPanel? _contentStackPanel;

    static ChatBubble()
    {
        ContentBlocksProperty.Changed.AddClassHandler<ChatBubble>((x, _) => x.UpdateContentVisibility());
        IsLatestMessageProperty.Changed.AddClassHandler<ChatBubble>((x, _) => x.CheckTruncation());
    }

    /// <summary>
    /// Plain text message content (legacy, for backward compatibility).
    /// </summary>
    [Content]
    public string Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>
    /// Structured content blocks for rich message rendering.
    /// When set, takes precedence over Message property.
    /// </summary>
    public ObservableCollection<ContentBlock>? ContentBlocks
    {
        get => GetValue(ContentBlocksProperty);
        set => SetValue(ContentBlocksProperty, value);
    }

    public bool IsFromUser
    {
        get => GetValue(IsFromUserProperty);
        set => SetValue(IsFromUserProperty, value);
    }

    public string? Timestamp
    {
        get => GetValue(TimestampProperty);
        set => SetValue(TimestampProperty, value);
    }

    public string? Role
    {
        get => GetValue(RoleProperty);
        set => SetValue(RoleProperty, value);
    }
    
    public bool IsLatestMessage
    {
        get => GetValue(IsLatestMessageProperty);
        set => SetValue(IsLatestMessageProperty, value);
    }
    
    public bool IsTruncated
    {
        get => GetValue(IsTruncatedProperty);
        set => SetValue(IsTruncatedProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        // Unsubscribe from old elements if any
        if (_contentStackPanel != null)
        {
            _contentStackPanel.SizeChanged -= OnContentSizeChanged;
        }

        _contentPresenter = e.NameScope.Find<ItemsControl>("PART_ContentPresenter");
        _legacyMessage = e.NameScope.Find<TextBlock>("PART_LegacyMessage");
        _contentContainer = e.NameScope.Find<Border>("PART_ContentContainer");
        _contentStackPanel = e.NameScope.Find<StackPanel>("PART_ContentStackPanel");

        // Subscribe to size changes to detect truncation
        if (_contentStackPanel != null)
        {
            _contentStackPanel.SizeChanged += OnContentSizeChanged;
        }

        UpdateContentVisibility();
        CheckTruncation();
    }
    
    private void OnContentSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        CheckTruncation();
    }
    
    private void CheckTruncation()
    {
        // Only check truncation for non-latest messages (latest messages have no height limit)
        if (IsLatestMessage || _contentStackPanel == null || _contentContainer == null)
        {
            IsTruncated = false;
            return;
        }

        // Check if the content's desired height exceeds the 300px max height limit
        const double MaxHeight = 200.0;
        var contentHeight = _contentStackPanel.DesiredSize.Height;
        
        IsTruncated = contentHeight > MaxHeight;
    }

    private void UpdateContentVisibility()
    {
        var hasContentBlocks = ContentBlocks is { Count: > 0 };

        if (_contentPresenter != null)
        {
            _contentPresenter.IsVisible = hasContentBlocks;
        }

        if (_legacyMessage != null)
        {
            _legacyMessage.IsVisible = !hasContentBlocks;
        }
    }
}
