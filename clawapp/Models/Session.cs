namespace clawapp.Models;

/// <summary>
/// Chat session info from OpenClaw gateway.
/// </summary>
public sealed class Session
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long? LastActivity { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Key : Name;
}
