using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using clawapp.Input;
using clawapp.Models;
using clawapp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace clawapp.ViewModels;

/// <summary>
/// Represents a message received while the app was backgrounded.
/// </summary>
/// <param name="SessionKey">The session key the message belongs to.</param>
/// <param name="RunId">The run ID for streaming messages (may be null).</param>
/// <param name="State">The message state (streaming, final, etc.).</param>
/// <param name="Message">The chat message content.</param>
public record BackgroundMessage(string SessionKey, string? RunId, string? State, ChatMessage Message);

public partial class ChatViewModel : ViewModelBase, IDisposable
{
    private readonly IOpenClawService _openClawService;
    private readonly IAudioRecorderService _audioRecorderService;
    private readonly ISettingsService _settingsService;
    private readonly INotificationService _notificationService;
    private readonly IAppLifecycleService _lifecycleService;
    private readonly ILogger<ChatViewModel> _logger;

    /// <summary>Maps runId to the in-progress assistant message so deltas update the same bubble.</summary>
    private readonly Dictionary<string, ChatMessage> _runIdToMessage = new();

    /// <summary>Buffers streaming content when ShowStreamingMessages is disabled. Key: runId</summary>
    private readonly Dictionary<string, ObservableCollection<ContentBlock>> _streamingContentBuffer = new();

    /// <summary>Timer for typing indicator animation when streaming is disabled.</summary>
    private readonly System.Timers.Timer _typingAnimationTimer;

    /// <summary>Queue for messages received while app is backgrounded. Thread-safe.</summary>
    private readonly ConcurrentQueue<BackgroundMessage> _backgroundMessageQueue = new();

    /// <summary>Current typing indicator frame (0=., 1=.., 2=...)</summary>
    private int _typingFrame;

    [ObservableProperty]
    private string _currentMessage = "";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isSending;

    [ObservableProperty]
    private bool _isSettingsOpen;

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private TimeSpan _recordingDuration;

    [ObservableProperty]
    private bool _isScrollToBottomButtonVisible;

    public SessionListViewModel SessionList { get; }
    public SettingsViewModel Settings { get; }

    public ObservableCollection<ChatMessage> Messages { get; } = new();
    public ObservableCollection<PendingAttachment> PendingAttachments { get; } = new();

    /// <summary>
    /// Filtered view of Messages that respects user settings (e.g., hiding tool results).
    /// This is what the UI should bind to for display.
    /// </summary>
    public FilteredObservableCollection<ChatMessage> FilteredMessages { get; }

    public bool HasPendingAttachments => PendingAttachments.Count > 0;
    public bool IsAudioRecordingSupported => _audioRecorderService.IsSupported;

    // Connection state exposed for UI
    public bool IsConnected => _openClawService.IsConnected;
    public string ConnectionStatusText => IsConnected ? "Connected" : "Disconnected";

