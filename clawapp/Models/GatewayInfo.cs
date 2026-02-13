namespace clawapp.Models;

/// <summary>
/// Gateway connection details for OpenClaw.
/// </summary>
public sealed class GatewayInfo
{
    /// <summary>
    /// The host address (IP or hostname). Can be a Tailscale machine name like "myhost.tailnet.ts.net".
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// The port number. If null or empty, no port is appended (uses default 80/443).
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Authentication token (optional).
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Whether to use secure WebSocket (wss://). Enable for Tailscale serve HTTPS endpoints.
    /// </summary>
    public bool UseSecureWebSocket { get; set; } = false;

    /// <summary>
    /// The WebSocket URI constructed from host, port, and security settings.
    /// When port is not specified, uses default ports (80 for ws, 443 for wss).
    /// </summary>
    public string WebSocketUri
    {
        get
        {
            var scheme = UseSecureWebSocket || LooksLikeSecureEndpoint(Host) ? "wss" : "ws";
            // Only append port if explicitly specified
            if (Port.HasValue && Port.Value > 0)
                return $"{scheme}://{Host}:{Port.Value}";
            return $"{scheme}://{Host}";
        }
    }

    /// <summary>
    /// The HTTP origin URL for WebSocket handshake. Must match the scheme of the WebSocket.
    /// When port is not specified, uses default ports (80 for http, 443 for https).
    /// </summary>
    public string OriginUrl
    {
        get
        {
            var scheme = UseSecureWebSocket || LooksLikeSecureEndpoint(Host) ? "https" : "http";
            // Only append port if explicitly specified
            if (Port.HasValue && Port.Value > 0)
                return $"{scheme}://{Host}:{Port.Value}";
            return $"{scheme}://{Host}";
        }
    }

    /// <summary>
    /// Detects if the host appears to be a secure/Tailscale endpoint based on common patterns.
    /// </summary>
    private static bool LooksLikeSecureEndpoint(string host)
    {
        if (string.IsNullOrEmpty(host))
            return false;

        var lowerHost = host.ToLowerInvariant();

        // Tailscale magic DNS often uses ts.net or .tailscale.svc
        if (lowerHost.Contains(".ts.net") || lowerHost.Contains(".tailscale"))
            return true;

        // Common HTTPS/Tailscale serve patterns
        if (lowerHost.EndsWith(".local") || lowerHost.EndsWith(".home"))
            return false; // Usually local development

        // If it contains a domain-like structure (contains dots and not just an IP)
        if (lowerHost.Contains('.') && !IsIpAddress(lowerHost))
            return true;

        return false;
    }

    private static bool IsIpAddress(string host)
    {
        // Simple check - if it matches IP pattern
        return System.Text.RegularExpressions.Regex.IsMatch(host,
            @"^(\d{1,3}\.){3}\d{1,3}$|^(\[[0-9a-fA-F:]+\])$");
    }
}
