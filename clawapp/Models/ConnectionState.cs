using System;
using System.Collections.Generic;
using clawapp.Services;

namespace clawapp.Models;

/// <summary>
/// Represents the current state of the WebSocket connection.
/// </summary>
public enum ConnectionState
{
    /// <summary>Not connected to the gateway.</summary>
    Disconnected,
    
    /// <summary>Currently attempting to connect.</summary>
    Connecting,
    
    /// <summary>Successfully connected and authenticated.</summary>
    Connected,
    
    /// <summary>Connection lost, attempting to reconnect.</summary>
    Reconnecting,
    
    /// <summary>Connection was explicitly closed by the user.</summary>
    Disconnecting
}

/// <summary>
/// Represents a message queued for sending when the connection is restored.
/// </summary>
public record QueuedMessage
{
    /// <summary>The session key to send the message to.</summary>
    public string SessionKey { get; init; } = "";
    
    /// <summary>The text content of the message.</summary>
    public string Text { get; init; } = "";
    
    /// <summary>Optional file attachments to send with the message.</summary>
    public IReadOnlyList<PendingAttachment>? Attachments { get; init; }
    
    /// <summary>When the message was queued.</summary>
    public DateTimeOffset QueuedAt { get; init; } = DateTimeOffset.UtcNow;
    
    /// <summary>Number of send attempts made.</summary>
    public int AttemptCount { get; set; }
    
    /// <summary>Unique identifier for this queued message.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();
}
