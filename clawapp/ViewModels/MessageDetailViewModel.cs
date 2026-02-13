using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using clawapp.Models;

namespace clawapp.ViewModels;

/// <summary>
/// ViewModel for the message detail dialog showing full message content.
/// </summary>
public partial class MessageDetailViewModel : ViewModelBase
{
    [ObservableProperty]
    private ChatMessage _message;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _textContent = string.Empty;

    [ObservableProperty]
    private string? _thinkingContent;

    [ObservableProperty]
    private string _toolCallsContent = string.Empty;

    [ObservableProperty]
    private string _metadataContent = string.Empty;

    [ObservableProperty]
    private bool _hasThinking;

    [ObservableProperty]
    private bool _hasToolCalls;

    [ObservableProperty]
    private bool _hasFiles;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private bool _isWordWrapEnabled = true;

    public MessageDetailViewModel(ChatMessage message)
    {
        _message = message;
        InitializeContent();
    }

    private void InitializeContent()
    {
        // Set title
        var roleDisplay = Message.IsUser ? "User" : "Assistant";
        var timestampDisplay = TryFormatTimestamp(Message.Timestamp, "yyyy-MM-dd HH:mm:ss") ?? "Unknown time";
        Title = $"{roleDisplay} Message - {timestampDisplay}";

        // Extract text content
        var textBlocks = Message.Content
            .Where(c => c.Type == "text" && !string.IsNullOrEmpty(c.Text))
            .Select(c => c.Text)
            .ToList();
        TextContent = textBlocks.Count > 0 ? string.Join("\n\n", textBlocks) : "(No text content)";

        // Extract thinking content
        var thinkingBlocks = Message.Content
            .Where(c => c.Type == "thinking" && !string.IsNullOrEmpty(c.Thinking))
            .Select(c => c.Thinking)
            .ToList();
        ThinkingContent = thinkingBlocks.Count > 0 ? string.Join("\n\n", thinkingBlocks) : null;
        HasThinking = ThinkingContent != null;

        // Extract tool calls
        var toolCallBlocks = Message.Content
            .Where(c => c.Type == "toolCall")
            .ToList();
        if (toolCallBlocks.Count > 0)
        {
            var toolCallsFormatted = new List<string>();
            foreach (var toolCall in toolCallBlocks)
            {
                var toolInfo = $"Tool: {toolCall.Name ?? "Unknown"}\n";
                toolInfo += $"ID: {toolCall.Id ?? "N/A"}\n";
                if (toolCall.Arguments != null)
                {
                    try
                    {
                        var argsJson = JsonSerializer.Serialize(toolCall.Arguments, new JsonSerializerOptions { WriteIndented = true });
                        toolInfo += $"Arguments:\n{argsJson}";
                    }
                    catch
                    {
                        toolInfo += $"Arguments: {toolCall.Arguments}";
                    }
                }
                toolCallsFormatted.Add(toolInfo);
            }
            ToolCallsContent = string.Join("\n\n---\n\n", toolCallsFormatted);
            HasToolCalls = true;
        }
        else
        {
            ToolCallsContent = "(No tool calls)";
            HasToolCalls = false;
        }

        // Check for files
        HasFiles = Message.Content.Any(c => c.Type == "file");

        // Build metadata
        var metadata = new List<string>
        {
            $"Role: {Message.Role}",
            $"Timestamp: {timestampDisplay}"
        };

        var messageTime = TryParseTimestamp(Message.Timestamp);
        if (messageTime.HasValue)
        {
            var age = DateTimeOffset.UtcNow - messageTime.Value;
            metadata.Add($"Age: {FormatAge(age)}");
        }

        if (Message.Usage != null)
        {
            metadata.Add("");
            metadata.Add("Token Usage:");
            if (Message.Usage.Input.HasValue)
                metadata.Add($"  Input: {Message.Usage.Input.Value:N0}");
            if (Message.Usage.Output.HasValue)
                metadata.Add($"  Output: {Message.Usage.Output.Value:N0}");
            if (Message.Usage.CacheRead.HasValue && Message.Usage.CacheRead.Value > 0)
                metadata.Add($"  Cache Read: {Message.Usage.CacheRead.Value:N0}");
            if (Message.Usage.CacheWrite.HasValue && Message.Usage.CacheWrite.Value > 0)
                metadata.Add($"  Cache Write: {Message.Usage.CacheWrite.Value:N0}");
            if (Message.Usage.Total.HasValue)
                metadata.Add($"  Total: {Message.Usage.Total.Value:N0}");

            if (Message.Usage.Cost != null)
            {
                metadata.Add("");
                metadata.Add("Cost:");
                if (Message.Usage.Cost.Input.HasValue)
                    metadata.Add($"  Input: ${Message.Usage.Cost.Input.Value:F4}");
                if (Message.Usage.Cost.Output.HasValue)
                    metadata.Add($"  Output: ${Message.Usage.Cost.Output.Value:F4}");
                if (Message.Usage.Cost.Total.HasValue)
                    metadata.Add($"  Total: ${Message.Usage.Cost.Total.Value:F4}");
            }
        }

        if (!string.IsNullOrEmpty(Message.StopReason))
        {
            metadata.Add("");
            metadata.Add($"Stop Reason: {Message.StopReason}");
        }

        if (!string.IsNullOrEmpty(Message.ToolCallId))
        {
            metadata.Add("");
            metadata.Add($"Tool Call ID: {Message.ToolCallId}");
        }

        if (!string.IsNullOrEmpty(Message.ToolName))
        {
            metadata.Add($"Tool Name: {Message.ToolName}");
        }

        metadata.Add("");
        metadata.Add($"Content Blocks: {Message.Content.Count}");
        
        // List content block types
        var blockTypes = Message.Content
            .GroupBy(c => c.Type ?? "unknown")
            .Select(g => $"  {g.Key}: {g.Count()}")
            .ToList();
        if (blockTypes.Count > 0)
        {
            metadata.AddRange(blockTypes);
        }

        MetadataContent = string.Join("\n", metadata);
    }

    private static string FormatAge(TimeSpan age)
    {
        if (age.TotalMinutes < 1)
            return "Just now";
        if (age.TotalMinutes < 60)
            return $"{(int)age.TotalMinutes} minute{((int)age.TotalMinutes == 1 ? "" : "s")} ago";
        if (age.TotalHours < 24)
            return $"{(int)age.TotalHours} hour{((int)age.TotalHours == 1 ? "" : "s")} ago";
        return $"{(int)age.TotalDays} day{((int)age.TotalDays == 1 ? "" : "s")} ago";
    }

    /// <summary>
    /// Safely converts a Unix timestamp to DateTimeOffset, returning null if invalid.
    /// </summary>
    private static DateTimeOffset? TryParseTimestamp(long? timestamp)
    {
        if (!timestamp.HasValue)
            return null;

        // Valid Unix timestamp range: -62135596800 (year 0001) to 253402300799 (year 9999)
        const long MinUnixTimestamp = -62135596800;
        const long MaxUnixTimestamp = 253402300799;

        if (timestamp.Value < MinUnixTimestamp || timestamp.Value > MaxUnixTimestamp)
            return null;

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(timestamp.Value);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Safely formats a Unix timestamp, returning null if invalid.
    /// </summary>
    private static string? TryFormatTimestamp(long? timestamp, string format)
    {
        var dateTime = TryParseTimestamp(timestamp);
        return dateTime?.ToString(format);
    }
}
