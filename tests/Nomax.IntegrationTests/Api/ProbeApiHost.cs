using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Hosting;
using Nomax.BuildingBlocks.Validation;
using Nomax.IntegrationTests.Support;
using Nomax.SharedKernel;
using Nomax.SharedKernel.Results;

namespace Nomax.IntegrationTests.Api;

/// <summary>
/// The API pipeline exactly as Program.cs composes it (AddNomaxApiDefaults / UseNomaxApiDefaults / MapNomaxApiDefaults),
/// plus a test-only "probe" module whose endpoints exercise every policy and error path.
/// </summary>
internal sealed class ProbeApiHost : IAsyncDisposable
{
    public const string Module = "probe";

    private readonly WebApplication _app;

    private ProbeApiHost(WebApplication app) => _app = app;

    public static async Task<ProbeApiHost> StartAsync(IAccountStatusReader? accountStatusReader)
    {
        // An empty content root keeps the appsettings.json files copied from the referenced hosts out of this host.
        var contentRoot = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "probe-host")).FullName;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
            ContentRootPath = contentRoot,
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Keycloak:Authority"] = TestJwt.Issuer,
            ["ConnectionStrings:Postgres"] = "Host=127.0.0.1;Port=1;Database=unused",
            ["ConnectionStrings:Redis"] = "127.0.0.1:1",
            ["Serilog:MinimumLevel:Default"] = "Warning",
        });

        builder.AddNomaxApiDefaults();
        TestJwt.TrustTestKey(builder.Services);
        if (accountStatusReader is not null)
        {
            builder.Services.AddSingleton(accountStatusReader);
        }

        builder.Services.AddNomaxModule(Module);
        builder.Services.AddScoped<IValidator<ProbeRequest>, ProbeRequestValidator>();

        var app = builder.Build();
        app.UseNomaxApiDefaults();
        MapProbeEndpoints(app);
        app.MapNomaxApiDefaults();

        await app.StartAsync(TestContext.Current.CancellationToken);
        return new ProbeApiHost(app);
    }

    public HttpClient CreateClient() => _app.GetTestClient();

    public ValueTask DisposeAsync() => _app.DisposeAsync();

    private static void MapProbeEndpoints(WebApplication app)
    {
        var probe = app.MapModuleGroup(Module);
        probe.MapGet("/authenticated", () => TypedResults.Ok(new { ok = true }));
        probe.MapGet("/murid", () => TypedResults.Ok(new { ok = true })).RequireAuthorization(Policies.MuridOnly);
        probe.MapGet("/staff", () => TypedResults.Ok(new { ok = true })).RequireAuthorization(Policies.StaffOnly);
        probe.MapGet("/admin", () => TypedResults.Ok(new { ok = true })).RequireAuthorization(Policies.AdminOnly);
        probe.MapGet("/me", (ICurrentUser user) => TypedResults.Ok(new { id = user.Id, isAdmin = user.IsInRole(Roles.Admin) }));
        probe.MapPost("/validated", (ProbeRequest request) => TypedResults.Ok(request)).WithValidation<ProbeRequest>();
        probe.MapGet("/not-found", () => AppError.NotFound().ToProblem());
        probe.MapGet("/conflict", () => Result.Failure<int>(AppError.Conflict("Username is already taken.")).ToOkResult());
        probe.MapGet("/boom", IResult () => throw new InvalidOperationException("secret detail from the database"));
        probe.MapGet("/too-large", IResult () => throw new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge));
        probe.MapPut("/put-only", () => TypedResults.NoContent());
        probe.MapGet("/formats", () => TypedResults.Ok(new ProbeFormats(
            ProbeScriptPref.KanjiFuriganaRomaji,
            new DateTimeOffset(2026, 10, 18, 19, 30, 0, TimeSpan.FromHours(7)),
            "練習")));

        app.MapGet("/hubs/probe", () => TypedResults.Ok(new { ok = true })).RequireAuthorization(Policies.Authenticated);
        app.MapGet("/anonymous", () => TypedResults.Ok(new { ok = true })).AllowAnonymous();
    }
}

internal sealed record ProbeRequest(string DisplayName, int Count);

internal sealed class ProbeRequestValidator : AbstractValidator<ProbeRequest>
{
    public ProbeRequestValidator()
    {
        RuleFor(request => request.DisplayName).NotEmpty().MaximumLength(80);
        RuleFor(request => request.Count).InclusiveBetween(1, 10);
    }
}

internal enum ProbeScriptPref
{
    Off,
    KanjiFuriganaRomaji,
}

internal sealed record ProbeFormats(ProbeScriptPref ScriptPref, DateTimeOffset At, string Japanese);
