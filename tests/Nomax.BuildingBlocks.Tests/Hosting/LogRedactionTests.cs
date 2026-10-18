using Nomax.BuildingBlocks.Hosting;

namespace Nomax.BuildingBlocks.Tests.Hosting;

public sealed class LogRedactionTests
{
    [Theory]
    [InlineData("/ical/3f9c2e7a.ics", "/ical/[redacted]")]
    [InlineData("/ICAL/3f9c2e7a.ics", "/ical/[redacted]")]
    [InlineData("/api/identity/me", "/api/identity/me")]
    [InlineData("/icalendar", "/icalendar")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Hides_the_ics_token(string? path, string expected)
    {
        LogRedaction.RedactPath(path).ShouldBe(expected);
    }
}
