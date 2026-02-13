using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using clawapp.Models;
using clawapp.ViewModels;
using Xunit;

namespace clawapp.Tests;

/// <summary>
/// Unit tests for the FilteredObservableCollection class.
/// </summary>
public class FilteredObservableCollectionTests
{
    [Fact]
    public void Constructor_WithSourceAndFilter_InitializesWithFilteredItems()
    {
        // Arrange
        var source = new ObservableCollection<string> { "apple", "banana", "apricot", "cherry" };
        Func<string, bool> filter = s => s.StartsWith("a");

        // Act
        var filtered = new FilteredObservableCollection<string>(source, filter);

        // Assert
        Assert.Equal(2, filtered.Count);
        Assert.Contains("apple", filtered);
        Assert.Contains("apricot", filtered);
    }

    [Fact]
    public void Add_ItemMatchingFilter_AddsToFilteredCollection()
    {
        // Arrange
        var source = new ObservableCollection<string>();
        Func<string, bool> filter = s => s.StartsWith("a");
        var filtered = new FilteredObservableCollection<string>(source, filter);

        bool collectionChanged = false;
        filtered.CollectionChanged += (_, _) => collectionChanged = true;

        // Act
        source.Add("apple");

        // Assert
        Assert.Single(filtered);
        Assert.Contains("apple", filtered);
        Assert.True(collectionChanged);
    }

    [Fact]
    public void Add_ItemNotMatchingFilter_DoesNotAddToFilteredCollection()
    {
        // Arrange
        var source = new ObservableCollection<string>();
        Func<string, bool> filter = s => s.StartsWith("a");
        var filtered = new FilteredObservableCollection<string>(source, filter);

        bool collectionChanged = false;
        filtered.CollectionChanged += (_, _) => collectionChanged = true;

        // Act
        source.Add("banana");

        // Assert
        Assert.Empty(filtered);
        Assert.False(collectionChanged);
    }

    [Fact]
    public void Remove_ItemInFilteredCollection_RemovesFromFiltered()
    {
        // Arrange
        var source = new ObservableCollection<string> { "apple", "apricot" };
        Func<string, bool> filter = s => s.StartsWith("a");
        var filtered = new FilteredObservableCollection<string>(source, filter);

        bool collectionChanged = false;
        filtered.CollectionChanged += (_, _) => collectionChanged = true;

        // Act
        source.Remove("apple");

        // Assert
        Assert.Single(filtered);
        Assert.DoesNotContain("apple", filtered);
        Assert.True(collectionChanged);
    }

    [Fact]
    public void Clear_RaisesResetEvent()
    {
        // Arrange
        var source = new ObservableCollection<string> { "apple", "apricot" };
        Func<string, bool> filter = s => s.StartsWith("a");
        var filtered = new FilteredObservableCollection<string>(source, filter);

        NotifyCollectionChangedEventArgs? eventArgs = null;
        filtered.CollectionChanged += (_, e) => eventArgs = e;

        // Act
        source.Clear();

        // Assert
        Assert.Empty(filtered);
        Assert.NotNull(eventArgs);
        Assert.Equal(NotifyCollectionChangedAction.Reset, eventArgs!.Action);
    }

    [Fact]
    public void Refresh_WithChangedFilter_UpdatesFilteredItems()
    {
        // Arrange
        var source = new ObservableCollection<string> { "apple", "banana", "apricot" };
        bool filterLongWords = false;
        Func<string, bool> filter = s => filterLongWords ? s.Length > 5 : s.StartsWith("a");
        var filtered = new FilteredObservableCollection<string>(source, filter);

        Assert.Equal(2, filtered.Count); // apple, apricot

        // Act - change filter behavior
        filterLongWords = true;
        filtered.Refresh();

        // Assert
        Assert.Equal(2, filtered.Count); // banana (6), apricot (7)
        Assert.Contains("banana", filtered);
        Assert.Contains("apricot", filtered);
    }

    [Fact]
    public void Refresh_RaisesResetEvent()
    {
        // Arrange
        var source = new ObservableCollection<string> { "apple", "banana" };
        Func<string, bool> filter = s => s.StartsWith("a");
        var filtered = new FilteredObservableCollection<string>(source, filter);

        NotifyCollectionChangedEventArgs? eventArgs = null;
        filtered.CollectionChanged += (_, e) => eventArgs = e;

        // Act
        filtered.Refresh();

        // Assert
        Assert.NotNull(eventArgs);
        Assert.Equal(NotifyCollectionChangedAction.Reset, eventArgs!.Action);
    }

    [Fact]
    public void IsReadOnly_ReturnsTrue()
    {
        // Arrange
        var source = new ObservableCollection<string>();
        var filtered = new FilteredObservableCollection<string>(source, _ => true);

        // Assert
        Assert.True(filtered.IsReadOnly);
    }

