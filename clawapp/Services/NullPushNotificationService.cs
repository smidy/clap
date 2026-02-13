using System;
using System.Threading;
using System.Threading.Tasks;

namespace clawapp.Services;

/// <summary>
/// Null implementation for platforms that don't support push notifications (Desktop).
/// </summary>
public class NullPushNotificationService : IPushNotificationService
{
    public string Platform => "none";

    public event EventHandler<string>? OnTokenRefreshed;

    public Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }

    public Task<bool> RegisterTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<bool> UnregisterTokenAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    // Suppress unused event warning
    protected virtual void OnTokenRefreshedEvent(string token)
    {
        OnTokenRefreshed?.Invoke(this, token);
    }
}
