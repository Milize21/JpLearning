using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.Modules.Classroom.Domain;
using Nomax.Modules.Classroom.Features.Sessions;
using Nomax.Modules.Classroom.Infrastructure;
using Nomax.Modules.Classroom.Infrastructure.Notifications;
using Nomax.SharedKernel.Results;
using Nomax.SharedKernel.Serialization;

namespace Nomax.Modules.Classroom.Features.Calendar;

/// <param name="Url">The full subscription URL. Shown once; only its hash is stored (BR-JADWAL-04).</param>
public sealed record CalendarTokenDto(string Url);

/// <param name="Active">Whether the user has a feed token; the URL itself cannot be shown again.</param>
public sealed record CalendarTokenStatusDto(bool Active, DateTimeOffset? CreatedAt);

/// <summary>
/// Personal ICS feed (BR-JADWAL-03/04): GET/POST/DELETE /api/classroom/me/calendar-token and the public
/// GET /ical/{token}.ics. The token is 32 random bytes (base64url); an unknown token is 404.
/// </summary>
internal sealed class CalendarFeedHandler(
    ClassroomDbContext db,
    ICurrentUser currentUser,
    IOptions<AppOptions> appOptions,
    IOptions<ScheduleOptions> scheduleOptions,
    TimeProvider timeProvider)
{
    public async Task<CalendarTokenStatusDto> StatusAsync(CancellationToken cancellationToken)
    {
        var token = await db.CalendarFeedTokens.AsNoTracking().FirstOrDefaultAsync(token => token.UserId == currentUser.Id, cancellationToken);
        return new CalendarTokenStatusDto(token is not null, token?.UpdatedAt);
    }

    /// <summary>Creates the token, or rotates it: the old URL stops working at once.</summary>
    public async Task<CalendarTokenDto> CreateAsync(CancellationToken cancellationToken)
    {
        var token = Base64Url.Encode(RandomNumberGenerator.GetBytes(32));
        var hash = Hash(token);
        var existing = await db.CalendarFeedTokens.FirstOrDefaultAsync(entry => entry.UserId == currentUser.Id, cancellationToken);
        if (existing is null)
        {
            db.CalendarFeedTokens.Add(CalendarFeedToken.Create(currentUser.Id, hash));
        }
        else
        {
            existing.Rotate(hash);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new CalendarTokenDto($"{appOptions.Value.PublicUrl.TrimEnd('/')}/ical/{token}.ics");
    }

    public async Task RevokeAsync(CancellationToken cancellationToken) =>
        await db.CalendarFeedTokens.Where(entry => entry.UserId == currentUser.Id).ExecuteDeleteAsync(cancellationToken);

    public async Task<IResult> FeedAsync(string token, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var hash = Hash(token);
        var entry = await db.CalendarFeedTokens.AsNoTracking().FirstOrDefaultAsync(entry => entry.TokenHash == hash, cancellationToken);
        if (entry is null)
        {
            return AppError.NotFound().ToProblem();
        }

        var now = timeProvider.GetUtcNow();
        var options = scheduleOptions.Value;
        var sessions = await ScheduleQueries.ForUser(db, entry.UserId, now.AddDays(-options.IcsPastDays), now.AddDays(options.IcsFutureDays))
            .ToListAsync(cancellationToken);
        var calendar = IcsCalendar.Write(
            sessions.Select(session => new IcsEvent(
                session.Id, session.Sequence, session.StartsAt, session.EndsAt, session.ClassName, session.Topic,
                PlatformLabel(session.Platform), session.MeetingUrl, session.Note, session.Status == SessionStatus.Cancelled)),
            now,
            "NomaX JP Learn");

        httpContext.Response.Headers[HeaderNames.CacheControl] = "private, max-age=300";
        return Results.Text(calendar, "text/calendar; charset=utf-8", Encoding.UTF8);
    }

    private static string PlatformLabel(SessionPlatform platform) => platform switch
    {
        SessionPlatform.Zoom => "Zoom",
        SessionPlatform.Discord => "Discord",
        _ => EnumSnakeCase.ToValue(platform),
    };

    private static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static void Map(IEndpointRouteBuilder group, IEndpointRouteBuilder root)
    {
        group.MapGet("/me/calendar-token", async (CalendarFeedHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.StatusAsync(cancellationToken)))
            .WithName("GetCalendarTokenStatus")
            .WithSummary("Whether the caller has a calendar feed")
            .Produces<CalendarTokenStatusDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/me/calendar-token", async (CalendarFeedHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.CreateAsync(cancellationToken)))
            .WithName("CreateCalendarToken")
            .WithSummary("Create or rotate the personal ICS feed; the URL is shown once")
            .Produces<CalendarTokenDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapDelete("/me/calendar-token", async (CalendarFeedHandler handler, CancellationToken cancellationToken) =>
            {
                await handler.RevokeAsync(cancellationToken);
                return TypedResults.NoContent();
            })
            .WithName("RevokeCalendarToken")
            .WithSummary("Revoke the personal ICS feed")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // Public: calendar apps fetch it without a login; the unguessable token is the credential.
        root.MapGet("/ical/{token}.ics", (string token, HttpContext httpContext, CalendarFeedHandler handler, CancellationToken cancellationToken) =>
                handler.FeedAsync(token, httpContext, cancellationToken))
            .AllowAnonymous()
            .WithGroupName("classroom")
            .WithName("GetCalendarFeed")
            .WithSummary("ICS feed of a user's sessions (token in the URL)")
            .Produces(StatusCodes.Status200OK, contentType: "text/calendar")
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
