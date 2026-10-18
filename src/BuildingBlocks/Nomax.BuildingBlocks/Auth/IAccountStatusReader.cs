namespace Nomax.BuildingBlocks.Auth;

/// <summary>
/// Tells whether an account may use the API (docs/01 §9). Implemented by the Identity module from
/// identity.user_profiles.is_active, cached for at most 60 seconds; a missing profile counts as inactive.
/// </summary>
public interface IAccountStatusReader
{
    ValueTask<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken);
}
