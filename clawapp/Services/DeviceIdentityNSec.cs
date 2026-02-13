using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NSec.Cryptography;

namespace clawapp.Services;

/// <summary>
/// Device identity with Ed25519 keypair for OpenClaw authentication using NSec.
/// </summary>
public sealed class DeviceIdentityNSec
{
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = "";

    [JsonPropertyName("publicKey")]
    public string PublicKeyBase64 { get; set; } = "";

    [JsonPropertyName("privateKey")]
    public string PrivateKeyBase64 { get; set; } = "";

    [JsonPropertyName("createdAt")]
    public long CreatedAtMs { get; set; }
}

/// <summary>
/// Manages device identity with Ed25519 keypair using NSec library.
/// </summary>
public sealed class DeviceIdentityStoreNSec
{
    private static readonly SignatureAlgorithm Algorithm = SignatureAlgorithm.Ed25519;
    
    private static readonly string IdentityPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "clawapp",
        "device-identity.json"
    );

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public DeviceIdentityNSec LoadOrCreate()
    {
        var existing = Load();
        if (existing != null)
        {
            var derivedId = DeriveDeviceId(existing.PublicKeyBase64);
            if (derivedId != null && derivedId != existing.DeviceId)
            {
                existing.DeviceId = derivedId;
                Save(existing);
            }
            return existing;
        }

        var fresh = Generate();
        Save(fresh);
        return fresh;
    }

    private DeviceIdentityNSec Generate()
    {
        // Generate Ed25519 keypair
        using var key = Key.Create(Algorithm, new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
        
        // Export keys
        var privateKeyBytes = key.Export(KeyBlobFormat.RawPrivateKey);
        var publicKeyBytes = key.Export(KeyBlobFormat.RawPublicKey);
        
        var publicKeyBase64 = Convert.ToBase64String(publicKeyBytes);
        var deviceId = DeriveDeviceId(publicKeyBase64) ?? Guid.NewGuid().ToString("N");

        return new DeviceIdentityNSec
        {
            DeviceId = deviceId,
            PublicKeyBase64 = publicKeyBase64,
            PrivateKeyBase64 = Convert.ToBase64String(privateKeyBytes),
            CreatedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    private string? DeriveDeviceId(string publicKeyBase64)
    {
        try
        {
            var publicKey = Convert.FromBase64String(publicKeyBase64);
            var hash = SHA256.HashData(publicKey);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }

    public string? SignPayload(string payload, DeviceIdentityNSec identity)
    {
        try
        {
            var privateKeyBytes = Convert.FromBase64String(identity.PrivateKeyBase64);
            
            // Import private key
            using var key = Key.Import(Algorithm, privateKeyBytes, KeyBlobFormat.RawPrivateKey);
            
            // Sign the payload
            var data = Encoding.UTF8.GetBytes(payload);
            var signature = Algorithm.Sign(key, data);
            
            return Base64UrlEncode(signature);
        }
        catch
        {
            return null;
        }
    }

    public string? PublicKeyBase64Url(DeviceIdentityNSec identity)
    {
        try
        {
            var publicKey = Convert.FromBase64String(identity.PublicKeyBase64);
            return Base64UrlEncode(publicKey);
        }
        catch
        {
            return null;
        }
    }

    public string BuildAuthPayload(
        string deviceId,
        string clientId,
        string clientMode,
        string role,
        string[] scopes,
        long signedAtMs,
        string? token,
        string? nonce)
    {
        var scopeString = string.Join(",", scopes);
        var authToken = token ?? "";
        var version = string.IsNullOrEmpty(nonce) ? "v1" : "v2";

        var parts = new[]
        {
            version,
            deviceId,
            clientId,
            clientMode,
            role,
            scopeString,
            signedAtMs.ToString(),
            authToken
        };

        var payload = string.Join("|", parts);
        
        if (!string.IsNullOrEmpty(nonce))
        {
            payload += "|" + nonce;
        }

        return payload;
    }

    private DeviceIdentityNSec? Load()
    {
        try
        {
            if (!File.Exists(IdentityPath))
                return null;

            var json = File.ReadAllText(IdentityPath);
            var identity = JsonSerializer.Deserialize<DeviceIdentityNSec>(json, JsonOptions);
            
            if (identity == null || 
                string.IsNullOrWhiteSpace(identity.DeviceId) ||
                string.IsNullOrWhiteSpace(identity.PublicKeyBase64) ||
                string.IsNullOrWhiteSpace(identity.PrivateKeyBase64))
            {
                return null;
            }

            return identity;
        }
        catch
        {
            return null;
        }
    }

    private void Save(DeviceIdentityNSec identity)
    {
        try
        {
            var dir = Path.GetDirectoryName(IdentityPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(identity, JsonOptions);
            File.WriteAllText(IdentityPath, json);
        }
        catch
        {
            // Log error
        }
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
