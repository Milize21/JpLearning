using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
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
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Classroom.Features.Sessions;

/// <summary>Only the fields that are present change. An empty <see cref="MeetingUrl"/> or <see cref="Note"/> removes it.</summary>
public sealed record UpdateSessionRequest(
    string? Topic = null,
    DateTimeOffset? StartsAt = null,
    DateTimeOffset? EndsAt = null,
    SessionPlatform? Platform = null,
    string? MeetingUrl = null,
    string? Note = null,
    Guid? ChapterId = null,
    bool ClearChapter = false);

internal sealed class UpdateSessionValidator : AbstractValidator<UpdateSessionRequest>
{
    public UpdateSessionValidator()
    {
        RuleFor(request => request.Topic).Must(topic => topic is null || topic.Trim().Length is > 0 and <= ClassSession.TopicMaxLength)
            .WithMessage($"Must be 1–{ClassSession.TopicMaxLength} characters.");
        RuleFor(request => request.Platform).IsInEnum();
        RuleFor(request => request.MeetingUrl).HttpsUrl(ClassSession.UrlMaxLength).When(request => !string.IsNullOrEmpty(request.MeetingUrl));
        RuleFor(request => request.Note).MaximumLength(ClassSession.NoteMaxLength);
    }
}

/// <summary>
/// PATCH /api/classroom/sessions/{id} — teachers of the class and admins. Every change raises the ICS sequence; a change of
/// time, link or topic also notifies members (<c>session_updated</c>, BR-JADWAL-05). Pending reminders are planned again.
/// </summary>
internal sealed class UpdateSessionHandler(
    ClassroomDbContext db,
    ClassAccess access,
    IContentCatalog catalog,
    NotificationPlanner planner,
    TimeProvider timeProvider)
{
    public async Task<Result<SessionDto>> HandleAsync(Guid id, UpdateSessionRequest request, CancellationToken cancellationToken)
    {
        var session = await db.ClassSessions.FirstOrDefaultAsync(session => session.Id == id, cancellationToken);
        if (session is null)
        {
            return AppError.NotFound();
        }

        var found = await access.ForTeacherAsync(session.ClassId, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        if (session.Status == SessionStatus.Cancelled)
        {
            return AppError.InvalidState("A cancelled session cannot be changed.");
        }

        var startsAt = request.StartsAt?.ToUniversalTime() ?? session.StartsAt;
        var endsAt = request.EndsAt?.ToUniversalTime() ?? session.EndsAt;
        var meetingUrl = request.MeetingUrl is null ? session.MeetingUrl : request.MeetingUrl.Length == 0 ? null : request.MeetingUrl;
        if (ScheduleRules.Validate(startsAt, endsAt, meetingUrl, found.Value.Status) is { } invalid)
        {
            return invalid.Field == "classId"
                ? AppError.InvalidState(invalid.Message)
                : AppError.Validation(new Dictionary<string, string[]> { [invalid.Field] = [invalid.Message] });
        }

        var chapterId = request.ClearChapter ? null : request.ChapterId ?? session.ChapterId;
        if (request.ChapterId is { } newChapter && (await catalog.GetChapterAsync(newChapter, cancellationToken))?.BookId != found.Value.BookId)
        {
            return AppError.Validation(new Dictionary<string, string[]> { ["chapterId"] = ["Must be a chapter of the class's book."] });
        }

        var previous = new SessionSnapshot(session.Topic, session.StartsAt, session.EndsAt, session.MeetingUrl);
        var topic = request.Topic?.Trim() ?? session.Topic;
        var note = request.Note is null ? session.Note : request.Note.Trim().Length == 0 ? null : request.Note.Trim();
        session.Update(chapterId, topic, startsAt, endsAt, request.Platform ?? session.Platform, meetingUrl, note);

        var notify = previous.Topic != topic || previous.StartsAt != startsAt || previous.EndsAt != endsAt || previous.MeetingUrl != meetingUrl;
        await planner.SessionUpdatedAsync(session, previous, notify, cancellationToken);
        db.AddIntegrationEvent(new SessionUpdated(session.Id, session.ClassId, session.StartsAt, session.EndsAt, session.Sequence), timeProvider);
        await db.SaveChangesAsync(cancellationToken);
        return SessionDto.From(session, found.Value.Name);
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPatch("/sessions/{id:guid}", async (Guid id, UpdateSessionRequest request, UpdateSessionHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, request, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithValidation<UpdateSessionRequest>()
            .WithName("UpdateSession")
            .WithSummary("Change a session; members are told about time, link or topic changes (teachers, admin)")
            .Produces<SessionDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
