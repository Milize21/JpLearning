using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Messaging;
using Nomax.BuildingBlocks.Validation;
using Nomax.Modules.Classroom.Contracts;
using Nomax.Modules.Classroom.Domain;
using Nomax.Modules.Classroom.Features.Common;
using Nomax.Modules.Classroom.Infrastructure;
using Nomax.Modules.Classroom.Infrastructure.Notifications;
using Nomax.Modules.Content.Contracts;
using Nomax.SharedKernel.Identifiers;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Classroom.Features.Sessions;

/// <param name="Timezone">IANA zone of the creator, for display (default Asia/Jakarta).</param>
public sealed record CreateSessionRequest(
    string Topic,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    SessionPlatform Platform,
    string? MeetingUrl = null,
    string? Note = null,
    Guid? ChapterId = null,
    string? Timezone = null);

internal sealed class CreateSessionValidator : AbstractValidator<CreateSessionRequest>
{
    public CreateSessionValidator()
    {
        RuleFor(request => request.Topic).Must(topic => topic?.Trim().Length is > 0 and <= ClassSession.TopicMaxLength)
            .WithMessage($"Must be 1–{ClassSession.TopicMaxLength} characters.");
        RuleFor(request => request.Platform).IsInEnum();
        RuleFor(request => request.MeetingUrl).HttpsUrl(ClassSession.UrlMaxLength).When(request => !string.IsNullOrEmpty(request.MeetingUrl));
        RuleFor(request => request.Note).MaximumLength(ClassSession.NoteMaxLength);
        RuleFor(request => request.Timezone)
            .Must(zone => zone is null || (zone.Length <= ClassSession.TimezoneMaxLength && TimeZoneInfo.TryFindSystemTimeZoneById(zone, out _)))
            .WithMessage("Must be an IANA time zone such as Asia/Jakarta.");
    }
}

/// <summary>
/// POST /api/classroom/classes/{id}/sessions — teachers of the class and admins. BR-JADWAL-01 validity, BR-JADWAL-05
/// <c>session_created</c> to all members and BR-JADWAL-06 reminders, saved with the session and the SessionCreated event.
/// </summary>
internal sealed class CreateSessionHandler(
    ClassroomDbContext db,
    ClassAccess access,
    IContentCatalog catalog,
    NotificationPlanner planner,
    TimeProvider timeProvider)
{
    public async Task<Result<SessionDto>> HandleAsync(Guid classId, CreateSessionRequest request, CancellationToken cancellationToken)
    {
        var found = await access.ForTeacherAsync(classId, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var item = found.Value;
        var meetingUrl = string.IsNullOrEmpty(request.MeetingUrl) ? null : request.MeetingUrl;
        if (ScheduleRules.Validate(request.StartsAt, request.EndsAt, meetingUrl, item.Status) is { } invalid)
        {
            return invalid.Field == "classId"
                ? AppError.InvalidState(invalid.Message)
                : AppError.Validation(new Dictionary<string, string[]> { [invalid.Field] = [invalid.Message] });
        }

        if (request.ChapterId is { } chapterId && (await catalog.GetChapterAsync(chapterId, cancellationToken))?.BookId != item.BookId)
        {
            return AppError.Validation(new Dictionary<string, string[]> { ["chapterId"] = ["Must be a chapter of the class's book."] });
        }

        var session = ClassSession.Create(
            Uuid7.New(timeProvider), classId, request.ChapterId, request.Topic, request.StartsAt.ToUniversalTime(), request.EndsAt.ToUniversalTime(),
            request.Timezone ?? "Asia/Jakarta", request.Platform, meetingUrl, string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim());
        db.ClassSessions.Add(session);
        await planner.SessionCreatedAsync(session, cancellationToken);
        db.AddIntegrationEvent(new SessionCreated(session.Id, classId, session.StartsAt, session.EndsAt, session.Sequence), timeProvider);
        await db.SaveChangesAsync(cancellationToken);
        return SessionDto.From(session, item.Name);
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("/classes/{id:guid}/sessions", async (Guid id, CreateSessionRequest request, CreateSessionHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, request, cancellationToken))
                    .ToHttpResult(session => TypedResults.Created($"/api/classroom/sessions/{session.Id}", session)))
            .RequireAuthorization(Policies.StaffOnly)
            .WithValidation<CreateSessionRequest>()
            .WithName("CreateSession")
            .WithSummary("Schedule a session; members are notified and reminded (teachers, admin)")
            .Produces<SessionDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
