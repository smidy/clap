using System.Text.Json;
using System.Text.Json.Serialization;

namespace clawapp.Models;

// --- Base frame types (discriminated by "type") ---

public abstract record WebSocketFrame
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    protected WebSocketFrame() { }
    protected WebSocketFrame(string type) => Type = type;
}

public record RequestFrame : WebSocketFrame
{
    [JsonPropertyName("type")]
    public new string Type { get; init; } = "req";
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";
    [JsonPropertyName("method")]
    public string Method { get; init; } = "";
    [JsonPropertyName("params")]
    public JsonElement? Params { get; init; }

    public RequestFrame() : base("req") { }
}

/// <summary>
/// Strongly-typed request frame for serialization.
/// </summary>
public record RequestFrame<TParams> : WebSocketFrame
{
    [JsonPropertyName("type")]
    public new string Type { get; init; } = "req";
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";
    [JsonPropertyName("method")]
    public string Method { get; init; } = "";
    [JsonPropertyName("params")]
    public TParams Params { get; init; } = default!;

    public RequestFrame() : base("req") { }
    public RequestFrame(string type, string id, string method, TParams @params) : base(type)
    {
        Type = type;
        Id = id;
        Method = method;
        Params = @params;
    }

    /// <summary>
    /// Creates a new request frame with type="req".
    /// </summary>
    public static RequestFrame<TParams> Create(string id, string method, TParams @params)
        => new("req", id, method, @params);
}

public record ResponseFrame : WebSocketFrame
{
    [JsonPropertyName("type")]
    public new string Type { get; init; } = "res";
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }
    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; init; }
    [JsonPropertyName("error")]
    public ErrorPayload? Error { get; init; }

    public ResponseFrame() : base("res") { }
}

public record ErrorPayload
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = "";
    [JsonPropertyName("message")]
    public string Message { get; init; } = "";
}

public record EventFrame : WebSocketFrame
{
    [JsonPropertyName("type")]
    public new string Type { get; init; } = "event";
    [JsonPropertyName("event")]
    public string Event { get; init; } = "";
    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; init; }
    [JsonPropertyName("seq")]
    public int? Seq { get; init; }
    [JsonPropertyName("stateVersion")]
    public string? StateVersion { get; init; }

    public EventFrame() : base("event") { }
}

public record PingFrame : WebSocketFrame
{
    [JsonPropertyName("type")]
    public new string Type { get; init; } = "ping";

    public PingFrame() : base("ping") { }
}

public record PongFrame : WebSocketFrame
{
    [JsonPropertyName("type")]
    public new string Type { get; init; } = "pong";

    public PongFrame() : base("pong") { }
}

// --- Connect challenge (event payload) ---

public record ConnectChallengePayload
{
    [JsonPropertyName("nonce")]
    public string Nonce { get; init; } = "";
    [JsonPropertyName("ts")]
    public long Ts { get; init; }
}

// --- Connect request params (client → gateway) ---

public record ConnectParams
{
    [JsonPropertyName("minProtocol")]
    public int MinProtocol { get; init; } = 3;
    [JsonPropertyName("maxProtocol")]
    public int MaxProtocol { get; init; } = 3;
    [JsonPropertyName("client")]
    public ClientInfo Client { get; init; } = null!;
    [JsonPropertyName("role")]
    public string Role { get; init; } = "operator";
    [JsonPropertyName("scopes")]
    public string[] Scopes { get; init; } = null!;
    [JsonPropertyName("caps")]
    public string[] Caps { get; init; } = null!;
    [JsonPropertyName("commands")]
    public string[] Commands { get; init; } = null!;
    [JsonPropertyName("permissions")]
    public object Permissions { get; init; } = null!;
    [JsonPropertyName("auth")]
    public AuthInfo Auth { get; init; } = null!;
    [JsonPropertyName("locale")]
    public string Locale { get; init; } = "en-US";
    [JsonPropertyName("userAgent")]
    public string UserAgent { get; init; } = "";
    [JsonPropertyName("device")]
    public DeviceInfo Device { get; init; } = null!;
}

public record ClientInfo
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";
    [JsonPropertyName("version")]
    public string Version { get; init; } = "";
    [JsonPropertyName("platform")]
    public string Platform { get; init; } = "";
    [JsonPropertyName("mode")]
    public string Mode { get; init; } = "operator";
}

public record AuthInfo
{
    [JsonPropertyName("token")]
    public string Token { get; init; } = "";
}

public record DeviceInfo
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";
    [JsonPropertyName("publicKey")]
    public string PublicKey { get; init; } = "";
    [JsonPropertyName("signature")]
    public string Signature { get; init; } = "";
    [JsonPropertyName("signedAt")]
    public long SignedAt { get; init; }
    [JsonPropertyName("nonce")]
    public string Nonce { get; init; } = "";
}

// --- Connect response (hello-ok payload) ---

public record HelloOkPayload
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "hello-ok";
    [JsonPropertyName("protocol")]
    public int Protocol { get; init; } = 3;
    [JsonPropertyName("policy")]
    public PolicyInfo? Policy { get; init; }
    [JsonPropertyName("auth")]
    public HelloAuth? Auth { get; init; }
}

public record PolicyInfo
{
    [JsonPropertyName("tickIntervalMs")]
    public int TickIntervalMs { get; init; } = 15000;
}

public record HelloAuth
{
    [JsonPropertyName("deviceToken")]
    public string DeviceToken { get; init; } = "";
    [JsonPropertyName("role")]
    public string Role { get; init; } = "";
    [JsonPropertyName("scopes")]
    public string[]? Scopes { get; init; }
}

// --- Chat.history response payload ---

public record ChatHistoryPayload
{
    [JsonPropertyName("messages")]
    public ChatMessageDto[]? Messages { get; init; }
}

