using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using clawapp.Models;

namespace clawapp.Services;

/// <summary>
/// OpenClaw gateway WebSocket protocol implementation with Polly-based resilience.
/// </summary>
public sealed class OpenClawService : IOpenClawService
{
    private const int ProtocolVersion = 3;
    /// <summary>Gateway schema requires exact client id. Use "openclaw-control-ui" for operator/chat clients.</summary>
    private const string ClientId = "openclaw-control-ui";
    private const string ClientMode = "ui";
    private const string ClientVersion = "1.0.0";

    private readonly ILogger<OpenClawService> _logger;
    private readonly DeviceIdentityStoreBouncyCastle _identityStore = new();
    private DeviceIdentityBouncyCastle? _identity;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pending = new();
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _receiveCts;
    private CancellationTokenSource? _pingCts;
    private Task? _receiveTask;
    private Task? _pingTask;
    private int _tickIntervalMs = 15000;
    private string _lastChallengeNonce = "";
    private long _lastChallengeTs;
    private TaskCompletionSource<bool>? _firstChallengeTcs;

    // Resilience fields
    private readonly ConcurrentQueue<Models.QueuedMessage> _offlineQueue = new();
    private string? _lastHost;
    private int _lastPort;
    private string? _lastToken;
    private bool _lastUseSecureWebSocket;
    private bool _isReconnecting;
    private readonly SemaphoreSlim _reconnectLock = new(1, 1);
    private CancellationTokenSource? _connectionCts;

    // Connection state tracking
    private Models.ConnectionState _connectionState = Models.ConnectionState.Disconnected;
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private DateTimeOffset _lastConnectedTime = DateTimeOffset.MinValue;

    // Polly retry policy configuration
    private const int MaxReconnectAttempts = 5;
    private const int MaxReconnectDelaySeconds = 5;
    private const int JitterMilliseconds = 500;
    private const int ConnectionAttemptTimeoutSeconds = 10;

    public Models.ConnectionState ConnectionState
    {
        get => _connectionState;
        private set
        {
            if (_connectionState != value)
            {
                _connectionState = value;
                _logger.LogDebug("Connection state changed to {State}", value);
            }
        }
    }

    public int OfflineQueueCount => _offlineQueue.Count;
    public int CurrentReconnectAttempt { get; private set; }

    public event EventHandler<Models.ConnectionState>? OnConnectionStateChanged;
    public event EventHandler<int>? OnReconnectAttempt;
    public event EventHandler? OnReconnected;
    public event EventHandler? OnOfflineQueueEmpty;

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    public event EventHandler? OnConnected;
    public event EventHandler? OnDisconnected;
    public event EventHandler<string>? OnError;
    public event EventHandler<(string SessionKey, string? RunId, string? State, ChatMessage Message)>? OnMessageReceived;

    private readonly IPushNotificationService _pushService;

    public OpenClawService(ILogger<OpenClawService> logger, IPushNotificationService pushService)
    {
        _logger = logger;
        _pushService = pushService;
    }

    /// <summary>
    /// Creates a Polly retry policy for reconnection attempts with exponential backoff and jitter.
    /// </summary>
    private IAsyncPolicy CreateReconnectPolicy(CancellationToken userToken)
    {
        return Policy
            .Handle<Exception>() // WebSocketException, IOException, TaskCanceledException, etc.
            .WaitAndRetryAsync(
                retryCount: MaxReconnectAttempts,
                sleepDurationProvider: attempt =>
                {
                    // Exponential backoff: 1s, 2s, 4s, 8s, 16s, then cap at 30s
                    var baseDelay = TimeSpan.FromSeconds(
                        Math.Min(MaxReconnectDelaySeconds, Math.Pow(2, attempt - 1)));

                    // Add jitter (±500ms) to prevent thundering herd
                    var jitter = TimeSpan.FromMilliseconds(
                        Random.Shared.Next(-JitterMilliseconds, JitterMilliseconds));

                    var totalDelay = baseDelay + jitter;

                    _logger.LogDebug(
                        "Calculated retry delay for attempt {Attempt}: base={BaseDelay}ms, jitter={Jitter}ms, total={Total}ms",
                        attempt, baseDelay.TotalMilliseconds, jitter.TotalMilliseconds, totalDelay.TotalMilliseconds);

                    return totalDelay;
                },
                onRetry: (exception, delay, attempt, context) =>
                {
                    CurrentReconnectAttempt = attempt;

                    // Log full exception with stack trace
                    _logger.LogWarning(
                        exception,
                        "Reconnection attempt {Attempt}/{MaxAttempts} failed. Retrying in {Delay}ms. Exception type: {ExceptionType}",
                        attempt, MaxReconnectAttempts, delay.TotalMilliseconds, exception.GetType().Name);

                    // Fire event for UI updates
                    OnReconnectAttempt?.Invoke(this, attempt);
                });
    }

