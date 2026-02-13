using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace clawapp.Services;

/// <summary>
/// Device identity with Ed25519 keypair using Bouncy Castle.
/// </summary>
public sealed class DeviceIdentityBouncyCastle
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
/// Manages device identity with Ed25519 keypair using Bouncy Castle.
/// </summary>
public sealed class DeviceIdentityStoreBouncyCastle
{
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

    public DeviceIdentityBouncyCastle LoadOrCreate()
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

    private DeviceIdentityBouncyCastle Generate()
    {
        // Generate Ed25519 keypair
        var keyPairGenerator = new Ed25519KeyPairGenerator();
        keyPairGenerator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
        var keyPair = keyPairGenerator.GenerateKeyPair();

        var privateKey = (Ed25519PrivateKeyParameters)keyPair.Private;
        var publicKey = (Ed25519PublicKeyParameters)keyPair.Public;

        var privateKeyBytes = privateKey.GetEncoded();
        var publicKeyBytes = publicKey.GetEncoded();
        
        var publicKeyBase64 = Convert.ToBase64String(publicKeyBytes);
        var deviceId = DeriveDeviceId(publicKeyBase64) ?? Guid.NewGuid().ToString("N");

        return new DeviceIdentityBouncyCastle
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

    public string? SignPayload(string payload, DeviceIdentityBouncyCastle identity)
    {
        try
        {
            var privateKeyBytes = Convert.FromBase64String(identity.PrivateKeyBase64);
            var privateKey = new Ed25519PrivateKeyParameters(privateKeyBytes, 0);

            var signer = new Ed25519Signer();
            signer.Init(true, privateKey);
            
            var data = Encoding.UTF8.GetBytes(payload);
            signer.BlockUpdate(data, 0, data.Length);
            var signature = signer.GenerateSignature();
            
            return Base64UrlEncode(signature);
        }
        catch
        {
            return null;
        }
    }

    public string? PublicKeyBase64Url(DeviceIdentityBouncyCastle identity)
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

    private DeviceIdentityBouncyCastle? Load()
    {
        try
        {
            if (!File.Exists(IdentityPath))
                return null;

            var json = File.ReadAllText(IdentityPath);
            var identity = JsonSerializer.Deserialize<DeviceIdentityBouncyCastle>(json, JsonOptions);
            
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

    private void Save(DeviceIdentityBouncyCastle identity)
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
