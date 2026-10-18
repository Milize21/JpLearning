using Microsoft.Extensions.Time.Testing;
using Nomax.SharedKernel.Identifiers;

namespace Nomax.BuildingBlocks.Tests.SharedKernel;

public sealed class Uuid7Tests
{
    [Fact]
    public void Creates_version_7_ids()
    {
        var id = Uuid7.New(new FakeTimeProvider());

        id.Version.ShouldBe(7);
    }

    [Fact]
    public void Ids_sort_by_creation_time()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 18, 0, 0, 0, TimeSpan.Zero));
        var first = Uuid7.New(clock);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        var second = Uuid7.New(clock);

        string.CompareOrdinal(first.ToString(), second.ToString()).ShouldBeLessThan(0);
    }
}
