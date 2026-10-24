using System.Globalization;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Nomax.Modules.Content.Infrastructure.Files;

namespace Nomax.Modules.Content.Tests.Infrastructure;

public sealed class SignedUrlsTests
{
    private static readonly Guid FileId = Guid.Parse("01920000-0000-7000-8000-000000000001");
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));

    private SignedUrls Create(string environment = "Production") => new(
        Options.Create(new StorageOptions { SigningKey = Convert.ToBase64String(new byte[32]), SignedUrlTtlSeconds = 300 }),
        new TestEnvironment(environment),
        _clock);

    private static (long Expires, string Signature, bool Inline) Parse(string url)
    {
        var query = url[(url.IndexOf('?', StringComparison.Ordinal) + 1)..]
            .Split('&')
            .Select(part => part.Split('=', 2))
            .ToDictionary(part => part[0], part => part[1], StringComparer.Ordinal);
        return (long.Parse(query["exp"], CultureInfo.InvariantCulture), query["sig"], query.ContainsKey("inline"));
    }

    [Fact]
    public void BR_FILE_04_a_fresh_link_is_valid_for_five_minutes_then_expires()
    {
        var urls = Create();
        var (expires, signature, inline) = Parse(urls.Create(FileId));

        urls.Verify(FileId, expires, signature, inline).ShouldBeTrue();
        expires.ShouldBe(_clock.GetUtcNow().AddMinutes(5).ToUnixTimeSeconds());

        _clock.Advance(TimeSpan.FromMinutes(5));
        urls.Verify(FileId, expires, signature, inline).ShouldBeTrue();

        _clock.Advance(TimeSpan.FromSeconds(1));
        urls.Verify(FileId, expires, signature, inline).ShouldBeFalse();
    }

    [Fact]
    public void BR_FILE_04_a_link_cannot_be_reused_for_another_file_a_later_expiry_or_inline_display()
    {
        var urls = Create();
        var (expires, signature, _) = Parse(urls.Create(FileId));

        urls.Verify(Guid.NewGuid(), expires, signature, inline: false).ShouldBeFalse();
        urls.Verify(FileId, expires + 3600, signature, inline: false).ShouldBeFalse();
        urls.Verify(FileId, expires, signature, inline: true).ShouldBeFalse();
        urls.Verify(FileId, expires, null, inline: false).ShouldBeFalse();
        urls.Verify(FileId, expires, signature[..^1] + (signature[^1] == 'A' ? 'B' : 'A'), inline: false).ShouldBeFalse();
    }

    [Fact]
    public void BR_FILE_02_inline_links_exist_only_when_asked_for()
    {
        var urls = Create();
        urls.Create(FileId).ShouldNotContain("inline");
        var (expires, signature, inline) = Parse(urls.Create(FileId, inline: true));
        inline.ShouldBeTrue();
        urls.Verify(FileId, expires, signature, inline: true).ShouldBeTrue();
    }

    [Fact]
    public void Production_requires_a_signing_key_of_at_least_32_bytes()
    {
        Should.Throw<InvalidOperationException>(() => new SignedUrls(
            Options.Create(new StorageOptions()), new TestEnvironment("Production"), _clock));
        Should.Throw<InvalidOperationException>(() => new SignedUrls(
            Options.Create(new StorageOptions { SigningKey = Convert.ToBase64String(new byte[16]) }),
            new TestEnvironment("Production"),
            _clock));
        Should.NotThrow(() => new SignedUrls(Options.Create(new StorageOptions()), new TestEnvironment("Development"), _clock));
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Nomax.Api";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
