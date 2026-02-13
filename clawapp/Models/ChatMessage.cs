using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace clawapp.Models;

/// <summary>
/// A single chat message (user or assistant).
/// </summary>
public sealed class ChatMessage : INotifyPropertyChanged
{
    private ObservableCollection<ContentBlock> _content = new();
    private bool _isLatestMessage;
    private bool _isStreaming;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Role { get; set; } = string.Empty; // "user" | "assistant"
    public ObservableCollection<ContentBlock> Content
    {
        get => _content;
        set
        {
            if (ReferenceEquals(_content, value)) return;
            _content = value ?? new ObservableCollection<ContentBlock>();
            OnPropertyChanged();
        }
    }
    public long? Timestamp { get; set; }
    public string? ToolCallId { get; set; }
    public string? ToolName { get; set; }
    public UsageInfo? Usage { get; set; }
    public string? StopReason { get; set; }

    public bool IsUser => string.Equals(Role, "user", StringComparison.OrdinalIgnoreCase);
    
    /// <summary>
    /// Indicates whether this is the latest message in the collection.
    /// Latest messages are displayed with full height; others are height-limited to reduce scroll jumps.
    /// </summary>
    public bool IsLatestMessage
    {
        get => _isLatestMessage;
        set
        {
            if (_isLatestMessage == value) return;
            _isLatestMessage = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Indicates whether this message is currently being streamed.
    /// Used for UI styling and typing indicator animations.
    /// </summary>
    public bool IsStreaming
    {
        get => _isStreaming;
        set
        {
            if (_isStreaming == value) return;
            _isStreaming = value;
            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// Get plain text content from all text blocks (for simple display).
    /// </summary>
    public string GetTextContent()
    {
        return string.Join("\n", Content
            .Where(c => c.Type == "text" && !string.IsNullOrEmpty(c.Text))
            .Select(c => c.Text));
    }

    /// <summary>
    /// Get thinking content from all thinking blocks.
    /// </summary>
    public string? GetThinkingContent()
    {
        var thinking = Content
            .Where(c => c.Type == "thinking" && !string.IsNullOrEmpty(c.Thinking))
            .Select(c => c.Thinking)
            .FirstOrDefault();
        return thinking;
    }
}

public sealed class ContentBlock : INotifyPropertyChanged
{
    private string? _type;
    private string? _text;
    private string? _thinking;
    private string? _thinkingSignature;
    private string? _mimeType;
    private string? _fileName;
    private object? _content;
    private string? _id;
    private string? _name;
    private object? _arguments;
    private double? _duration;
    private string? _transcript;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string? Type
    {
        get => _type;
        set
        {
            if (_type == value) return;
            _type = value;
            OnPropertyChanged();
        }
    }

    public string? Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            OnPropertyChanged();
        }
    }

    public string? Thinking
    {
        get => _thinking;
        set
        {
            if (_thinking == value) return;
            _thinking = value;
            OnPropertyChanged();
        }
    }

    public string? ThinkingSignature
    {
        get => _thinkingSignature;
        set
        {
            if (_thinkingSignature == value) return;
            _thinkingSignature = value;
            OnPropertyChanged();
        }
    }

    public string? MimeType
    {
        get => _mimeType;
        set
        {
            if (_mimeType == value) return;
            _mimeType = value;
            OnPropertyChanged();
        }
    }

    public string? FileName
    {
        get => _fileName;
        set
        {
            if (_fileName == value) return;
            _fileName = value;
            OnPropertyChanged();
        }
    }

    public object? Content
    {
        get => _content;
        set
        {
            if (ReferenceEquals(_content, value)) return;
            _content = value;
            OnPropertyChanged();
        }
    }

    public string? Id
    {
        get => _id;
        set
        {
            if (_id == value) return;
            _id = value;
            OnPropertyChanged();
        }
    }

    public string? Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            OnPropertyChanged();
        }
    }

    public object? Arguments
    {
        get => _arguments;
        set
        {
            if (ReferenceEquals(_arguments, value)) return;
            _arguments = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Audio duration in seconds.
    /// </summary>
    public double? Duration
    {
        get => _duration;
        set
        {
            if (_duration == value) return;
            _duration = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Audio transcription text (if available).
    /// </summary>
    public string? Transcript
    {
        get => _transcript;
        set
        {
            if (_transcript == value) return;
            _transcript = value;
            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class UsageInfo
{
    public int? Input { get; set; }
    public int? Output { get; set; }
    public int? CacheRead { get; set; }
    public int? CacheWrite { get; set; }
    public UsageCost? Cost { get; set; }
    public int? Total { get; set; }
}

public sealed class UsageCost
{
    public double? Input { get; set; }
    public double? Output { get; set; }
    public double? CacheRead { get; set; }
    public double? CacheWrite { get; set; }
    public double? Total { get; set; }
}