    public async Task ConnectAsync(string host, int? port, string token, bool useSecureWebSocket = false, CancellationToken cancellationToken = default)
    {
        // Store connection details for reconnection
        _lastHost = host;
        _lastPort = port ?? 0; // Store 0 to indicate no port specified
        _lastToken = token;
        _lastUseSecureWebSocket = useSecureWebSocket;

        // Cancel any existing connection attempts
        _connectionCts?.Cancel();
        _connectionCts?.Dispose();

        // Create fresh CTS for this connection lifecycle, linked to user token
        _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await DisconnectAsync(cancellationToken, preserveQueue: true).ConfigureAwait(false);

        await SetConnectionStateAsync(Models.ConnectionState.Connecting, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Connecting to {Host}:{Port} (Secure={Secure})", host, port, useSecureWebSocket);

        // Load or create device identity
        _identity = _identityStore.LoadOrCreate();

        // Build WebSocket URI - only append port if specified
        var scheme = useSecureWebSocket ? "wss" : "ws";
        var uriString = port.HasValue && port.Value > 0
            ? $"{scheme}://{host}:{port.Value}"
            : $"{scheme}://{host}";
        var uri = new Uri(uriString);

        _webSocket = new ClientWebSocket();

        // Set Origin header to match the scheme - only append port if specified
        var originScheme = useSecureWebSocket ? "https" : "http";
        var originString = port.HasValue && port.Value > 0
            ? $"{originScheme}://{host}:{port.Value}"
            : $"{originScheme}://{host}";
        _webSocket.Options.SetRequestHeader("Origin", originString);

        await _webSocket.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);

        _receiveCts = new CancellationTokenSource();
        _firstChallengeTcs = new TaskCompletionSource<bool>();
        _receiveTask = ReceiveLoopAsync(_receiveCts.Token);

        // Wait for connect.challenge (optional) or timeout before sending connect
        await Task.WhenAny(_firstChallengeTcs.Task, Task.Delay(2000, cancellationToken)).ConfigureAwait(false);
        _firstChallengeTcs = null;

        var platform = "desktop";
        if (OperatingSystem.IsAndroid()) platform = "android";
        else if (OperatingSystem.IsIOS()) platform = "ios";
        else if (OperatingSystem.IsMacOS()) platform = "macos";
        else if (OperatingSystem.IsWindows()) platform = "windows";
        else if (OperatingSystem.IsLinux()) platform = "linux";
        else if (OperatingSystem.IsBrowser()) platform = "browser";

        var nonce = _lastChallengeNonce;
        var signedAtMs = _lastChallengeTs > 0 ? _lastChallengeTs : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Build and sign auth payload
        var scopes = new[] { "operator.read", "operator.write", "operator.admin" };
        var authPayload = _identityStore.BuildAuthPayload(
            deviceId: _identity.DeviceId,
            clientId: ClientId,
            clientMode: ClientMode,
            role: "operator",
            scopes: scopes,
            signedAtMs: signedAtMs,
            token: token,
            nonce: nonce
        );

        var signature = _identityStore.SignPayload(authPayload, _identity);
        var publicKey = _identityStore.PublicKeyBase64Url(_identity);

        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(publicKey))
        {
            _logger.LogCritical("Failed to sign device auth payload.");
            throw new InvalidOperationException("Failed to sign device auth payload.");
        }

        var connectParams = new
        {
            minProtocol = ProtocolVersion,
            maxProtocol = ProtocolVersion,
            client = new { id = ClientId, version = ClientVersion, platform, mode = ClientMode },
            role = "operator",
            scopes,
            caps = Array.Empty<string>(),
            commands = Array.Empty<string>(),
            permissions = new { },
            auth = new { token = token ?? "" },
            locale = "en-US",
            userAgent = $"clawapp-dotnet/{ClientVersion}",
            device = new
            {
                id = _identity.DeviceId,
                publicKey,
                signature,
                signedAt = signedAtMs,
                nonce
            }
        };