public record ChatMessageDto
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = "";
    [JsonPropertyName("content")]
    public ChatMessageContent[]? Content { get; init; }
    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; init; }
    [JsonPropertyName("toolCallId")]
    public string? ToolCallId { get; init; }
    [JsonPropertyName("tool_call_id")]
    public string? Tool_call_id { get; init; }
    [JsonPropertyName("toolName")]
    public string? ToolName { get; init; }
    [JsonPropertyName("tool_name")]
    public string? Tool_name { get; init; }
    [JsonPropertyName("usage")]
    public ChatUsage? Usage { get; init; }
    [JsonPropertyName("stopReason")]
    public string? StopReason { get; init; }
}

public record ChatMessageContent
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }
    [JsonPropertyName("text")]
    public string? Text { get; init; }
    [JsonPropertyName("thinking")]
    public string? Thinking { get; init; }
    [JsonPropertyName("thinkingSignature")]
    public string? ThinkingSignature { get; init; }
    [JsonPropertyName("mimeType")]
    public string? MimeType { get; init; }
    [JsonPropertyName("fileName")]
    public string? FileName { get; init; }
    [JsonPropertyName("content")]
    public object? Content { get; init; }
    [JsonPropertyName("id")]
    public string? Id { get; init; }
    [JsonPropertyName("name")]
    public string? Name { get; init; }
    [JsonPropertyName("arguments")]
    public object? Arguments { get; init; }
}

public record ChatUsage
{
    [JsonPropertyName("input")]
    public int? Input { get; init; }
    [JsonPropertyName("output")]
    public int? Output { get; init; }
    [JsonPropertyName("cacheRead")]
    public int? CacheRead { get; init; }
    [JsonPropertyName("cacheWrite")]
    public int? CacheWrite { get; init; }
    [JsonPropertyName("cost")]
    public ChatUsageCost? Cost { get; init; }
    [JsonPropertyName("total")]
    public int? Total { get; init; }
    [JsonPropertyName("totalTokens")]
    public int? TotalTokens { get; init; }
}

public record ChatUsageCost
{
    [JsonPropertyName("input")]
    public double? Input { get; init; }
    [JsonPropertyName("output")]
    public double? Output { get; init; }
    [JsonPropertyName("cacheRead")]
    public double? CacheRead { get; init; }
    [JsonPropertyName("cacheWrite")]
    public double? CacheWrite { get; init; }
    [JsonPropertyName("total")]
    public double? Total { get; init; }
}

// --- Chat.send request params ---

public record ChatSendParams
{
    [JsonPropertyName("sessionKey")]
    public string SessionKey { get; init; } = "";
    [JsonPropertyName("message")]
    public string Message { get; init; } = "";
    [JsonPropertyName("deliver")]
    public bool Deliver { get; init; }
    [JsonPropertyName("idempotencyKey")]
    public string IdempotencyKey { get; init; } = "";
}

// --- Chat.send with attachments request params ---

public record ChatSendWithAttachmentsParams
{
    [JsonPropertyName("sessionKey")]
    public string SessionKey { get; init; } = "";
    [JsonPropertyName("message")]
    public string Message { get; init; } = "";
    [JsonPropertyName("deliver")]
    public bool Deliver { get; init; }
    [JsonPropertyName("idempotencyKey")]
    public string IdempotencyKey { get; init; } = "";
    [JsonPropertyName("attachments")]
    public AttachmentParams[]? Attachments { get; init; }
}

public record AttachmentParams
{
    [JsonPropertyName("mimeType")]
    public string MimeType { get; init; } = "";
    [JsonPropertyName("content")]
    public string Content { get; init; } = ""; // Base64-encoded data
    [JsonPropertyName("fileName")]
    public string? FileName { get; init; }
}

// --- Chat.send response payload ---

public record ChatSendPayload
{
    [JsonPropertyName("messageId")]
    public string MessageId { get; init; } = "";
    [JsonPropertyName("status")]
    public string Status { get; init; } = "";
}

// --- Chat.history request params ---

public record ChatHistoryParams
{
    [JsonPropertyName("sessionKey")]
    public string SessionKey { get; init; } = "";
    [JsonPropertyName("limit")]
    public int Limit { get; init; } = 50;
}

// --- Chat.subscribe request params ---

public record ChatSubscribeParams
{
    [JsonPropertyName("sessionKey")]
    public string SessionKey { get; init; } = "";
}

// --- Sessions.list request params ---

public record SessionsListParams
{
    [JsonPropertyName("limit")]
    public int Limit { get; init; } = 20;
}

// --- Chat event payload (real-time message) ---

public record ChatEventPayload
{
    [JsonPropertyName("sessionKey")]
    public string SessionKey { get; init; } = "";
    [JsonPropertyName("runId")]
    public string? RunId { get; init; }
    [JsonPropertyName("state")]
    public string? State { get; init; }
    [JsonPropertyName("seq")]
    public int? Seq { get; init; }
    [JsonPropertyName("message")]
    public ChatMessageDto? Message { get; init; }
}

// --- Sessions.list response payload ---

public record SessionsListPayload
{
    [JsonPropertyName("sessions")]
    public SessionDto[]? Sessions { get; init; }
}

public record SessionDto
{
    [JsonPropertyName("key")]
    public string Key { get; init; } = "";
    [JsonPropertyName("name")]
    public string? Name { get; init; }
    [JsonPropertyName("lastActivity")]
    public long? LastActivity { get; init; }
}

// --- Device.push.register request params ---

public record DevicePushRegisterParams
{
    [JsonPropertyName("pushToken")]
    public string PushToken { get; init; } = "";
    [JsonPropertyName("pushPlatform")]
    public string PushPlatform { get; init; } = "";
}
