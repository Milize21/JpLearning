using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Validation;
using Nomax.Modules.Classroom.Contracts;
using Nomax.Modules.Identity.Contracts;
using Nomax.Modules.Practice.Domain;
using Nomax.Modules.Practice.Features.Common;
using Nomax.Modules.Practice.Infrastructure;
using Nomax.SharedKernel.Identifiers;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Practice.Features.Comments;

/// <param name="TargetId">The drill session id, or the class id for <c>progress</c>.</param>
public sealed record CreateCommentRequest(Guid StudentId, CommentTarget TargetType, Guid TargetId, string Text);

internal sealed class CreateCommentValidator : AbstractValidator<CreateCommentRequest>
{
    public CreateCommentValidator()
    {
        RuleFor(request => request.StudentId).NotEmpty();
        RuleFor(request => request.TargetId).NotEmpty();
        // Quiz attempts and submissions get comments with phase 3.
        RuleFor(request => request.TargetType).Must(target => target is CommentTarget.DrillSession or CommentTarget.Progress)
            .WithMessage("Must be drill_session or progress.");
        RuleFor(request => request.Text).Must(text => text?.Trim().Length is > 0 and <= Comment.TextMaxLength)
            .WithMessage($"Must be 1–{Comment.TextMaxLength} characters.");
    }
}

/// <summary>
/// Comments without a grade (BR-DRILL-01, docs/03 §7.14). GET /comments: students read those about themselves; staff
/// those they wrote and those about students they teach. POST /comments: teachers of the student and admins.
/// </summary>
internal sealed class CommentsHandler(
    PracticeDbContext db, PracticeAccess access, IClassAccessReader classes, IUserDirectory users, TimeProvider timeProvider)
{
    public async Task<Result<IReadOnlyList<CommentDto>>> ListAsync(string? targetType, Guid? targetId, Guid? studentId, CancellationToken cancellationToken)
    {
        var parsed = QueryEnums.Parse<CommentTarget>(targetType, "targetType");
        if (!parsed.IsSuccess)
        {
            return parsed.Error;
        }

        var comments = db.Comments.AsNoTracking();
        if (access.IsStudent)
        {
            comments = comments.Where(comment => comment.StudentId == access.UserId);
        }
        else if (studentId is { } student)
        {
            if (!await access.CanSeeStudentAsync(student, cancellationToken))
            {
                return Result.Success<IReadOnlyList<CommentDto>>([]);
            }

            comments = comments.Where(comment => comment.StudentId == student);
        }
        else if (!access.IsAdmin)
        {
            comments = comments.Where(comment => comment.AuthorId == access.UserId);
        }

        if (parsed.Value is { } type)
        {
            comments = comments.Where(comment => comment.TargetType == type);
        }

        if (targetId is { } target)
        {
            comments = comments.Where(comment => comment.TargetId == target);
        }

        var rows = await comments.OrderByDescending(comment => comment.CreatedAt).Take(200).ToListAsync(cancellationToken);
        var authors = await users.GetUsersAsync([.. rows.Select(comment => comment.AuthorId).Distinct()], cancellationToken);
        return Result.Success<IReadOnlyList<CommentDto>>([.. rows.Select(comment => ToDto(comment, authors))]);
    }

    public async Task<Result<CommentDto>> CreateAsync(CreateCommentRequest request, CancellationToken cancellationToken)
    {
        if (!await access.CanSeeStudentAsync(request.StudentId, cancellationToken))
        {
            return AppError.NotFound("No such student.");
        }

        var targetExists = request.TargetType switch
        {
            CommentTarget.DrillSession => await db.DrillSessions.AnyAsync(session => session.Id == request.TargetId && session.StudentId == request.StudentId, cancellationToken),
            CommentTarget.Progress => await access.CanTeachClassAsync(request.TargetId, cancellationToken)
                && await classes.IsStudentOfAsync(request.StudentId, request.TargetId, cancellationToken),
            _ => false,
        };
        if (!targetExists)
        {
            return AppError.NotFound("No such target for this student.");
        }

        var comment = Comment.Create(Uuid7.New(timeProvider), request.StudentId, access.UserId, request.TargetType, request.TargetId, request.Text);
        db.Comments.Add(comment);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(comment, await users.GetUsersAsync([access.UserId], cancellationToken));
    }

    private static CommentDto ToDto(Comment comment, IReadOnlyDictionary<Guid, UserSummary> authors) =>
        new(comment.Id, comment.StudentId, comment.AuthorId,
            authors.TryGetValue(comment.AuthorId, out var author) ? author.DisplayName : string.Empty,
            comment.TargetType, comment.TargetId, comment.Text, comment.CreatedAt);

    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/comments", async (
                [FromQuery(Name = "targetType")] string? targetType,
                [FromQuery(Name = "targetId")] Guid? targetId,
                [FromQuery(Name = "studentId")] Guid? studentId,
                CommentsHandler handler,
                CancellationToken cancellationToken) =>
                (await handler.ListAsync(targetType, targetId, studentId, cancellationToken)).ToOkResult())
            .WithName("ListComments")
            .WithSummary("Comments: students see those about themselves; staff those they may see")
            .Produces<IReadOnlyList<CommentDto>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/comments", async (CreateCommentRequest request, CommentsHandler handler, CancellationToken cancellationToken) =>
                (await handler.CreateAsync(request, cancellationToken))
                    .ToHttpResult(comment => TypedResults.Created($"/api/practice/comments/{comment.Id}", comment)))
            .RequireAuthorization(Policies.StaffOnly)
            .WithValidation<CreateCommentRequest>()
            .WithName("CreateComment")
            .WithSummary("Comment on a drill session or a student's progress, without a grade (teachers of the student, admin)")
            .Produces<CommentDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
