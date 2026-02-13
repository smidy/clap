using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.VisualTree;
using clawapp.Models;
using clawapp.ViewModels;
using System;
using System.Collections.Specialized;
using System.Linq;

namespace clawapp.Views;

public partial class ChatView : UserControl
{
    private IInputPane? _inputPane;
    private ScrollViewer? _scrollViewer;
    private bool _shouldScrollToBottomOnNextAdd = false;

    public ChatView()
    {
        InitializeComponent();
        
        // Subscribe to DataContext changes to hook into FilteredMessages collection
        DataContextChanged += OnDataContextChanged;
        
        // Subscribe to loaded event to setup InputPane
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        MessagesListBox.AutoScrollToSelectedItem = false;
    }

    public  ScrollBar? GetScrollBar(ScrollViewer scrollViewer)
    {
        var verticalScrollBar = scrollViewer.GetVisualDescendants()
            .OfType<ScrollBar>()
            .FirstOrDefault(sb => sb.Orientation == Orientation.Vertical);
        return verticalScrollBar;
    }
  
    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Get InputPane from TopLevel
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.InputPane != null)
        {
            _inputPane = topLevel.InputPane;
            _inputPane.StateChanged += OnInputPaneStateChanged;
        }

        var scrollViewer = GetScrollViewer();

        if (scrollViewer != null)
        {
            scrollViewer.ScrollChanged += OnScrollChanged;
        }
    }
    
    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (_inputPane != null)
        {
            _inputPane.StateChanged -= OnInputPaneStateChanged;
            _inputPane = null;
        }

        var scrollViewer = GetScrollViewer();

        if (scrollViewer != null)
        {
            scrollViewer.ScrollChanged -= OnScrollChanged;
        }
    }
    
    private void OnInputPaneStateChanged(object? sender, InputPaneStateEventArgs e)
    {
        if (e.NewState == InputPaneState.Open && IsScrolledToBottom())
        {
            ScrollToBottom();
        }
    }
    
    private async void AttachButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ChatViewModel vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } storageProvider) return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select files to attach",
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                FilePickerFileTypes.All
            }
        });

        if (files.Count > 0)
        {
            await vm.AddAttachmentsAsync(files);
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ChatViewModel vm)
        {
            // Unsubscribe from old collection changes if any
            vm.FilteredMessages.CollectionChanged -= OnMessagesCollectionChanged;
            // Subscribe to new VM's filtered messages collection
            vm.FilteredMessages.CollectionChanged += OnMessagesCollectionChanged;
            
        }
    }
    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        // Update scroll-to-bottom button visibility
        if (DataContext is ChatViewModel vm)
        {
            vm.IsScrollToBottomButtonVisible = !IsScrolledToBottom();
        }

        // When content extent grows (streaming message getting longer), stay at bottom if already there
        if (e.ExtentDelta.Y > 0 && IsScrolledToBottom())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(
                ScrollToBottom,
                Avalonia.Threading.DispatcherPriority.Render);
        }
    }
    private void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // When messages are cleared (session change), set flag to scroll on next add
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            _shouldScrollToBottomOnNextAdd = true;
            return;
        }
        
        // Only scroll when new messages are added
        if (e.Action != NotifyCollectionChangedAction.Add) return;
        if (MessagesListBox.ItemCount == 0) return;

        // If this is the first batch after clearing (initial load), always scroll to bottom
        if (_shouldScrollToBottomOnNextAdd)
        {
            _shouldScrollToBottomOnNextAdd = false;
            ScrollToBottom();
            return;
        }

        // For subsequent messages, only scroll if already at bottom
        // This prevents interrupting the user if they're reading message history
        if (!IsScrolledToBottom()) return;

        ScrollToBottom();
    }

    private bool IsScrolledToBottom()
    {
        var scrollViewer = GetScrollViewer();

        if (scrollViewer == null) return true; // Default to true if can't find ScrollViewer

        // Check if we're within 5px of the bottom (tolerance for floating point rounding)
        var offset = scrollViewer.Offset.Y;
        var extent = scrollViewer.Extent.Height;
        var viewport = scrollViewer.Viewport.Height;
        var maxScroll = extent - viewport;

        // If content is smaller than viewport, we're always "at bottom"
        if (maxScroll <= 0) return true;

        // Check if within 5px of bottom
        return (maxScroll - offset) < 5.0;
    }

    private ScrollViewer? GetScrollViewer()
    {
        if (_scrollViewer == null)
        {
            // Find the ScrollViewer inside the ListBox
            _scrollViewer = MessagesListBox.GetVisualDescendants()
                .OfType<ScrollViewer>()
                .FirstOrDefault();
        }

        return _scrollViewer;
    }

    private void ScrollToBottom()
    {
        var scrollViewer = GetScrollViewer();

        if (scrollViewer == null) return;

        if (MessagesListBox.ItemCount == 0) return;

        Avalonia.Threading.Dispatcher.UIThread.Post(
            scrollViewer.ScrollToEnd,
            Avalonia.Threading.DispatcherPriority.Render);
    }

    private void OnMessageDoubleTapped(object? sender, TappedEventArgs e)
    {
        // Get the tapped ListBoxItem
        var source = e.Source as Control;
        var listBoxItem = source?.FindAncestorOfType<ListBoxItem>();
        
        if (listBoxItem?.DataContext is not ChatMessage message) return;

        // Create the message detail view
        var viewModel = new MessageDetailViewModel(message);
        var detailView = new MessageDetailView(viewModel);
        
        // Handle close event
        detailView.CloseRequested += (_, _) =>
        {
            MessageDetailOverlay.Children.Clear();
            MessageDetailOverlay.IsVisible = false;
        };

        // Show in overlay
        MessageDetailOverlay.Children.Clear();
        MessageDetailOverlay.Children.Add(detailView);
        MessageDetailOverlay.IsVisible = true;
    }

    private void ScrollToBottomButton_Click(object? sender, RoutedEventArgs e)
    {
        ScrollToBottom();
    }
}