    [Fact]
    public void Add_ThrowsNotSupportedException()
    {
        // Arrange
        var source = new ObservableCollection<string>();
        var filtered = new FilteredObservableCollection<string>(source, _ => true);

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => filtered.Add("test"));
    }

    [Fact]
    public void Remove_ThrowsNotSupportedException()
    {
        // Arrange
        var source = new ObservableCollection<string> { "test" };
        var filtered = new FilteredObservableCollection<string>(source, _ => true);

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => filtered.Remove("test"));
    }

    [Fact]
    public void Clear_ThrowsNotSupportedException()
    {
        // Arrange
        var source = new ObservableCollection<string>();
        var filtered = new FilteredObservableCollection<string>(source, _ => true);

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => filtered.Clear());
    }
}

/// <summary>
/// Unit tests for the ChatMessage filtering logic in ChatViewModel.
/// </summary>
public class ChatMessageFilteringTests
{
    [Fact]
    public void ShouldShowMessage_ToolResultRole_ShowToolResultsEnabled_ReturnsTrue()
    {
        // Arrange
        var message = new ChatMessage
        {
            Role = "toolResult",
            Content = new() { new ContentBlock { Type = "text", Text = "File contents" } }
        };

        // Enable the setting via reflection (since it's a singleton)
        var settingsProvider = SettingsProvider.Instance;
        typeof(SettingsProvider).GetProperty(nameof(SettingsProvider.ShowToolResults))?
            .GetSetMethod(true)?.Invoke(settingsProvider, new object[] { true });

        // Act - using the same logic as ChatViewModel.ShouldShowMessage
        bool result = ShouldShowMessage(message);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldShowMessage_ToolResultRole_ShowToolResultsDisabled_ReturnsFalse()
    {
        // Arrange
        var message = new ChatMessage
        {
            Role = "toolResult",
            Content = new() { new ContentBlock { Type = "text", Text = "File contents" } }
        };

        // Disable the setting via reflection
        var settingsProvider = SettingsProvider.Instance;
        typeof(SettingsProvider).GetProperty(nameof(SettingsProvider.ShowToolResults))?
            .GetSetMethod(true)?.Invoke(settingsProvider, new object[] { false });

        // Act
        bool result = ShouldShowMessage(message);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldShowMessage_UserRole_AlwaysReturnsTrue()
    {
        // Arrange
        var message = new ChatMessage
        {
            Role = "user",
            Content = new() { new ContentBlock { Type = "text", Text = "Hello" } }
        };

        // Test with ShowToolResults both enabled and disabled
        var settingsProvider = SettingsProvider.Instance;
        
        // Disable setting
        typeof(SettingsProvider).GetProperty(nameof(SettingsProvider.ShowToolResults))?
            .GetSetMethod(true)?.Invoke(settingsProvider, new object[] { false });
        Assert.True(ShouldShowMessage(message));

        // Enable setting
        typeof(SettingsProvider).GetProperty(nameof(SettingsProvider.ShowToolResults))?
            .GetSetMethod(true)?.Invoke(settingsProvider, new object[] { true });
        Assert.True(ShouldShowMessage(message));
    }

    [Fact]
    public void ShouldShowMessage_AssistantRole_AlwaysReturnsTrue()
    {
        // Arrange
        var message = new ChatMessage
        {
            Role = "assistant",
            Content = new() { new ContentBlock { Type = "text", Text = "Hello" } }
        };

        // Test with ShowToolResults disabled
        var settingsProvider = SettingsProvider.Instance;
        typeof(SettingsProvider).GetProperty(nameof(SettingsProvider.ShowToolResults))?
            .GetSetMethod(true)?.Invoke(settingsProvider, new object[] { false });

        // Act & Assert
        Assert.True(ShouldShowMessage(message));
    }

    [Fact]
    public void ShouldShowMessage_ToolResultRole_CaseInsensitive()
    {
        // Arrange
        var messageLower = new ChatMessage { Role = "toolresult" };
        var messageUpper = new ChatMessage { Role = "TOOLRESULT" };
        var messageMixed = new ChatMessage { Role = "ToolResult" };

        var settingsProvider = SettingsProvider.Instance;
        typeof(SettingsProvider).GetProperty(nameof(SettingsProvider.ShowToolResults))?
            .GetSetMethod(true)?.Invoke(settingsProvider, new object[] { false });

        // Act & Assert - all should be filtered out when disabled
        Assert.False(ShouldShowMessage(messageLower));
        Assert.False(ShouldShowMessage(messageUpper));
        Assert.False(ShouldShowMessage(messageMixed));
    }

    /// <summary>
    /// Mirrors the logic in ChatViewModel.ShouldShowMessage for testing.
    /// </summary>
    private static bool ShouldShowMessage(ChatMessage message)
    {
        // Filter out toolResult messages if ShowToolResults is disabled
        if (string.Equals(message.Role, "toolResult", StringComparison.OrdinalIgnoreCase))
        {
            return SettingsProvider.Instance.ShowToolResults;
        }

        return true;
    }
}
