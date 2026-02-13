using System.Threading;
using System.Threading.Tasks;
using clawapp.Models;

namespace clawapp.Services;

/// <summary>
/// Persist gateway connection settings and app preferences.
/// </summary>
public interface ISettingsService
{
    Task SaveGatewaySettingsAsync(GatewayInfo gateway, CancellationToken cancellationToken = default);
    Task<GatewayInfo?> LoadGatewaySettingsAsync(CancellationToken cancellationToken = default);
    Task SaveThemeVariantAsync(string themeVariant, CancellationToken cancellationToken = default);
    Task<string?> LoadThemeVariantAsync(CancellationToken cancellationToken = default);
    
    // Display settings
    Task SaveShowThinkingBlocksAsync(bool show, CancellationToken cancellationToken = default);
    Task<bool> LoadShowThinkingBlocksAsync(CancellationToken cancellationToken = default);
    Task SaveShowToolCallsAsync(bool show, CancellationToken cancellationToken = default);
    Task<bool> LoadShowToolCallsAsync(CancellationToken cancellationToken = default);
    Task SaveShowToolResultsAsync(bool show, CancellationToken cancellationToken = default);
    Task<bool> LoadShowToolResultsAsync(CancellationToken cancellationToken = default);

    // Media settings
    Task SaveAutoPlayAudioAsync(bool autoPlay, CancellationToken cancellationToken = default);
    Task<bool> LoadAutoPlayAudioAsync(CancellationToken cancellationToken = default);
    Task SaveAutoDownloadAttachmentsAsync(bool autoDownload, CancellationToken cancellationToken = default);
    Task<bool> LoadAutoDownloadAttachmentsAsync(CancellationToken cancellationToken = default);

    // Notification settings
    Task SaveNotificationSoundsEnabledAsync(bool enabled, CancellationToken cancellationToken = default);
    Task<bool> LoadNotificationSoundsEnabledAsync(CancellationToken cancellationToken = default);
    Task SaveNotificationVibrationEnabledAsync(bool enabled, CancellationToken cancellationToken = default);
    Task<bool> LoadNotificationVibrationEnabledAsync(CancellationToken cancellationToken = default);

    // Streaming settings
    Task SaveShowStreamingMessagesAsync(bool show, CancellationToken cancellationToken = default);
    Task<bool> LoadShowStreamingMessagesAsync(CancellationToken cancellationToken = default);
}
