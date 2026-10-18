using System.Net;
using Nomax.IntegrationTests.Support;
using Nomax.SharedKernel;
using static Nomax.IntegrationTests.Support.HttpAssertions;

namespace Nomax.IntegrationTests.Api;

/// <summary>
/// Until a module provides IAccountStatusReader, signed-in endpoints must fail closed instead of skipping the check.
/// </summary>
public sealed class MissingAccountStatusReaderTests
{
    [Fact]
    public async Task Signed_in_endpoint_fails_closed()
    {
        await using var host = await ProbeApiHost.StartAsync(accountStatusReader: null);
        using var client = host.CreateClient();

        using var response = await client.SendAsync(
            Get("/api/probe/authenticated", TestUsers.TokenFor(Roles.Admin)),
            TestContext.Current.CancellationToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.InternalServerError, "internal_error");
    }

    [Fact]
    public async Task Anonymous_endpoint_still_works()
    {
        await using var host = await ProbeApiHost.StartAsync(accountStatusReader: null);
        using var client = host.CreateClient();

        using var response = await client.SendAsync(Get("/health/live"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
