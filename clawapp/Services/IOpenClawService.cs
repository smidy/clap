using clawapp.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace clawapp.Services;

/// <summary>
/// OpenClaw gateway WebSocket communication.
/// </summary>
public interface IOpenClawService
{
    bool IsConnected { get; }

    /// <summary>Current connection state of the service.</summary>
    ConnectionState ConnectionState { get; }

    /// <summary>Number of messages currently in the offline queue.</summary>
    int OfflineQueueCount { get; }

    /// <summary>Current reconnect attempt number (0 if not reconnecting).</summary>
    int CurrentReconnectAttempt { get; }

    event EventHandler? OnConnected;
    event EventHandler? OnDisconnected;
    event EventHandler<string>? OnError;
    event EventHandler<(string SessionKey, string? RunId, string? State, ChatMessage Message)>? OnMessageReceived;

    /// <summary>Fired when the connection state changes.</summary>
    event EventHandler<ConnectionState>? OnConnectionStateChanged;

    /// <summary>Fired when a reconnect attempt is made. Argument is the attempt number.</summary>
    event EventHandler<int>? OnReconnectAttempt;

    /// <summary>Fired when successfully reconnected after a disconnection.</summary>
    event EventHandler? OnReconnected;

    /// <summary>Fired when the offline message queue has been emptied.</summary>
    event EventHandler? OnOfflineQueueEmpty;

    /// <summary>
    /// Connects to the OpenClaw gateway.
    /// </summary>
    /// <param name="host">The hostname or IP address of the gateway.</param>
    /// <param name="port">The port number. If null, no port is appended (uses default 80/443).</param>
    /// <param name="token">Optional authentication token.</param>
    /// <param name="useSecureWebSocket">Whether to use wss:// instead of ws://. Enable for Tailscale serve HTTPS endpoints.</param>
    /// <param name="cancellationToken">Cancellation token for the connection attempt.</param>
    Task ConnectAsync(string host, int? port, string token, bool useSecureWebSocket = false, CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels any pending reconnection attempts. Call this when manually disconnecting.
    /// </summary>
    Task CancelReconnectionAsync();

    Task SendMessageAsync(string sessionKey, string message, CancellationToken cancellationToken = default);
    Task SendMessageWithAttachmentsAsync(string sessionKey, string message, IReadOnlyList<PendingAttachment> attachments, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatMessage>> GetHistoryAsync(string sessionKey, int limit = 50, CancellationToken cancellationToken = default);
    Task SubscribeToSessionAsync(string sessionKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Session>> GetSessionsAsync(int limit = 20, CancellationToken cancellationToken = default);
    Task<bool> RegisterPushTokenAsync(string pushToken, string pushPlatform, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a pending file attachment to be sent with a message.
/// </summary>
public record PendingAttachment
{
    public string FileName { get; init; } = "";
    public string MimeType { get; init; } = "";
    public byte[] Data { get; init; } = Array.Empty<byte>();
    public long Size => Data.Length;
}
