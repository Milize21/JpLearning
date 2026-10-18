using Nomax.BuildingBlocks.Auth;

namespace Nomax.IntegrationTests.Support;

/// <summary>Stands in for the Identity module's reader until Phase 0.2.</summary>
internal sealed class FakeAccountStatusReader : IAccountStatusReader
{
    private int _calls;

    public HashSet<Guid> Inactive { get; } = [TestUsers.InactiveMurid];

    public int Calls => _calls;

    public ValueTask<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        return ValueTask.FromResult(!Inactive.Contains(userId));
    }
}
