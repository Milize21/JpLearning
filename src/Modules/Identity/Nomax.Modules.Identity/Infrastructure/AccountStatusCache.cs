using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nomax.BuildingBlocks.Auth;
using StackExchange.Redis;

namespace Nomax.Modules.Identity.Infrastructure;

/// <summary>
/// <see cref="IAccountStatusReader"/> backed by identity.user_profiles.is_active, cached in Valkey under
/// <c>profile-active:{userId}</c> for 60 seconds (docs/01 §9, docs/03 §11). A missing profile counts as inactive.
/// When Valkey is down the database answers directly, so requests never fail because of the cache.
/// </summary>
internal sealed class AccountStatusReader(
    IdentityDbContext db,
    AccountStatusCache cache) : IAccountStatusReader
{
    public async ValueTask<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await cache.TryGetAsync(userId) is { } cached)
        {
            return cached;
        }

        var active = await db.UserProfiles.AsNoTracking()
            .Where(profile => profile.Id == userId)
            .Select(profile => profile.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        await cache.SetAsync(userId, active);
        return active;
    }
}

internal sealed partial class AccountStatusCache(IConnectionMultiplexer redis, ILogger<AccountStatusCache> logger)
{
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(60);

    public static RedisKey Key(Guid userId) => $"profile-active:{userId}";

    public async Task<bool?> TryGetAsync(Guid userId)
    {
        if (!redis.IsConnected)
        {
            return null;
        }

        try
        {
            var value = await redis.GetDatabase().StringGetAsync(Key(userId));
            return value.HasValue ? value == "1" : null;
        }
        catch (RedisException exception)
        {
            LogCacheError(logger, exception);
            return null;
        }
    }

    public async Task SetAsync(Guid userId, bool active)
    {
        if (!redis.IsConnected)
        {
            return;
        }

        try
        {
            await redis.GetDatabase().StringSetAsync(Key(userId), active ? "1" : "0", Duration);
        }
        catch (RedisException exception)
        {
            LogCacheError(logger, exception);
        }
    }

    /// <summary>Called right after (de)activation so the change applies at once instead of after 60 seconds.</summary>
    public async Task InvalidateAsync(Guid userId)
    {
        if (!redis.IsConnected)
        {
            return;
        }

        try
        {
            await redis.GetDatabase().KeyDeleteAsync(Key(userId));
        }
        catch (RedisException exception)
        {
            LogCacheError(logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Account status cache is unavailable; reading the database instead")]
    private static partial void LogCacheError(ILogger logger, Exception exception);
}
