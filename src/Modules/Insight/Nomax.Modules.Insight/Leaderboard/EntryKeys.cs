using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Nomax.Modules.Insight.Infrastructure;

namespace Nomax.Modules.Insight.Leaderboard;

/// <summary>
/// D-40: the pseudonymous <c>entryKey</c> of a leaderboard row, so clients can tell rows apart (and find their own through
/// <c>MyRankUpdated</c>) without seeing other users' ids (docs/05 §9).
/// </summary>
internal sealed class EntryKeys
{
    private const int Length = 16;
    private readonly byte[] _key;

    public EntryKeys(IOptions<LeaderboardOptions> options, IHostEnvironment environment)
    {
        var secret = options.Value.EntryKeySecret;
        if (!string.IsNullOrEmpty(secret))
        {
            _key = Encoding.UTF8.GetBytes(secret);
        }
        else if (environment.IsDevelopment())
        {
            _key = RandomNumberGenerator.GetBytes(32);
        }
        else
        {
            throw new InvalidOperationException("Leaderboard:EntryKeySecret is required outside development (D-40).");
        }
    }

    public string For(Guid seasonId, Guid studentId)
    {
        Span<byte> input = stackalloc byte[32];
        seasonId.TryWriteBytes(input[..16]);
        studentId.TryWriteBytes(input[16..]);
        Span<byte> hash = stackalloc byte[32];
        HMACSHA256.HashData(_key, input, hash);
        return Base64Url.EncodeToString(hash)[..Length];
    }
}
