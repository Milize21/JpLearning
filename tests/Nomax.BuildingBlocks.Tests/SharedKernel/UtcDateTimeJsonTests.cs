using System.Text.Json;
using Nomax.SharedKernel.Serialization;

namespace Nomax.BuildingBlocks.Tests.SharedKernel;

public sealed class UtcDateTimeJsonTests
{
    public sealed record Holder(DateTimeOffset At);

    [Fact]
    public void Writes_utc_with_z_suffix()
    {
        var wib = new DateTimeOffset(2026, 10, 18, 19, 30, 0, TimeSpan.FromHours(7));

        JsonSerializer.Serialize(new Holder(wib), NomaxJson.Options).ShouldBe("""{"at":"2026-10-18T12:30:00Z"}""");
    }

    [Fact]
    public void Keeps_fractional_seconds()
    {
        var at = new DateTimeOffset(2026, 10, 18, 12, 30, 0, 125, TimeSpan.Zero);

        JsonSerializer.Serialize(new Holder(at), NomaxJson.Options).ShouldBe("""{"at":"2026-10-18T12:30:00.125Z"}""");
    }

    [Fact]
    public void Reads_any_offset_as_utc()
    {
        var holder = JsonSerializer.Deserialize<Holder>("""{"at":"2026-10-18T19:30:00+07:00"}""", NomaxJson.Options)!;

        holder.At.Offset.ShouldBe(TimeSpan.Zero);
        holder.At.ShouldBe(new DateTimeOffset(2026, 10, 18, 12, 30, 0, TimeSpan.Zero));
    }
}
