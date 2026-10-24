using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Validation;
using Nomax.Modules.Practice.Domain;
using Nomax.Modules.Practice.Features.Common;
using Nomax.Modules.Practice.Infrastructure;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Practice.Features.Preview;

/// <param name="IncludeDrafts">Default: published only; authors may also try drafts and questions in review.</param>
public sealed record PreviewDrillRequest(Category Category, IReadOnlyList<Guid>? ChapterIds = null, Guid? LevelId = null, int? Count = null, bool IncludeDrafts = false);

internal sealed class PreviewDrillValidator : AbstractValidator<PreviewDrillRequest>
{
    public PreviewDrillValidator()
    {
        RuleFor(request => request.Category).IsInEnum();
        RuleFor(request => request.Count).InclusiveBetween(1, 50).When(request => request.Count is not null);
        RuleFor(request => request.ChapterIds).Must(ids => ids is null || ids.Count <= 200).WithMessage("At most 200 chapters.");
    }
}

/// <summary>
/// POST /api/practice/preview/drill — staff try drilling as a student would (BR-AKSES-05): a random batch of student
/// versions; answers go to <c>/questions/{id}/preview-answer</c>. Nothing is stored and no event is published.
/// </summary>
internal sealed class PreviewDrillHandler(PracticeDbContext db, PracticeAccess access)
{
    public async Task<Result<IReadOnlyList<SafeQuestion>>> HandleAsync(PreviewDrillRequest request, CancellationToken cancellationToken)
    {
        var questions = db.Questions.AsNoTracking().Where(question =>
            question.Category == request.Category
            && question.Kind != QuestionKind.LongAnswer
            && question.Kind != QuestionKind.HandwriteKana
            && question.Kind != QuestionKind.HandwriteKanji);
        questions = request.IncludeDrafts
            ? questions.Where(question => question.Status != QuestionStatus.Archived)
            : questions.Where(question => question.Status == QuestionStatus.Published);

        if (!access.IsAdmin)
        {
            var me = access.UserId;
            questions = questions.Where(question => question.Scope == QuestionScope.Builtin || question.OwnerId == me);
        }

        if (request.LevelId is { } levelId)
        {
            questions = questions.Where(question => question.LevelId == levelId);
        }

        if (request.ChapterIds is { Count: > 0 } chapterIds)
        {
            var ids = chapterIds.ToList();
            questions = questions.Where(question => ids.Contains(question.ChapterId));
        }

        var pool = await questions.Select(question => question.Id).Take(5000).ToListAsync(cancellationToken);
        var picked = DrillSelection.NormalBatch(pool, Guid.NewGuid(), 0, request.Count ?? 10);
        var rows = await db.Questions.AsNoTracking().Where(question => picked.Contains(question.Id)).ToDictionaryAsync(question => question.Id, cancellationToken);
        return Result.Success<IReadOnlyList<SafeQuestion>>([.. picked.Where(rows.ContainsKey).Select(id => SafeQuestions.For(rows[id], Random.Shared))]);
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("/preview/drill", async (PreviewDrillRequest request, PreviewDrillHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(request, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithValidation<PreviewDrillRequest>()
            .WithName("PreviewDrill")
            .WithSummary("Try drilling without saving anything (staff preview)")
            .Produces<IReadOnlyList<SafeQuestion>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
}
