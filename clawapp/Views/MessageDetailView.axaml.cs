using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using clawapp.ViewModels;

namespace clawapp.Views;

/// <summary>
/// User control for displaying detailed message content in an overlay.
/// </summary>
public partial class MessageDetailView : UserControl
{
    public event EventHandler? CloseRequested;

    public MessageDetailView()
    {
        InitializeComponent();
    }

    public MessageDetailView(MessageDetailViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void CopyTextButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MessageDetailViewModel vm && !string.IsNullOrEmpty(vm.TextContent))
        {
            await CopyToClipboardAsync(vm.TextContent);
        }
    }

    private async void CopyThinkingButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MessageDetailViewModel vm && !string.IsNullOrEmpty(vm.ThinkingContent))
        {
            await CopyToClipboardAsync(vm.ThinkingContent);
        }
    }

    private async void CopyToolCallsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MessageDetailViewModel vm && !string.IsNullOrEmpty(vm.ToolCallsContent))
        {
            await CopyToClipboardAsync(vm.ToolCallsContent);
        }
    }

    private async void CopyMetadataButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MessageDetailViewModel vm && !string.IsNullOrEmpty(vm.MetadataContent))
        {
            await CopyToClipboardAsync(vm.MetadataContent);
        }
    }

    private async Task CopyToClipboardAsync(string text)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(text);
        }
    }
}
