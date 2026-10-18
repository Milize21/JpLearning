using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Nomax.Modules.Classroom.Infrastructure.Notifications;

/// <summary>
/// Message encryption for Web Push (RFC 8291, <c>aes128gcm</c> of RFC 8188), written against the .NET crypto APIs so no
/// third-party package is needed. The ephemeral key and salt can be passed in for the RFC test vector.
/// </summary>
public static class WebPushEncryption
{
    private const int RecordSize = 4096;

    public static byte[] Encrypt(
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> userAgentPublicKey,
        ReadOnlySpan<byte> authSecret,
        ECDiffieHellman? ephemeral = null,
        byte[]? salt = null)
    {
        using var ownedEphemeral = ephemeral is null ? ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256) : null;
        var serverKey = ephemeral ?? ownedEphemeral!;
        salt ??= RandomNumberGenerator.GetBytes(16);

        var serverPublic = ExportPublicKey(serverKey);
        using var clientKey = ImportPublicKey(userAgentPublicKey);
        var sharedSecret = serverKey.DeriveRawSecretAgreement(clientKey.PublicKey);

        // IKM = HKDF(auth_secret, ecdh_secret, "WebPush: info" || 0x00 || ua_public || as_public, 32)
        var keyInfo = Concat("WebPush: info\0"u8.ToArray(), userAgentPublicKey.ToArray(), serverPublic);
        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, sharedSecret, 32, authSecret.ToArray(), keyInfo);
        var contentKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, "Content-Encoding: aes128gcm\0"u8.ToArray());
        var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, "Content-Encoding: nonce\0"u8.ToArray());

        // A single record: payload followed by the 0x02 delimiter of the last record (no padding).
        var record = new byte[plaintext.Length + 1];
        plaintext.CopyTo(record);
        record[^1] = 0x02;
        var ciphertext = new byte[record.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(contentKey, tag.Length))
        {
            aes.Encrypt(nonce, record, ciphertext, tag);
        }

        // Header: salt (16) | record size (4, big endian) | key id length (1) | key id (the server public key).
        var header = new byte[16 + 4 + 1 + serverPublic.Length];
        salt.CopyTo(header, 0);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(16), RecordSize);
        header[20] = (byte)serverPublic.Length;
        serverPublic.CopyTo(header, 21);
        return Concat(header, ciphertext, tag);
    }

    public static byte[] ExportPublicKey(ECDiffieHellman key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var parameters = key.ExportParameters(includePrivateParameters: false);
        return Concat([0x04], parameters.Q.X!, parameters.Q.Y!);
    }

    private static ECDiffieHellman ImportPublicKey(ReadOnlySpan<byte> uncompressed)
    {
        if (uncompressed.Length != 65 || uncompressed[0] != 0x04)
        {
            throw new CryptographicException("The subscription key is not an uncompressed P-256 point.");
        }

        return ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = uncompressed[1..33].ToArray(), Y = uncompressed[33..65].ToArray() },
        });
    }

    internal static byte[] Concat(params ReadOnlySpan<byte[]> parts)
    {
        var total = 0;
        foreach (var part in parts)
        {
            total += part.Length;
        }

        var result = new byte[total];
        var offset = 0;
        foreach (var part in parts)
        {
            part.CopyTo(result, offset);
            offset += part.Length;
        }

        return result;
    }
}

public static class Base64Url
{
    public static string Encode(ReadOnlySpan<byte> bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Decode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var padded = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '='));
    }
}

public enum PushResult
{
    Delivered,

    /// <summary>The push service no longer knows the subscription (404/410): delete it.</summary>
    Gone,

    Failed,
}

/// <summary>Sends one encrypted message to a browser push service with a VAPID (RFC 8292) authorization.</summary>
internal sealed class WebPushSender(HttpClient http, IOptions<WebPushOptions> options, TimeProvider timeProvider)
{
    public bool IsEnabled => options.Value.IsConfigured;

    public async Task<(PushResult Result, string? Error)> SendAsync(string endpoint, string p256dh, string auth, string payloadJson, CancellationToken cancellationToken)
    {
        var body = WebPushEncryption.Encrypt(Encoding.UTF8.GetBytes(payloadJson), Base64Url.Decode(p256dh), Base64Url.Decode(auth));
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new ByteArrayContent(body) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Content.Headers.ContentEncoding.Add("aes128gcm");
        request.Headers.TryAddWithoutValidation("TTL", "86400");
        request.Headers.TryAddWithoutValidation("Urgency", "normal");
        request.Headers.TryAddWithoutValidation("Authorization", $"vapid t={VapidToken(new Uri(endpoint))}, k={options.Value.PublicKey}");

        using var response = await http.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return (PushResult.Delivered, null);
        }

        return response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone
            ? (PushResult.Gone, null)
            : (PushResult.Failed, $"Push service answered {(int)response.StatusCode}.");
    }

    /// <summary>ES256 JWT for the push service origin, valid 12 hours.</summary>
    private string VapidToken(Uri endpoint)
    {
        var header = Base64Url.Encode(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, string> { ["typ"] = "JWT", ["alg"] = "ES256" }));
        var claims = Base64Url.Encode(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["aud"] = endpoint.GetLeftPart(UriPartial.Authority),
            ["exp"] = timeProvider.GetUtcNow().AddHours(12).ToUnixTimeSeconds(),
            ["sub"] = options.Value.Subject,
        }));
        var signingInput = Encoding.ASCII.GetBytes($"{header}.{claims}");

        var publicKey = Base64Url.Decode(options.Value.PublicKey!);
        using var key = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = Base64Url.Decode(options.Value.PrivateKey!),
            Q = new ECPoint { X = publicKey[1..33], Y = publicKey[33..65] },
        });
        // JWS ES256 uses the raw r||s form, which is .NET's default (IEEE P1363).
        var signature = key.SignData(signingInput, HashAlgorithmName.SHA256);
        return $"{header}.{claims}.{Base64Url.Encode(signature)}";
    }
}
