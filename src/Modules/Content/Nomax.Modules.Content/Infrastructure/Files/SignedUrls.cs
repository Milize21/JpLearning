using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Nomax.Modules.Content.Infrastructure.Files;

/// <summary>
/// BR-FILE-04: download links <c>/api/content/files/{id}/content?exp=…&amp;sig=…</c> signed with HMAC-SHA256 and valid for a few
/// minutes. Resource endpoints check access first and then redirect here; the link itself needs no token.
/// </summary>
internal sealed class SignedUrls
{
    private readonly byte[] _key;
    private readonly TimeProvider _timeProvider;
    private readonly int _ttlSeconds;

    public SignedUrls(IOptions<StorageOptions> options, IHostEnvironment environment, TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _ttlSeconds = options.Value.SignedUrlTtlSeconds;
        if (!string.IsNullOrWhiteSpace(options.Value.SigningKey))
        {
            _key = Convert.FromBase64String(options.Value.SigningKey);
            if (_key.Length < 32)
            {
                throw new InvalidOperationException("Storage:SigningKey must be at least 32 bytes (base64).");
            }
        }
        else if (environment.IsDevelopment())
        {
            _key = RandomNumberGenerator.GetBytes(32);
        }
        else
        {
            throw new InvalidOperationException("Storage:SigningKey is not configured.");
        }
    }

    /// <param name="inline">Only for verified images (book covers); everything else is an attachment.</param>
    public string Create(Guid fileId, bool inline = false)
    {
        var expires = _timeProvider.GetUtcNow().AddSeconds(_ttlSeconds).ToUnixTimeSeconds();
        var signature = Sign(fileId, expires, inline);
        var url = string.Create(CultureInfo.InvariantCulture, $"/api/content/files/{fileId}/content?exp={expires}&sig={signature}");
        return inline ? url + "&inline=1" : url;
    }

    public bool Verify(Guid fileId, long expires, string? signature, bool inline)
    {
        if (string.IsNullOrEmpty(signature) || _timeProvider.GetUtcNow().ToUnixTimeSeconds() > expires)
        {
            return false;
        }

        var expected = Encoding.ASCII.GetBytes(Sign(fileId, expires, inline));
        var actual = Encoding.ASCII.GetBytes(signature);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private string Sign(Guid fileId, long expires, bool inline)
    {
        var payload = Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{fileId:N}:{expires}:{(inline ? 1 : 0)}"));
        return Base64Url(HMACSHA256.HashData(_key, payload));
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
