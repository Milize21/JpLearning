using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.Modules.Practice.Domain;
using Nomax.Modules.Practice.Features.Common;
using Nomax.Modules.Practice.Infrastructure;
using Nomax.SharedKernel.Identifiers;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Practice.Features.Questions;

public sealed record BulkPublishRequest(IReadOnlyList<Guid> QuestionIds);

public sealed record BulkPublishResponse(int Published, IReadOnlyList<Guid> Skipped);

/// <summary>
/// Status actions of BR-SOAL-02 — POST /questions/{id}/submit-review · /publish · /archive · /duplicate (owner, admin) and
/// POST /questions/publish (admin, reviewing generated questions in bulk).
/// </summary>
internal sealed class QuestionActionsHandler(PracticeDbContext db, PracticeAccess access, QuestionStore store, TimeProvider timeProvider)
{
    public Task<Result<QuestionDto>> SubmitForReviewAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, question => QuestionRules.CanSubmitForReview(question.Status), QuestionStatus.Review, cancellationToken);

    public Task<Result<QuestionDto>> PublishAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, question => QuestionRules.CanPublish(question.Status, question.Scope, access.IsAdmin), QuestionStatus.Published, cancellationToken);

    public Task<Result<QuestionDto>> ArchiveAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAsync(id, question => QuestionRules.CanArchive(question.Status), QuestionStatus.Archived, cancellationToken);

    /// <summary>
    /// BR-SOAL-04: a draft copy owned by the caller. Sensei copies are private; an answered original owned by the caller is
    /// archived, so changing an answered question means "duplicate, edit, publish".
    /// </summary>
    public async Task<Result<QuestionDto>> DuplicateAsync(Guid id, CancellationToken cancellationToken)
    {
        var found = await store.VisibleAsync(id, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var original = found.Value;
        var scope = access.IsAdmin ? original.Scope : QuestionScope.Private;
        var copy = original.Duplicate(Uuid7.New(timeProvider), access.UserId, scope);
        db.Questions.Add(copy);
        if (QuestionRules.CanManage(original.Scope, original.OwnerId, access.UserId, access.IsAdmin)
            && original.Status != QuestionStatus.Archived
            && await store.HasAnswersAsync(id, cancellationToken))
        {
            original.ChangeStatus(QuestionStatus.Archived);
        }

        await db.SaveChangesAsync(cancellationToken);
        return QuestionDto.From(copy, hasAnswers: false);
    }

    public async Task<Result<BulkPublishResponse>> PublishManyAsync(BulkPublishRequest request, CancellationToken cancellationToken)
    {
        var ids = (request.QuestionIds ?? []).Distinct().Take(500).ToList();
        var questions = await db.Questions.Where(question => ids.Contains(question.Id)).ToListAsync(cancellationToken);
        var skipped = ids.Except(questions.Select(question => question.Id)).ToList();
        var published = 0;
        foreach (var question in questions)
        {
            if (QuestionRules.CanPublish(question.Status, question.Scope, isAdmin: true))
            {
                question.ChangeStatus(QuestionStatus.Published);
                published++;
            }
            else
            {
                skipped.Add(question.Id);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new BulkPublishResponse(published, skipped);
    }

    private async Task<Result<QuestionDto>> ChangeAsync(Guid id, Func<Question, bool> allowed, QuestionStatus status, CancellationToken cancellationToken)
    {
        var found = await store.ManageableAsync(id, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var question = found.Value;
        if (!allowed(question))
        {
            return question.Scope == QuestionScope.Builtin && status == QuestionStatus.Published && !access.IsAdmin
                ? AppError.Forbidden("Only admins publish builtin questions.")
                : AppError.InvalidState($"A {question.Status} question cannot become {status}.");
        }

        question.ChangeStatus(status);
        await db.SaveChangesAsync(cancellationToken);
        return QuestionDto.From(question, await store.HasAnswersAsync(id, cancellationToken));
    }

    public static void Map(IEndpointRouteBuilder group)
    {
        Configure(group.MapPost("/questions/{id:guid}/submit-review", async (Guid id, QuestionActionsHandler handler, CancellationToken cancellationToken) =>
                (await handler.SubmitForReviewAsync(id, cancellationToken)).ToOkResult()), "SubmitQuestionForReview", "Draft → review (owner, admin)");
        Configure(group.MapPost("/questions/{id:guid}/publish", async (Guid id, QuestionActionsHandler handler, CancellationToken cancellationToken) =>
                (await handler.PublishAsync(id, cancellationToken)).ToOkResult()), "PublishQuestion", "Publish; builtin questions by admins only (owner, admin)");
        Configure(group.MapPost("/questions/{id:guid}/archive", async (Guid id, QuestionActionsHandler handler, CancellationToken cancellationToken) =>
                (await handler.ArchiveAsync(id, cancellationToken)).ToOkResult()), "ArchiveQuestion", "Archive (owner, admin)");
        group.MapPost("/questions/{id:guid}/duplicate", async (Guid id, QuestionActionsHandler handler, CancellationToken cancellationToken) =>
                (await handler.DuplicateAsync(id, cancellationToken))
                    .ToHttpResult(question => TypedResults.Created($"/api/practice/questions/{question.Id}", question)))
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("DuplicateQuestion")
            .WithSummary("Copy as a draft owned by the caller; an answered original of the caller is archived (staff)")
            .Produces<QuestionDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/questions/publish", async (BulkPublishRequest request, QuestionActionsHandler handler, CancellationToken cancellationToken) =>
                (await handler.PublishManyAsync(request, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.AdminOnly)
            .WithName("PublishQuestions")
            .WithSummary("Publish many questions at once, e.g. reviewed generator output (admin)")
            .Produces<BulkPublishResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static void Configure(RouteHandlerBuilder builder, string name, string summary) =>
        builder
            .RequireAuthorization(Policies.StaffOnly)
            .WithName(name)
            .WithSummary(summary)
            .Produces<QuestionDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