    // Platform detection for responsive UI
    public bool IsDesktop
    {
        get
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
                return true;
            return false;
        }
    }

    /// <summary>Commands using AvaloniaAsyncRelayCommand to ensure CanExecuteChanged is raised on the UI thread.</summary>
    public ICommand ToggleRecordingCommand { get; }
    public ICommand SendMessageCommand { get; }
    public ICommand DisconnectCommand { get; }

    public ChatViewModel(IOpenClawService openClawService, ISettingsService settingsService, IAudioRecorderService audioRecorderService, INotificationService notificationService, IAppLifecycleService lifecycleService, ILogger<ChatViewModel> logger)
    {
        _openClawService = openClawService;
        _audioRecorderService = audioRecorderService;
        _settingsService = settingsService;
        _notificationService = notificationService;
        _lifecycleService = lifecycleService;
        _logger = logger;
        SessionList = new SessionListViewModel(openClawService, OnSessionSelectedAsync);
        Settings = new SettingsViewModel(settingsService);

        // Subscribe to lifecycle events to flush background message queue when foregrounded
        _lifecycleService.ForegroundEntered += OnForegroundEntered;

        // Initialize typing animation timer (500ms interval)
        _typingAnimationTimer = new System.Timers.Timer(500);
        _typingAnimationTimer.Elapsed += OnTypingAnimationTick;
        _typingAnimationTimer.AutoReset = true;

        // Initialize filtered messages collection
        FilteredMessages = new FilteredObservableCollection<ChatMessage>(Messages, ShouldShowMessage);

        // Subscribe to settings changes to refresh the filter
        SettingsProvider.Instance.PropertyChanged += OnSettingsChanged;

        // Initialize commands with Avalonia-safe wrapper
        ToggleRecordingCommand = new AvaloniaAsyncRelayCommand(ToggleRecordingAsync);
        SendMessageCommand = new AvaloniaAsyncRelayCommand(SendMessageAsync);
        DisconnectCommand = new AvaloniaAsyncRelayCommand(DisconnectAsync);

        _openClawService.OnConnected += (_, _) =>
        {
            _logger.LogInformation("Connected to gateway");
            Dispatcher.UIThread.Post(() =>
            {
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(ConnectionStatusText));
            });
        };

        _openClawService.OnDisconnected += (_, _) =>
        {
            _logger.LogInformation("Disconnected from gateway");
            Dispatcher.UIThread.Post(() =>
            {
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(ConnectionStatusText));
            });
        };

        _openClawService.OnReconnected += (_, _) =>
        {
            _logger.LogInformation("Reconnected to gateway, refreshing session data");
            _ = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(ConnectionStatusText));
                // Refresh messages for the current session after reconnection
                // to catch any messages that arrived while disconnected
                await RefreshCurrentSessionAsync();
            });
        };

        _openClawService.OnMessageReceived += (_, e) =>
        {
            if (e.SessionKey != SessionList.SelectedSession?.Key)
                return;

            var (sessionKey, runId, state, message) = e;

            // If app is backgrounded, queue the message instead of dispatching to UI
            if (!_lifecycleService.IsForeground)
            {
                _logger.LogDebug("App is backgrounded, queuing message for session {SessionKey}", sessionKey);
                _backgroundMessageQueue.Enqueue(new BackgroundMessage(sessionKey, runId, state, message));
                return;
            }

            // App is foregrounded, process normally
            ProcessMessage(sessionKey, runId, state, message);
        };

        _audioRecorderService.DurationUpdated += (_, duration) =>
        {
            Dispatcher.UIThread.Post(() => RecordingDuration = duration);
        };

        PendingAttachments.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasPendingAttachments));
        
        // Track latest message for height limiting (reduces scroll jumps)
        Messages.CollectionChanged += (_, e) =>
        {
            UpdateLatestMessageFlags();
        };
    }
    
    /// <summary>
    /// Updates the IsLatestMessage flag on all messages.
    /// Only the last message in the collection is marked as latest (gets full height).
    /// </summary>
    private void UpdateLatestMessageFlags()
    {
        if (Messages.Count == 0) return;
        
        // Clear all flags first
        foreach (var msg in Messages)
        {
            msg.IsLatestMessage = false;
        }
        
        // Mark the last message as latest
        Messages[Messages.Count - 1].IsLatestMessage = true;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await Settings.InitializeAsync().ConfigureAwait(false);
        await SessionList.RefreshCommand.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
    }

    [RelayCommand]
    private void ToggleSettings()
    {
        IsSettingsOpen = !IsSettingsOpen;
    }

    private async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        await _openClawService.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        // Navigate back to connection view will be handled by MainViewModel
        // which listens to the OnDisconnected event
    }

    private async Task ToggleRecordingAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (IsRecording)
            {
                var result = await _audioRecorderService.StopRecordingAsync(cancellationToken).ConfigureAwait(false);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    IsRecording = false;
                    if (result.Data.Length > 0)
                    {
                        var attachment = new PendingAttachment
                        {
                            FileName = result.FileName,
                            MimeType = result.MimeType,
                            Data = result.Data
                        };
                        PendingAttachments.Add(attachment);
                    }
                });
            }
            else
            {
                if (!_audioRecorderService.IsSupported)
                {
                    _logger.LogWarning("Audio recording is not supported on this platform");
                    return;
                }
                await _audioRecorderService.StartRecordingAsync(cancellationToken).ConfigureAwait(false);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    IsRecording = true;
                    RecordingDuration = TimeSpan.Zero;
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audio recording operation failed: {Message}", ex.Message);
            await Dispatcher.UIThread.InvokeAsync(() => IsRecording = false);
        }
    }

    [RelayCommand]
    private void CancelRecording()
    {
        if (!IsRecording) return;

        _audioRecorderService.CancelRecording();
        IsRecording = false;
        RecordingDuration = TimeSpan.Zero;
    }

    /// <summary>
    /// Adds files to the pending attachments list.
    /// This is called from the view after file picker dialog.
    /// </summary>
    public async Task AddAttachmentsAsync(IEnumerable<Avalonia.Platform.Storage.IStorageFile> files)
    {
        foreach (var file in files)
        {
            try
            {
                await using var stream = await file.OpenReadAsync().ConfigureAwait(false);
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream).ConfigureAwait(false);
                var data = memoryStream.ToArray();

                var mimeType = GetMimeType(file.Name);
                var attachment = new PendingAttachment
                {
                    FileName = file.Name,
                    MimeType = mimeType,
                    Data = data
                };

                Avalonia.Threading.Dispatcher.UIThread.Post(() => PendingAttachments.Add(attachment));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load attachment {FileName}: {Message}", file.Name, ex.Message);
            }
        }
    }

    [RelayCommand]
    private void RemoveAttachment(PendingAttachment attachment)
    {
        PendingAttachments.Remove(attachment);
    }

    [RelayCommand]
    private void ClearAttachments()
    {
        PendingAttachments.Clear();
    }

    private static string GetMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".html" or ".htm" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".zip" => "application/zip",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            _ => "application/octet-stream"
        };
    }

    private async Task OnSessionSelectedAsync(Session? session)
    {
        if (session == null) return;

        // Clear collections on UI thread to avoid cross-thread access violations
        // when FilteredMessages processes the Reset notification
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _runIdToMessage.Clear();
            Messages.Clear();
        });

        IsLoading = true;
        try
        {
            await _openClawService.SubscribeToSessionAsync(session.Key).ConfigureAwait(false);
            var history = await _openClawService.GetHistoryAsync(session.Key, limit: 50).ConfigureAwait(false);
            Dispatcher.UIThread.Post(() =>
            {
                foreach (var m in history)
                    Messages.Add(m);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load session {SessionKey}: {Message}", session.Key, ex.Message);
            throw;
        }
        finally
        {
            Dispatcher.UIThread.Post(() => IsLoading = false);
        }
    }

    /// <summary>
    /// Refreshes the current session's message history after reconnection.
    /// This ensures messages that arrived while disconnected are displayed.
    /// </summary>
    private async Task RefreshCurrentSessionAsync()
    {
        var session = SessionList.SelectedSession;
        if (session == null) return;

        _logger.LogInformation("Refreshing session {SessionKey} after reconnection", session.Key);

        try
        {
            // Re-subscribe to ensure we get new messages
            await _openClawService.SubscribeToSessionAsync(session.Key).ConfigureAwait(false);

            // Fetch latest history - use a higher limit to ensure we don't miss messages
            var history = await _openClawService.GetHistoryAsync(session.Key, limit: 100).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Merge new messages without clearing existing ones to avoid UI flicker
                var existingIds = new HashSet<string>();
                foreach (var msg in Messages)
                {
                    var msgId = $"{msg.Role}:{msg.Timestamp}";
                    existingIds.Add(msgId);
                }

                int addedCount = 0;
                foreach (var msg in history)
                {
                    var msgId = $"{msg.Role}:{msg.Timestamp}";
                    if (!existingIds.Contains(msgId))
                    {
                        Messages.Add(msg);
                        addedCount++;
                    }
                }

                if (addedCount > 0)
                {
                    _logger.LogInformation("Added {Count} new messages after reconnection", addedCount);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh session {SessionKey} after reconnection", session.Key);
        }
    }

    private async Task SendMessageAsync(CancellationToken cancellationToken)
    {
        var sessionKey = SessionList.SelectedSession?.Key ?? "main";
        var text = CurrentMessage?.Trim() ?? "";
        var hasAttachments = PendingAttachments.Count > 0;

        // Require either text or attachments
        if (string.IsNullOrEmpty(text) && !hasAttachments) return;
        if (!_openClawService.IsConnected) return;

        // Capture attachments before clearing
        var attachments = PendingAttachments.ToList();

        CurrentMessage = "";
        PendingAttachments.Clear();
        IsSending = true;

        // Build content blocks for the local message display
        var contentBlocks = new ObservableCollection<ContentBlock>();
        if (!string.IsNullOrEmpty(text))
        {
            contentBlocks.Add(new ContentBlock { Type = "text", Text = text });
        }
        foreach (var attachment in attachments)
        {
            contentBlocks.Add(new ContentBlock
            {
                Type = "file",
                FileName = attachment.FileName,
                MimeType = attachment.MimeType
            });
        }

        try
        {
            if (hasAttachments)
            {
                await _openClawService.SendMessageWithAttachmentsAsync(sessionKey, text, attachments, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _openClawService.SendMessageAsync(sessionKey, text, cancellationToken).ConfigureAwait(false);
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Messages.Add(new ChatMessage
                {
                    Role = "user",
                    Content = contentBlocks,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Send message failed for session {SessionKey}: {Message}", sessionKey, ex.Message);
            throw;
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsSending = false);
        }
    }

    /// <summary>
    /// Sets the MainViewModel reference for theme synchronization.
    /// Called by MainView when the DataContext is set.
    /// </summary>
    public void SetMainViewModel(MainViewModel mainViewModel)
    {
        Settings.MainViewModel = mainViewModel;
    }

    /// <summary>
    /// Determines whether a message should be shown based on user settings.
    /// </summary>
    private static bool ShouldShowMessage(ChatMessage message)
    {
        // Handle null message (shouldn't happen, but be defensive)
        if (message == null)
            return false;

        // Hide toolResult messages if ShowToolResults is disabled
        if (!string.IsNullOrEmpty(message.Role) && 
            string.Equals(message.Role, "toolResult", StringComparison.OrdinalIgnoreCase))
        {
            if (!SettingsProvider.Instance.ShowToolResults)
                return false;
        }

        // Check if message has any visible content after applying filters
        // Hide messages that only contain filtered-out blocks (e.g., thinking-only when ShowThinkingBlocks = false)
        return HasVisibleContent(message);
    }

    /// <summary>
    /// Checks if a message has any visible content blocks after applying current filter settings.
    /// </summary>
    private static bool HasVisibleContent(ChatMessage message)
    {
        if (message.Content == null || message.Content.Count == 0)
            return false;

        foreach (var item in message.Content)
        {
            if (item is not ContentBlock block)
                continue;

            // Check each block type against settings
            switch (block.Type?.ToLowerInvariant())
            {
                case "thinking":
                    if (SettingsProvider.Instance.ShowThinkingBlocks)
                        return true;
                    break;

                case "toolcall":
                    if (SettingsProvider.Instance.ShowToolCalls)
                        return true;
                    break;

                case "text":
                case "file":
                case "audio":
                    // Always show these content types
                    return true;

                default:
                    // Unknown block types are shown by default
                    return true;
            }
        }

        // No visible content blocks found
        return false;
    }

    /// <summary>
    /// Handles settings changes to refresh the filtered collection.
    /// </summary>
    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsProvider.ShowToolResults) ||
            e.PropertyName == nameof(SettingsProvider.ShowThinkingBlocks) ||
            e.PropertyName == nameof(SettingsProvider.ShowToolCalls))
        {
            Dispatcher.UIThread.Post(() => FilteredMessages.Refresh());
        }
    }

    /// <summary>
    /// Determines if a notification should be played for the given message.
    /// </summary>
    private bool ShouldPlayNotification(ChatMessage message)
    {
        // Only notify for assistant messages (not user's own messages)
        if (message.IsUser)
            return false;

        // Extract text content from message blocks
        var textContent = ExtractTextContent(message.Content);
        
        // Don't notify for HEARTBEAT_OK messages
        if (textContent?.Trim() == "HEARTBEAT_OK")
            return false;

        return true;
    }

    /// <summary>
    /// Extracts plain text content from message content blocks.
    /// </summary>
    private string? ExtractTextContent(IList content)
    {
        if (content == null || content.Count == 0)
            return null;

        var textParts = new List<string>();
        foreach (var block in content)
        {
            if (block is ContentBlock cb && cb.Type == "text" && !string.IsNullOrEmpty(cb.Text))
            {
                textParts.Add(cb.Text);
            }
        }

        return textParts.Count > 0 ? string.Join(" ", textParts) : null;
    }

    /// <summary>
    /// Buffers streaming content for later display when ShowStreamingMessages is disabled.
    /// </summary>
    private void BufferStreamingContent(string runId, ObservableCollection<ContentBlock> incoming)
    {
        if (!_streamingContentBuffer.TryGetValue(runId, out var buffered))
        {
            buffered = new ObservableCollection<ContentBlock>();
            _streamingContentBuffer[runId] = buffered;
        }

        // Merge incoming blocks into buffer (same logic as MergeContentBlocks)
        for (var i = 0; i < incoming.Count; i++)
        {
            var incomingBlock = incoming[i];

            if (i < buffered.Count)
            {
                var existingBlock = buffered[i];

                if (existingBlock.Type == incomingBlock.Type)
                {
                    if (existingBlock.Text != incomingBlock.Text)
                        existingBlock.Text = incomingBlock.Text;
                    if (existingBlock.Thinking != incomingBlock.Thinking)
                        existingBlock.Thinking = incomingBlock.Thinking;
                    if (existingBlock.ThinkingSignature != incomingBlock.ThinkingSignature)
                        existingBlock.ThinkingSignature = incomingBlock.ThinkingSignature;
                    if (existingBlock.Name != incomingBlock.Name)
                        existingBlock.Name = incomingBlock.Name;
                    if (!ReferenceEquals(existingBlock.Arguments, incomingBlock.Arguments))
                        existingBlock.Arguments = incomingBlock.Arguments;
                    if (existingBlock.Id != incomingBlock.Id)
                        existingBlock.Id = incomingBlock.Id;
                    if (existingBlock.MimeType != incomingBlock.MimeType)
                        existingBlock.MimeType = incomingBlock.MimeType;
                    if (existingBlock.FileName != incomingBlock.FileName)
                        existingBlock.FileName = incomingBlock.FileName;
                    if (!ReferenceEquals(existingBlock.Content, incomingBlock.Content))
                        existingBlock.Content = incomingBlock.Content;
                    if (existingBlock.Duration != incomingBlock.Duration)
                        existingBlock.Duration = incomingBlock.Duration;
                    if (existingBlock.Transcript != incomingBlock.Transcript)
                        existingBlock.Transcript = incomingBlock.Transcript;
                }
                else
                {
                    buffered[i] = incomingBlock;
                }
            }
            else
            {
                buffered.Add(incomingBlock);
            }
        }

        while (buffered.Count > incoming.Count)
        {
            buffered.RemoveAt(buffered.Count - 1);
        }
    }

    /// <summary>
    /// Creates content blocks for the typing indicator animation.
    /// </summary>
    private ObservableCollection<ContentBlock> CreateTypingIndicatorContent()
    {
        return new ObservableCollection<ContentBlock>
        {
            new ContentBlock { Type = "text", Text = "." }
        };
    }

    /// <summary>
    /// Updates the typing indicator text for a message.
    /// </summary>
    private void UpdateTypingIndicator(ChatMessage message)
    {
        var dots = _typingFrame switch
        {
            0 => ".",
            1 => "..",
            _ => "..."
        };

        if (message.Content.Count > 0 && message.Content[0].Type == "text")
        {
            message.Content[0].Text = dots;
        }
    }

    /// <summary>
    /// Starts the typing animation timer.
    /// </summary>
    private void StartTypingAnimation()
    {
        if (!_typingAnimationTimer.Enabled)
        {
            _typingFrame = 0;
            _typingAnimationTimer.Start();
        }
    }

    /// <summary>
    /// Stops the typing animation timer if no more streaming messages.
    /// </summary>
    private void StopTypingAnimationIfIdle()
    {
        if (_runIdToMessage.Count == 0)
        {
            _typingAnimationTimer.Stop();
        }
    }

    /// <summary>
    /// Handles the typing animation tick event.
    /// </summary>
    private void OnTypingAnimationTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _typingFrame = (_typingFrame + 1) % 3;

            // Update all messages that are currently streaming with typing indicators
            foreach (var kvp in _runIdToMessage)
            {
                if (_streamingContentBuffer.ContainsKey(kvp.Key))
                {
                    UpdateTypingIndicator(kvp.Value);
                }
            }
        });
    }

    /// <summary>
    /// Merge incoming content blocks into existing collection.
    /// Updates existing blocks (e.g., streaming text) instead of replacing the entire collection.
    /// This enables granular UI updates - only changed blocks re-render, not the entire message.
    /// CRITICAL: All ObservableCollection modifications must be on UI thread.
    /// </summary>
    private void MergeContentBlocks(ObservableCollection<ContentBlock> existing, ObservableCollection<ContentBlock> incoming)
    {
        // Strategy: Match blocks by type and position, update existing or add new
        for (var i = 0; i < incoming.Count; i++)
        {
            var incomingBlock = incoming[i];
            
            if (i < existing.Count)
            {
                var existingBlock = existing[i];
                
                // If same type, update properties (triggers granular UI update)
                if (existingBlock.Type == incomingBlock.Type)
                {
                    // Update only changed properties
                    if (existingBlock.Text != incomingBlock.Text)
                        existingBlock.Text = incomingBlock.Text;
                    if (existingBlock.Thinking != incomingBlock.Thinking)
                        existingBlock.Thinking = incomingBlock.Thinking;
                    if (existingBlock.ThinkingSignature != incomingBlock.ThinkingSignature)
                        existingBlock.ThinkingSignature = incomingBlock.ThinkingSignature;
                    if (existingBlock.Name != incomingBlock.Name)
                        existingBlock.Name = incomingBlock.Name;
                    if (!ReferenceEquals(existingBlock.Arguments, incomingBlock.Arguments))
                        existingBlock.Arguments = incomingBlock.Arguments;
                    if (existingBlock.Id != incomingBlock.Id)
                        existingBlock.Id = incomingBlock.Id;
                    if (existingBlock.MimeType != incomingBlock.MimeType)
                        existingBlock.MimeType = incomingBlock.MimeType;
                    if (existingBlock.FileName != incomingBlock.FileName)
                        existingBlock.FileName = incomingBlock.FileName;
                    if (!ReferenceEquals(existingBlock.Content, incomingBlock.Content))
                        existingBlock.Content = incomingBlock.Content;
                    if (existingBlock.Duration != incomingBlock.Duration)
                        existingBlock.Duration = incomingBlock.Duration;
                    if (existingBlock.Transcript != incomingBlock.Transcript)
                        existingBlock.Transcript = incomingBlock.Transcript;
                }
                else
                {
                    // Different type at same position - replace
                    existing[i] = incomingBlock;
                }
            }
            else
            {
                // New block - add to collection
                existing.Add(incomingBlock);
            }
        }
        
        // Remove excess blocks if incoming has fewer
        while (existing.Count > incoming.Count)
        {
            existing.RemoveAt(existing.Count - 1);
        }
    }

    /// <summary>
    /// Processes a message by adding it to the Messages collection.
    /// Must be called on the UI thread.
    /// </summary>
    private void ProcessMessage(string sessionKey, string? runId, string? state, ChatMessage message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var isLegacy = string.IsNullOrEmpty(runId) || string.IsNullOrEmpty(state);

            if (isLegacy)
            {
                Messages.Add(message);
            }
            else
            {
                // Check if streaming is disabled and message is still streaming
                var showStreaming = SettingsProvider.Instance.ShowStreamingMessages;
                var isStreaming = state is not "final" and not "aborted" and not "error";

                if (_runIdToMessage.TryGetValue(runId!, out var existing))
                {
                    if (!showStreaming && isStreaming)
                    {
                        // Buffer the content, don't show it yet
                        BufferStreamingContent(runId!, message.Content);
                        UpdateTypingIndicator(existing);
                    }
                    else
                    {
                        // Normal flow: merge content blocks
                        // If we were buffering, flush the buffer first
                        if (_streamingContentBuffer.TryGetValue(runId!, out var buffered))
                        {
                            MergeContentBlocks(existing.Content, buffered);
                            _streamingContentBuffer.Remove(runId!);
                            StopTypingAnimationIfIdle();
                        }
                        MergeContentBlocks(existing.Content, message.Content);
                    }

                    existing.Timestamp = message.Timestamp;
                    existing.IsStreaming = isStreaming;

                    if (!isStreaming)
                    {
                        _runIdToMessage.Remove(runId!);
                        _streamingContentBuffer.Remove(runId!);
                        StopTypingAnimationIfIdle();
                    }
                }
                else
                {
                    // New message
                    if (!showStreaming && isStreaming)
                    {
                        // Start buffering and show typing indicator
                        _streamingContentBuffer[runId!] = new ObservableCollection<ContentBlock>();
                        BufferStreamingContent(runId!, message.Content);

                        // Replace content with typing indicator
                        var typingMessage = new ChatMessage
                        {
                            Role = message.Role,
                            Content = CreateTypingIndicatorContent(),
                            Timestamp = message.Timestamp,
                            IsStreaming = true
                        };
                        Messages.Add(typingMessage);
                        _runIdToMessage[runId!] = typingMessage;
                        StartTypingAnimation();
                    }
                    else
                    {
                        Messages.Add(message);
                        if (isStreaming)
                        {
                            message.IsStreaming = true;
                            _runIdToMessage[runId!] = message;
                        }
                    }
                }
            }
        });
    }

    /// <summary>
    /// Called when the app enters the foreground.
    /// Flushes any messages that were queued while backgrounded.
    /// </summary>
    private void OnForegroundEntered(object? sender, EventArgs e)
    {
        if (_backgroundMessageQueue.IsEmpty)
            return;

        _logger.LogInformation("App entering foreground, flushing {Count} queued messages", _backgroundMessageQueue.Count);

        Dispatcher.UIThread.Post(() =>
        {
            int processedCount = 0;
            while (_backgroundMessageQueue.TryDequeue(out var bgMessage))
            {
                // Only process messages for the currently selected session
                if (bgMessage.SessionKey == SessionList.SelectedSession?.Key)
                {
                    ProcessMessage(bgMessage.SessionKey, bgMessage.RunId, bgMessage.State, bgMessage.Message);
                    processedCount++;
                }
            }

            if (processedCount > 0)
            {
                _logger.LogInformation("Flushed {ProcessedCount} messages to UI", processedCount);
            }
        });
    }

    /// <summary>
    /// Disposes resources used by the view model.
    /// </summary>
    public void Dispose()
    {
        _typingAnimationTimer?.Stop();
        _typingAnimationTimer?.Dispose();

        if (_lifecycleService != null)
        {
            _lifecycleService.ForegroundEntered -= OnForegroundEntered;
        }
    }
}
