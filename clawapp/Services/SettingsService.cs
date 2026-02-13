using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using clawapp.Models;

namespace clawapp.Services;

/// <summary>
/// Persists gateway settings using isolated storage.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private const string GatewayFileName = "gateway_settings.json";
    private const string AppFileName = "app_settings.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public async Task SaveGatewaySettingsAsync(GatewayInfo gateway, CancellationToken cancellationToken = default)
    {
        var store = System.IO.IsolatedStorage.IsolatedStorageFile.GetStore(
            System.IO.IsolatedStorage.IsolatedStorageScope.User | System.IO.IsolatedStorage.IsolatedStorageScope.Assembly,
            null, null);

        await using var stream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
            GatewayFileName, FileMode.Create, FileAccess.Write, FileShare.None, store);

        await JsonSerializer.SerializeAsync(stream, new GatewaySettingsDto
        {
            Host = gateway.Host,
            Port = gateway.Port,
            Token = gateway.Token,
            UseSecureWebSocket = gateway.UseSecureWebSocket
        }, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<GatewayInfo?> LoadGatewaySettingsAsync(CancellationToken cancellationToken = default)
    {
        var store = System.IO.IsolatedStorage.IsolatedStorageFile.GetStore(
            System.IO.IsolatedStorage.IsolatedStorageScope.User | System.IO.IsolatedStorage.IsolatedStorageScope.Assembly,
            null, null);

        if (!store.FileExists(GatewayFileName))
            return null;

        await using var stream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
            GatewayFileName, FileMode.Open, FileAccess.Read, FileShare.Read, store);

        var dto = await JsonSerializer.DeserializeAsync<GatewaySettingsDto>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return dto == null ? null : new GatewayInfo
        {
            Host = dto.Host ?? "127.0.0.1",
            // Port is nullable - only set if > 0
            Port = dto.Port > 0 ? dto.Port : null,
            Token = dto.Token ?? string.Empty,
            UseSecureWebSocket = dto.UseSecureWebSocket
        };
    }

    public async Task SaveThemeVariantAsync(string themeVariant, CancellationToken cancellationToken = default)
    {
        var store = System.IO.IsolatedStorage.IsolatedStorageFile.GetStore(
            System.IO.IsolatedStorage.IsolatedStorageScope.User | System.IO.IsolatedStorage.IsolatedStorageScope.Assembly,
            null, null);

        // Load existing settings first to preserve other values
        AppSettingsDto? existing = null;
        if (store.FileExists(AppFileName))
        {
            await using var readStream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
                AppFileName, FileMode.Open, FileAccess.Read, FileShare.Read, store);
            existing = await JsonSerializer.DeserializeAsync<AppSettingsDto>(readStream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        var settings = existing ?? new AppSettingsDto();
        settings.ThemeVariant = themeVariant;

        await using var writeStream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
            AppFileName, FileMode.Create, FileAccess.Write, FileShare.None, store);

        await JsonSerializer.SerializeAsync(writeStream, settings, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> LoadThemeVariantAsync(CancellationToken cancellationToken = default)
    {
        var store = System.IO.IsolatedStorage.IsolatedStorageFile.GetStore(
            System.IO.IsolatedStorage.IsolatedStorageScope.User | System.IO.IsolatedStorage.IsolatedStorageScope.Assembly,
            null, null);

        if (!store.FileExists(AppFileName))
            return null;

        await using var stream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
            AppFileName, FileMode.Open, FileAccess.Read, FileShare.Read, store);

        var dto = await JsonSerializer.DeserializeAsync<AppSettingsDto>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return dto?.ThemeVariant;
    }

    private sealed class GatewaySettingsDto
    {
        public string? Host { get; set; }
        public int? Port { get; set; }
        public string? Token { get; set; }
        public bool UseSecureWebSocket { get; set; }
    }

    public async Task SaveShowThinkingBlocksAsync(bool show, CancellationToken cancellationToken = default)
    {
        var store = System.IO.IsolatedStorage.IsolatedStorageFile.GetStore(
            System.IO.IsolatedStorage.IsolatedStorageScope.User | System.IO.IsolatedStorage.IsolatedStorageScope.Assembly,
            null, null);

        // Load existing settings first to preserve other values
        AppSettingsDto? existing = null;
        if (store.FileExists(AppFileName))
        {
            await using var readStream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
                AppFileName, FileMode.Open, FileAccess.Read, FileShare.Read, store);
            existing = await JsonSerializer.DeserializeAsync<AppSettingsDto>(readStream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        var settings = existing ?? new AppSettingsDto();
        settings.ShowThinkingBlocks = show;

        await using var writeStream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
            AppFileName, FileMode.Create, FileAccess.Write, FileShare.None, store);

        await JsonSerializer.SerializeAsync(writeStream, settings, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> LoadShowThinkingBlocksAsync(CancellationToken cancellationToken = default)
    {
        var store = System.IO.IsolatedStorage.IsolatedStorageFile.GetStore(
            System.IO.IsolatedStorage.IsolatedStorageScope.User | System.IO.IsolatedStorage.IsolatedStorageScope.Assembly,
            null, null);

        if (!store.FileExists(AppFileName))
            return false; // Default: don't show thinking blocks

        await using var stream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
            AppFileName, FileMode.Open, FileAccess.Read, FileShare.Read, store);

        var dto = await JsonSerializer.DeserializeAsync<AppSettingsDto>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return dto?.ShowThinkingBlocks ?? false;
    }

    public async Task SaveShowToolCallsAsync(bool show, CancellationToken cancellationToken = default)
    {
        var store = System.IO.IsolatedStorage.IsolatedStorageFile.GetStore(
            System.IO.IsolatedStorage.IsolatedStorageScope.User | System.IO.IsolatedStorage.IsolatedStorageScope.Assembly,
            null, null);

        // Load existing settings first to preserve other values
        AppSettingsDto? existing = null;
        if (store.FileExists(AppFileName))
        {
            await using var readStream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
                AppFileName, FileMode.Open, FileAccess.Read, FileShare.Read, store);
            existing = await JsonSerializer.DeserializeAsync<AppSettingsDto>(readStream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        var settings = existing ?? new AppSettingsDto();
        settings.ShowToolCalls = show;

        await using var writeStream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
            AppFileName, FileMode.Create, FileAccess.Write, FileShare.None, store);

        await JsonSerializer.SerializeAsync(writeStream, settings, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> LoadShowToolCallsAsync(CancellationToken cancellationToken = default)
    {
        var store = System.IO.IsolatedStorage.IsolatedStorageFile.GetStore(
            System.IO.IsolatedStorage.IsolatedStorageScope.User | System.IO.IsolatedStorage.IsolatedStorageScope.Assembly,
            null, null);

        if (!store.FileExists(AppFileName))
            return true; // Default: show tool calls

        await using var stream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
            AppFileName, FileMode.Open, FileAccess.Read, FileShare.Read, store);

        var dto = await JsonSerializer.DeserializeAsync<AppSettingsDto>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return dto?.ShowToolCalls ?? true;
    }

    public async Task SaveShowToolResultsAsync(bool show, CancellationToken cancellationToken = default)
    {
        var store = System.IO.IsolatedStorage.IsolatedStorageFile.GetStore(
            System.IO.IsolatedStorage.IsolatedStorageScope.User | System.IO.IsolatedStorage.IsolatedStorageScope.Assembly,
            null, null);

        // Load existing settings first to preserve other values
        AppSettingsDto? existing = null;
        if (store.FileExists(AppFileName))
        {
            await using var readStream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
                AppFileName, FileMode.Open, FileAccess.Read, FileShare.Read, store);
            existing = await JsonSerializer.DeserializeAsync<AppSettingsDto>(readStream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        var settings = existing ?? new AppSettingsDto();
        settings.ShowToolResults = show;

        await using var writeStream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
            AppFileName, FileMode.Create, FileAccess.Write, FileShare.None, store);

        await JsonSerializer.SerializeAsync(writeStream, settings, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> LoadShowToolResultsAsync(CancellationToken cancellationToken = default)
    {
        var store = System.IO.IsolatedStorage.IsolatedStorageFile.GetStore(
            System.IO.IsolatedStorage.IsolatedStorageScope.User | System.IO.IsolatedStorage.IsolatedStorageScope.Assembly,
            null, null);

        if (!store.FileExists(AppFileName))
            return true; // Default: show tool results

        await using var stream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
            AppFileName, FileMode.Open, FileAccess.Read, FileShare.Read, store);

        var dto = await JsonSerializer.DeserializeAsync<AppSettingsDto>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return dto?.ShowToolResults ?? true;
    }

    public async Task SaveAutoPlayAudioAsync(bool autoPlay, CancellationToken cancellationToken = default)
    {
        var store = System.IO.IsolatedStorage.IsolatedStorageFile.GetStore(
            System.IO.IsolatedStorage.IsolatedStorageScope.User | System.IO.IsolatedStorage.IsolatedStorageScope.Assembly,
            null, null);

        // Load existing settings first to preserve other values
        AppSettingsDto? existing = null;
        if (store.FileExists(AppFileName))
        {
            await using var readStream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
                AppFileName, FileMode.Open, FileAccess.Read, FileShare.Read, store);
            existing = await JsonSerializer.DeserializeAsync<AppSettingsDto>(readStream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        var settings = existing ?? new AppSettingsDto();
        settings.AutoPlayAudio = autoPlay;

        await using var writeStream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
            AppFileName, FileMode.Create, FileAccess.Write, FileShare.None, store);

        await JsonSerializer.SerializeAsync(writeStream, settings, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> LoadAutoPlayAudioAsync(CancellationToken cancellationToken = default)
    {
        var store = System.IO.IsolatedStorage.IsolatedStorageFile.GetStore(
            System.IO.IsolatedStorage.IsolatedStorageScope.User | System.IO.IsolatedStorage.IsolatedStorageScope.Assembly,
            null, null);

        if (!store.FileExists(AppFileName))
            return false; // Default: don't auto-play audio

        await using var stream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
            AppFileName, FileMode.Open, FileAccess.Read, FileShare.Read, store);

        var dto = await JsonSerializer.DeserializeAsync<AppSettingsDto>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return dto?.AutoPlayAudio ?? false;
    }

    public async Task SaveAutoDownloadAttachmentsAsync(bool autoDownload, CancellationToken cancellationToken = default)
    {
        var store = System.IO.IsolatedStorage.IsolatedStorageFile.GetStore(
            System.IO.IsolatedStorage.IsolatedStorageScope.User | System.IO.IsolatedStorage.IsolatedStorageScope.Assembly,
            null, null);

        // Load existing settings first to preserve other values
        AppSettingsDto? existing = null;
        if (store.FileExists(AppFileName))
        {
            await using var readStream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
                AppFileName, FileMode.Open, FileAccess.Read, FileShare.Read, store);
            existing = await JsonSerializer.DeserializeAsync<AppSettingsDto>(readStream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        var settings = existing ?? new AppSettingsDto();
        settings.AutoDownloadAttachments = autoDownload;

        await using var writeStream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
            AppFileName, FileMode.Create, FileAccess.Write, FileShare.None, store);

        await JsonSerializer.SerializeAsync(writeStream, settings, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> LoadAutoDownloadAttachmentsAsync(CancellationToken cancellationToken = default)
    {
        var store = System.IO.IsolatedStorage.IsolatedStorageFile.GetStore(
            System.IO.IsolatedStorage.IsolatedStorageScope.User | System.IO.IsolatedStorage.IsolatedStorageScope.Assembly,
            null, null);

        if (!store.FileExists(AppFileName))
            return false; // Default: don't auto-download attachments

        await using var stream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
            AppFileName, FileMode.Open, FileAccess.Read, FileShare.Read, store);

        var dto = await JsonSerializer.DeserializeAsync<AppSettingsDto>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return dto?.AutoDownloadAttachments ?? false;
    }

    public async Task SaveNotificationSoundsEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        var store = System.IO.IsolatedStorage.IsolatedStorageFile.GetStore(
            System.IO.IsolatedStorage.IsolatedStorageScope.User | System.IO.IsolatedStorage.IsolatedStorageScope.Assembly,
            null, null);

        // Load existing settings first to preserve other values
        AppSettingsDto? existing = null;
        if (store.FileExists(AppFileName))
        {
            await using var readStream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
                AppFileName, FileMode.Open, FileAccess.Read, FileShare.Read, store);
            existing = await JsonSerializer.DeserializeAsync<AppSettingsDto>(readStream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        var settings = existing ?? new AppSettingsDto();
        settings.NotificationSoundsEnabled = enabled;

        await using var writeStream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
            AppFileName, FileMode.Create, FileAccess.Write, FileShare.None, store);

        await JsonSerializer.SerializeAsync(writeStream, settings, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> LoadNotificationSoundsEnabledAsync(CancellationToken cancellationToken = default)
    {
        var store = System.IO.IsolatedStorage.IsolatedStorageFile.GetStore(
            System.IO.IsolatedStorage.IsolatedStorageScope.User | System.IO.IsolatedStorage.IsolatedStorageScope.Assembly,
            null, null);

        if (!store.FileExists(AppFileName))
            return true; // Default: notifications enabled

        await using var stream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
            AppFileName, FileMode.Open, FileAccess.Read, FileShare.Read, store);

        var dto = await JsonSerializer.DeserializeAsync<AppSettingsDto>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return dto?.NotificationSoundsEnabled ?? true;
    }

    public async Task SaveNotificationVibrationEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        var store = System.IO.IsolatedStorage.IsolatedStorageFile.GetStore(
            System.IO.IsolatedStorage.IsolatedStorageScope.User | System.IO.IsolatedStorage.IsolatedStorageScope.Assembly,
            null, null);

        // Load existing settings first to preserve other values
        AppSettingsDto? existing = null;
        if (store.FileExists(AppFileName))
        {
            await using var readStream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
                AppFileName, FileMode.Open, FileAccess.Read, FileShare.Read, store);
            existing = await JsonSerializer.DeserializeAsync<AppSettingsDto>(readStream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        var settings = existing ?? new AppSettingsDto();
        settings.NotificationVibrationEnabled = enabled;

        await using var writeStream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
            AppFileName, FileMode.Create, FileAccess.Write, FileShare.None, store);

        await JsonSerializer.SerializeAsync(writeStream, settings, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> LoadNotificationVibrationEnabledAsync(CancellationToken cancellationToken = default)
    {
        var store = System.IO.IsolatedStorage.IsolatedStorageFile.GetStore(
            System.IO.IsolatedStorage.IsolatedStorageScope.User | System.IO.IsolatedStorage.IsolatedStorageScope.Assembly,
            null, null);

        if (!store.FileExists(AppFileName))
            return true; // Default: vibration enabled

        await using var stream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
            AppFileName, FileMode.Open, FileAccess.Read, FileShare.Read, store);

        var dto = await JsonSerializer.DeserializeAsync<AppSettingsDto>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return dto?.NotificationVibrationEnabled ?? true;
    }

    public async Task SaveShowStreamingMessagesAsync(bool show, CancellationToken cancellationToken = default)
    {
        var store = System.IO.IsolatedStorage.IsolatedStorageFile.GetStore(
            System.IO.IsolatedStorage.IsolatedStorageScope.User | System.IO.IsolatedStorage.IsolatedStorageScope.Assembly,
            null, null);

        // Load existing settings first to preserve other values
        AppSettingsDto? existing = null;
        if (store.FileExists(AppFileName))
        {
            await using var readStream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
                AppFileName, FileMode.Open, FileAccess.Read, FileShare.Read, store);
            existing = await JsonSerializer.DeserializeAsync<AppSettingsDto>(readStream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        var settings = existing ?? new AppSettingsDto();
        settings.ShowStreamingMessages = show;

        await using var writeStream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
            AppFileName, FileMode.Create, FileAccess.Write, FileShare.None, store);

        await JsonSerializer.SerializeAsync(writeStream, settings, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> LoadShowStreamingMessagesAsync(CancellationToken cancellationToken = default)
    {
        var store = System.IO.IsolatedStorage.IsolatedStorageFile.GetStore(
            System.IO.IsolatedStorage.IsolatedStorageScope.User | System.IO.IsolatedStorage.IsolatedStorageScope.Assembly,
            null, null);

        if (!store.FileExists(AppFileName))
            return true; // Default: show streaming messages (live update)

        await using var stream = new System.IO.IsolatedStorage.IsolatedStorageFileStream(
            AppFileName, FileMode.Open, FileAccess.Read, FileShare.Read, store);

        var dto = await JsonSerializer.DeserializeAsync<AppSettingsDto>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return dto?.ShowStreamingMessages ?? true;
    }

    private sealed class AppSettingsDto
    {
        public string? ThemeVariant { get; set; }
        public bool? ShowThinkingBlocks { get; set; }
        public bool? ShowToolCalls { get; set; }
        public bool? ShowToolResults { get; set; }
        public bool? AutoPlayAudio { get; set; }
        public bool? AutoDownloadAttachments { get; set; }
        public bool? NotificationSoundsEnabled { get; set; }
        public bool? NotificationVibrationEnabled { get; set; }
        public bool? ShowStreamingMessages { get; set; }
    }
}
