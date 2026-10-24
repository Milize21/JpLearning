using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Persistence;
using Nomax.BuildingBlocks.Validation;
using Nomax.Modules.Practice.Domain;
using Nomax.Modules.Practice.Features.Common;
using Nomax.Modules.Practice.Infrastructure;
using Nomax.SharedKernel.Paging;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Practice.Features.Questions;

/// <param name="Q">Searches the Indonesian prompt (case-insensitive).</param>
public sealed record ListQuestionsQuery(
    [property: FromQuery(Name = "levelId")] Guid? LevelId,
    [property: FromQuery(Name = "chapterId")] Guid? ChapterId,
    [property: FromQuery(Name = "category")] string? Category,
    [property: FromQuery(Name = "kind")] string? Kind,
    [property: FromQuery(Name = "status")] string? Status,
    [property: FromQuery(Name = "scope")] string? Scope,
    [property: FromQuery(Name = "q")] string? Q,
    [property: FromQuery(Name = "page")] int? Page,
    [property: FromQuery(Name = "pageSize")] int? PageSize);

/// <summary>
/// GET /api/practice/questions — staff. Sensei see builtin questions and their own; admins see all (BR-SOAL-03).
/// Filtered and paged in SQL (docs/01 §5).
/// </summary>
internal sealed class ListQuestionsHandler(PracticeDbContext db, PracticeAccess access, QuestionStore store)
{
    public async Task<Result<PagedResult<QuestionDto>>> HandleAsync(ListQuestionsQuery query, CancellationToken cancellationToken)
    {
        var category = QueryEnums.Parse<Category>(query.Category, "category");
        var kind = QueryEnums.Parse<QuestionKind>(query.Kind, "kind");
        var status = QueryEnums.Parse<QuestionStatus>(query.Status, "status");
        var scope = QueryEnums.Parse<QuestionScope>(query.Scope, "scope");
        foreach (var parsed in new Result[] { category, kind, status, scope })
        {
            if (!parsed.IsSuccess)
            {
                return parsed.Error;
            }
        }

        var questions = string.IsNullOrWhiteSpace(query.Q)
            ? db.Questions.AsNoTracking()
            // TextBlock is a jsonb value object, so the search on its Indonesian text is SQL; xmin is the version (not in *).
            : db.Questions.FromSql($"SELECT *, xmin FROM practice.questions WHERE prompt->>'id' ILIKE {SqlLike.Contains(query.Q.Trim())} ESCAPE '\\'").AsNoTracking();

        if (!access.IsAdmin)
        {
            var me = access.UserId;
            questions = questions.Where(question => question.Scope == QuestionScope.Builtin || question.OwnerId == me);
        }

        if (query.LevelId is { } levelId)
        {
            questions = questions.Where(question => question.LevelId == levelId);
        }

        if (query.ChapterId is { } chapterId)
        {
            questions = questions.Where(question => question.ChapterId == chapterId);
        }

        if (category.Value is { } categoryValue)
        {
            questions = questions.Where(question => question.Category == categoryValue);
        }

        if (kind.Value is { } kindValue)
        {
            questions = questions.Where(question => question.Kind == kindValue);
        }

        if (status.Value is { } statusValue)
        {
            questions = questions.Where(question => question.Status == statusValue);
        }

        if (scope.Value is { } scopeValue)
        {
            questions = questions.Where(question => question.Scope == scopeValue);
        }

        var page = PageRequest.Create(query.Page, query.PageSize);
        var total = await questions.CountAsync(cancellationToken);
        var rows = await questions
            .OrderByDescending(question => question.CreatedAt)
            .ThenBy(question => question.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);
        var answered = await store.AnsweredAsync([.. rows.Select(row => row.Id)], cancellationToken);
        return new PagedResult<QuestionDto>([.. rows.Select(row => QuestionDto.From(row, answered.Contains(row.Id)))], page.Page, page.PageSize, total);
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/questions", async ([AsParameters] ListQuestionsQuery query, ListQuestionsHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(query, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("ListQuestions")
            .WithSummary("Question bank with filters; sensei see builtin + their own (staff)")
            .Produces<PagedResult<QuestionDto>>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
}