        var connectId = NewId();
        var connectPayload = new { type = "req", id = connectId, method = "connect", @params = connectParams };
        var helloJson = await SendRequestAsync(connectPayload, connectId, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(helloJson))
            throw new InvalidOperationException("Connect failed: no response.");

        var response = JsonSerializer.Deserialize<ResponseFrame>(helloJson, JsonOptions);
        if (response == null || !response.Ok)
        {
            var errMsg = response?.Error?.Message ?? "Connect failed.";
            _logger.LogError("Connect failed: {Error}", errMsg);
            throw new InvalidOperationException(errMsg);
        }

        if (response.Payload.HasValue)
        {
            var helloPayload = JsonSerializer.Deserialize<HelloOkPayload>(response.Payload.Value.GetRawText(), JsonOptions);
            if (helloPayload?.Policy != null)
                _tickIntervalMs = helloPayload.Policy.TickIntervalMs;
        }

        _pingCts = new CancellationTokenSource();
        _pingTask = PingLoopAsync(_pingCts.Token);

        _lastConnectedTime = DateTimeOffset.UtcNow;
        CurrentReconnectAttempt = 0;
        await SetConnectionStateAsync(Models.ConnectionState.Connected, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Connected to gateway. Tick interval: {TickIntervalMs}ms", _tickIntervalMs);
        OnConnected?.Invoke(this, EventArgs.Empty);

        // Register push notification token (Android/iOS only)
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _pushService.InitializeAsync(cancellationToken);
                    var token = await _pushService.GetTokenAsync(cancellationToken);
                    if (!string.IsNullOrEmpty(token))
                    {
                        await RegisterPushTokenAsync(token, _pushService.Platform, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to register push token");
                }
            }, CancellationToken.None);
        }

        // Process any queued messages from offline period
        _ = Task.Run(() => ProcessOfflineQueueAsync(_connectionCts.Token), CancellationToken.None);
    }

    private async Task SetConnectionStateAsync(ConnectionState newState, CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ConnectionState = newState;
            OnConnectionStateChanged?.Invoke(this, newState);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await DisconnectAsync(cancellationToken, false).ConfigureAwait(false);
    }

    private async Task DisconnectAsync(CancellationToken cancellationToken, bool preserveQueue, bool isReconnect = false)
    {
        _logger.LogInformation("Disconnecting from gateway (preserveQueue={PreserveQueue}, isReconnect={IsReconnect})...",
            preserveQueue, isReconnect);

        _pingCts?.Cancel();
        _receiveCts?.Cancel();
        if (_pingTask != null) await _pingTask.ConfigureAwait(false);
        if (_receiveTask != null) await _receiveTask.ConfigureAwait(false);
        _pingTask = null;
        _receiveTask = null;

        if (_webSocket?.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing WebSocket during disconnect.");
            }
        }

        _webSocket?.Dispose();
        _webSocket = null;
        foreach (var tcs in _pending.Values)
            tcs.TrySetCanceled(cancellationToken);
        _pending.Clear();

        if (!preserveQueue)
        {
            _offlineQueue.Clear();
        }

        if (!isReconnect)
        {
            await SetConnectionStateAsync(Models.ConnectionState.Disconnected, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation("Disconnected from gateway.");
        OnDisconnected?.Invoke(this, EventArgs.Empty);
    }

    public async Task SendMessageAsync(string sessionKey, string message, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            _logger.LogInformation("Not connected. Queuing message for session {SessionKey}.", sessionKey);
            _offlineQueue.Enqueue(new Models.QueuedMessage
            {
                SessionKey = sessionKey,
                Text = message,
                Attachments = null
            });
            return;
        }

        var request = RequestFrame<ChatSendParams>.Create(
            id: NewId(),
            method: "chat.send",
            @params: new ChatSendParams
            {
                SessionKey = sessionKey,
                Message = message,
                Deliver = false,
                IdempotencyKey = Guid.NewGuid().ToString()
            }
        );

        var resJson = await SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
        CheckResponseError(resJson, "Send message failed");
    }

    private void CheckResponseError(string? resJson, string errorContext)
    {
        if (!string.IsNullOrEmpty(resJson))
        {
            var response = JsonSerializer.Deserialize<ResponseFrame>(resJson, JsonOptions);
            if (response != null && !response.Ok)
            {
                var errMsg = response.Error?.Message ?? $"{errorContext}.";
                _logger.LogError("{Context}: {Error}", errorContext, errMsg);
                // We might want to surface this via OnError if it happens in background
                if (response != null && !response.Ok) OnError?.Invoke(this, $"{errorContext}: {errMsg}");
            }
        }
    }

    public async Task SendMessageWithAttachmentsAsync(string sessionKey, string message, IReadOnlyList<PendingAttachment> attachments, CancellationToken cancellationToken = default)
    {
        var attachmentParams = attachments.Select(a => new AttachmentParams
        {
            MimeType = a.MimeType,
            Content = Convert.ToBase64String(a.Data),
            FileName = a.FileName
        }).ToArray();

        var request = RequestFrame<ChatSendWithAttachmentsParams>.Create(
            id: NewId(),
            method: "chat.send",
            @params: new ChatSendWithAttachmentsParams
            {
                SessionKey = sessionKey,
                Message = message,
                Deliver = false,
                IdempotencyKey = Guid.NewGuid().ToString(),
                Attachments = attachmentParams
            }
        );

        if (!IsConnected)
        {
            _logger.LogInformation("Not connected. Queuing message with attachments for session {SessionKey}.", sessionKey);
            _offlineQueue.Enqueue(new Models.QueuedMessage
            {
                SessionKey = sessionKey,
                Text = message,
                Attachments = attachments.ToList()
            });
            return;
        }

        var resJson = await SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
        CheckResponseError(resJson, "Send message with attachments failed");
    }

    public async Task<IReadOnlyList<ChatMessage>> GetHistoryAsync(string sessionKey, int limit = 50, CancellationToken cancellationToken = default)
    {
        var request = RequestFrame<ChatHistoryParams>.Create(
            id: NewId(),
            method: "chat.history",
            @params: new ChatHistoryParams { SessionKey = sessionKey, Limit = limit }
        );
        var resJson = await SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(resJson))
            return Array.Empty<ChatMessage>();

        var response = JsonSerializer.Deserialize<ResponseFrame>(resJson, JsonOptions);
        if (response == null || !response.Ok || !response.Payload.HasValue)
            return Array.Empty<ChatMessage>();

        var historyPayload = JsonSerializer.Deserialize<ChatHistoryPayload>(response.Payload.Value.GetRawText(), JsonOptions);
        if (historyPayload?.Messages == null)
            return Array.Empty<ChatMessage>();

        // Map ChatMessageDto to ChatMessage
        return historyPayload.Messages.Select(MapChatMessage).ToList();
    }

    public async Task SubscribeToSessionAsync(string sessionKey, CancellationToken cancellationToken = default)
    {
        var request = RequestFrame<ChatSubscribeParams>.Create(
            id: NewId(),
            method: "chat.subscribe",
            @params: new ChatSubscribeParams { SessionKey = sessionKey }
        );
        await SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Session>> GetSessionsAsync(int limit = 20, CancellationToken cancellationToken = default)
    {
        var request = RequestFrame<SessionsListParams>.Create(
            id: NewId(),
            method: "sessions.list",
            @params: new SessionsListParams { Limit = limit }
        );
        var resJson = await SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(resJson))
            return Array.Empty<Session>();

        var response = JsonSerializer.Deserialize<ResponseFrame>(resJson, JsonOptions);
        if (response == null || !response.Ok || !response.Payload.HasValue)
            return Array.Empty<Session>();

        var sessionsPayload = JsonSerializer.Deserialize<SessionsListPayload>(response.Payload.Value.GetRawText(), JsonOptions);
        if (sessionsPayload?.Sessions == null)
            return Array.Empty<Session>();

        // Map SessionDto to Session
        return sessionsPayload.Sessions.Select(dto => new Session
        {
            Key = dto.Key,
            Name = dto.Name ?? "",
            LastActivity = dto.LastActivity
        }).ToList();
    }

    public async Task<bool> RegisterPushTokenAsync(string pushToken, string pushPlatform, CancellationToken cancellationToken = default)
    {
        var request = RequestFrame<DevicePushRegisterParams>.Create(
            id: NewId(),
            method: "device.push.register",
            @params: new DevicePushRegisterParams
            {
                PushToken = pushToken,
                PushPlatform = pushPlatform
            }
        );

        var resJson = await SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(resJson))
            return false;

        var response = JsonSerializer.Deserialize<ResponseFrame>(resJson, JsonOptions);
        if (response == null || !response.Ok)
        {
            _logger.LogError("Push token registration failed: {Error}", response?.Error?.Message ?? "Unknown error");
            return false;
        }

        _logger.LogInformation("Push token registered successfully");
        return true;
    }

    private static string NewId() => Guid.NewGuid().ToString();

    private static ChatMessage MapChatMessage(ChatMessageDto dto)
    {
        return new ChatMessage
        {
            Role = dto.Role,
            Content = new ObservableCollection<ContentBlock>(
                dto.Content?.Select(c => new ContentBlock
                {
                    Type = c.Type,
                    Text = c.Text,
                    Thinking = c.Thinking,
                    ThinkingSignature = c.ThinkingSignature,
                    MimeType = c.MimeType,
                    FileName = c.FileName,
                    Content = c.Content,
                    Id = c.Id,
                    Name = c.Name,
                    Arguments = c.Arguments
                }) ?? Enumerable.Empty<ContentBlock>()),
            Timestamp = dto.Timestamp,
            ToolCallId = dto.ToolCallId ?? dto.Tool_call_id,
            ToolName = dto.ToolName ?? dto.Tool_name,
            Usage = dto.Usage != null ? new UsageInfo
            {
                Input = dto.Usage.Input,
                Output = dto.Usage.Output,
                CacheRead = dto.Usage.CacheRead,
                CacheWrite = dto.Usage.CacheWrite,
                Cost = dto.Usage.Cost != null ? new UsageCost
                {
                    Input = dto.Usage.Cost.Input,
                    Output = dto.Usage.Cost.Output,
                    CacheRead = dto.Usage.Cost.CacheRead,
                    CacheWrite = dto.Usage.Cost.CacheWrite,
                    Total = dto.Usage.Cost.Total
                } : null,
                Total = dto.Usage.Total ?? dto.Usage.TotalTokens
            } : null,
            StopReason = dto.StopReason
        };
    }

    private async Task<string?> SendRequestAsync<TParams>(RequestFrame<TParams> request, CancellationToken cancellationToken)
    {
        if (_webSocket?.State != WebSocketState.Open)
            throw new InvalidOperationException("Not connected.");

        var json = JsonSerializer.Serialize(request, JsonOptions);
        var tcs = new TaskCompletionSource<string>();
        _pending[request.Id] = tcs;

        _logger.LogDebug("Sending request {Method} {Id} json : {Json}", request.Method, request.Id, json);

        try
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(30));
            return await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        }
        finally
        {
            _pending.TryRemove(request.Id, out _);
        }
    }

    private async Task<string?> SendRequestAsync(object payload, string id, CancellationToken cancellationToken)
    {
        if (_webSocket?.State != WebSocketState.Open)
            throw new InvalidOperationException("Not connected.");

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var tcs = new TaskCompletionSource<string>();
        _pending[id] = tcs;

        _logger.LogDebug("Sending connect request {Json}", json);

        try
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(30));
            return await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var segment = new ArraySegment<byte>(buffer);
        StringBuilder stringBuilder = new StringBuilder();

        try
        {
            while (_webSocket?.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await _webSocket.ReceiveAsync(segment, cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("WebSocket closed by server. Scheduling reconnection...");
                    ScheduleReconnect();
                    break;
                }

                stringBuilder.Append(Encoding.UTF8.GetString(buffer.AsSpan(0, result.Count)));

                if (result.EndOfMessage)
                {
                    var text = stringBuilder.ToString();
                    HandleFrame(text, isFirst: _firstChallengeTcs != null);
                    stringBuilder.Clear();
                }

            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogTrace("Receive loop cancelled.");
        }
        catch (WebSocketException wsEx)
        {
            _logger.LogError(wsEx, "WebSocket error in receive loop. Error code: {ErrorCode}, Message: {Message}",
                wsEx.WebSocketErrorCode, wsEx.Message);
            OnError?.Invoke(this, $"WebSocket error: {wsEx.Message}");
            ScheduleReconnect();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Receive loop error. Exception type: {ExceptionType}, Message: {Message}",
                ex.GetType().Name, ex.Message);
            OnError?.Invoke(this, ex.Message);
            ScheduleReconnect();
        }
    }

    private void HandleFrame(string json, bool isFirst = false)
    {
        try
        {
            if (isFirst)
                _firstChallengeTcs?.TrySetResult(true);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl))
                return;

            var type = typeEl.GetString() ?? "";

            switch (type)
            {
                case "event":
                    HandleEvent(json, root);
                    break;
                case "res":
                    if (root.TryGetProperty("id", out var idEl))
                    {
                        var id = idEl.GetString() ?? "";
                        if (_pending.TryGetValue(id, out var tcs))
                            tcs.TrySetResult(json);
                    }
                    else
                    {
                        _logger.LogDebug("Unable to retrieve id from Payload: {Payload}", json);
                    }
                        break;
                case "pong":
                    _logger.LogTrace("Received pong.");
                    break;
                default:
                    _logger.LogDebug("Unhandled frame Payload: {Payload}", json);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Frame parse error. Payload length: {Length}", json?.Length ?? 0);
        }
    }

    private void HandleEvent(string json, JsonElement root)
    {
        if (!root.TryGetProperty("event", out var evEl))
            return;

        var eventName = evEl.GetString() ?? "";

        switch (eventName)
        {
            case "connect.challenge":
                if (root.TryGetProperty("payload", out var challengeEl))
                {
                    var challenge = JsonSerializer.Deserialize<ConnectChallengePayload>(challengeEl.GetRawText(), JsonOptions);
                    if (challenge != null)
                    {
                        _lastChallengeNonce = challenge.Nonce;
                        _lastChallengeTs = challenge.Ts;
                        _logger.LogDebug("Received connect.challenge: nonce set, ts={Ts}", challenge.Ts);
                    }
                }
                break;

            case "chat":
                if (root.TryGetProperty("payload", out var chatEl))
                {
                    var chatPayload = JsonSerializer.Deserialize<ChatEventPayload>(chatEl.GetRawText(), JsonOptions);
                    if (chatPayload != null)
                    {
                        var message = chatPayload.Message != null
                            ? MapChatMessage(chatPayload.Message)
                            : new ChatMessage { Role = "assistant", Content = new ObservableCollection<ContentBlock>() };
                        _logger.LogDebug("Chat event for session {SessionKey} runId={RunId} state={State}", chatPayload.SessionKey, chatPayload.RunId, chatPayload.State);
                        OnMessageReceived?.Invoke(this, (chatPayload.SessionKey, chatPayload.RunId, chatPayload.State, message));
                    }
                }
                break;
        }
    }

    private async Task PingLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_tickIntervalMs, cancellationToken).ConfigureAwait(false);
                if (_webSocket?.State == WebSocketState.Open)
                {
                    _logger.LogTrace("Sending ping.");
                    var json = """{"type":"ping"}""";
                    var bytes = Encoding.UTF8.GetBytes(json);
                    await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogTrace("Ping loop cancelled.");
        }
    }

    private void ScheduleReconnect()
    {
        if (_isReconnecting) return;

        // Don't reconnect if we don't have connection details
        if (_lastHost == null) return;

        // Don't reconnect if user explicitly cancelled
        if (_connectionCts?.IsCancellationRequested == true)
        {
            _logger.LogInformation("Skipping reconnection because connection was explicitly cancelled.");
            return;
        }

        Task.Run(async () => await ExecuteReconnectionAsync());
    }

    private async Task ExecuteReconnectionAsync()
    {
        if (!await _reconnectLock.WaitAsync(0)) return; // Already running

        try
        {
            _isReconnecting = true;
            await SetConnectionStateAsync(Models.ConnectionState.Reconnecting);
            _logger.LogInformation("Starting reconnection process using Polly retry policy...");

            // Create a fresh token for this reconnection cycle with a per-attempt timeout
            // This is critical: we don't reuse the potentially cancelled _connectionCts
            var reconnectCts = new CancellationTokenSource();
            var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(ConnectionAttemptTimeoutSeconds));
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(reconnectCts.Token, timeoutCts.Token);

            try
            {
                var reconnectPolicy = CreateReconnectPolicy(reconnectCts.Token);

                await reconnectPolicy.ExecuteAsync(async token =>
                {
                    _logger.LogInformation("Executing connection attempt...");
                    // Convert stored port (0 means null)
                    int? reconnectPort = _lastPort > 0 ? _lastPort : null;
                    await ConnectAsync(_lastHost!, reconnectPort, _lastToken!, _lastUseSecureWebSocket, token);
                }, linkedCts.Token);

                // Success!
                _logger.LogInformation("Reconnection successful after {Attempt} attempts!", CurrentReconnectAttempt);
                OnReconnected?.Invoke(this, EventArgs.Empty);
            }
            catch (OperationCanceledException) when (reconnectCts.IsCancellationRequested || timeoutCts.IsCancellationRequested)
            {
                _logger.LogInformation("Reconnection was cancelled or timed out.");
                await SetConnectionStateAsync(Models.ConnectionState.Disconnected);
            }
            catch (Exception ex)
            {
                // All retry attempts exhausted
                _logger.LogError(ex, "All reconnection attempts failed. Giving up after {MaxAttempts} attempts.", MaxReconnectAttempts);
                await SetConnectionStateAsync(Models.ConnectionState.Disconnected);
                OnError?.Invoke(this, $"Reconnection failed after {MaxReconnectAttempts} attempts: {ex.Message}");
            }
            finally
            {
                linkedCts.Dispose();
                timeoutCts.Dispose();
                reconnectCts.Dispose();
            }
        }
        finally
        {
            _isReconnecting = false;
            _reconnectLock.Release();
        }
    }

    public async Task CancelReconnectionAsync()
    {
        _logger.LogInformation("Cancelling reconnection attempts...");

        // Cancel the connection CTS to stop any ongoing connection attempts
        _connectionCts?.Cancel();

        // Wait briefly for reconnection loop to exit
        if (!await _reconnectLock.WaitAsync(TimeSpan.FromSeconds(2)))
        {
            _logger.LogWarning("Could not acquire reconnect lock for cancellation");
        }
        else
        {
            _reconnectLock.Release();
        }

        CurrentReconnectAttempt = 0;
        await SetConnectionStateAsync(Models.ConnectionState.Disconnected);
        _logger.LogInformation("Reconnection cancelled successfully.");
    }

    private async Task ProcessOfflineQueueAsync(CancellationToken cancellationToken = default)
    {
        if (_offlineQueue.IsEmpty) return;

        _logger.LogInformation("Processing offline queue ({Count} items)...", _offlineQueue.Count);

        int processedCount = 0;
        int errorCount = 0;

        while (_offlineQueue.TryPeek(out var queuedMessage))
        {
            if (cancellationToken.IsCancellationRequested || !IsConnected)
            {
                _logger.LogInformation("Offline queue processing paused (cancelled or disconnected). {Processed} sent, {Errors} errors.",
                    processedCount, errorCount);
                return;
            }

            try
            {
                // Dequeue and send
                if (!_offlineQueue.TryDequeue(out var message))
                    continue;

                message.AttemptCount++;

                // Send the message
                if (message.Attachments?.Count > 0)
                {
                    await SendMessageWithAttachmentsAsync(message.SessionKey, message.Text, message.Attachments, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await SendMessageAsync(message.SessionKey, message.Text, cancellationToken).ConfigureAwait(false);
                }

                processedCount++;
                _logger.LogDebug("Sent queued message {MessageId} for session {SessionKey}", message.Id, message.SessionKey);
            }
            catch (Exception ex)
            {
                errorCount++;
                _logger.LogError(ex, "Error sending queued message. Message will be discarded.");
                // Remove from queue to avoid infinite retry
                _offlineQueue.TryDequeue(out _);
            }
        }

        _logger.LogInformation("Offline queue processed. {Processed} sent, {Errors} errors. Queue is now empty.",
            processedCount, errorCount);

        if (processedCount > 0 || errorCount > 0)
            OnOfflineQueueEmpty?.Invoke(this, EventArgs.Empty);
    }
}
