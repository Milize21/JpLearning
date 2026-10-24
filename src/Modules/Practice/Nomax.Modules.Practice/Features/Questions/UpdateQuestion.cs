using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Persistence;
using Nomax.BuildingBlocks.Validation;
using Nomax.Modules.Practice.Domain;
using Nomax.Modules.Practice.Features.Common;
using Nomax.Modules.Practice.Infrastructure;
using Nomax.SharedKernel.Results;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Practice.Features.Questions;

/// <summary>
/// Partial update. <see cref="Kind"/>, <see cref="Payload"/>, <see cref="Category"/> and the chapter are content and stay
/// locked once students answered the question (BR-SOAL-04); the prompt, explanation and tags can always be corrected.
/// </summary>
public sealed record UpdateQuestionRequest(
    uint Version,
    TextBlock? Prompt = null,
    RichText? Explanation = null,
    bool ClearExplanation = false,
    IReadOnlyList<string>? Tags = null,
    QuestionKind? Kind = null,
    JsonElement? Payload = null,
    Category? Category = null,
    Guid? LevelId = null,
    Guid? ChapterId = null);

internal sealed class UpdateQuestionValidator : AbstractValidator<UpdateQuestionRequest>
{
    public UpdateQuestionValidator()
    {
        RuleFor(request => request.Prompt).ValidTextBlock(TextBlockUsage.Prompt).When(request => request.Prompt is not null);
        RuleFor(request => request.Explanation).ValidRichText().When(request => request.Explanation is not null);
        QuestionValidation.Tags(RuleFor(request => request.Tags));
        RuleFor(request => request.Kind).IsInEnum().When(request => request.Kind is not null);
        RuleFor(request => request.Category).IsInEnum().When(request => request.Category is not null);
        RuleFor(request => request.LevelId).NotNull().When(request => request.ChapterId is not null)
            .WithMessage("levelId and chapterId change together.");
        RuleFor(request => request.ChapterId).NotNull().When(request => request.LevelId is not null)
            .WithMessage("levelId and chapterId change together.");
        RuleFor(request => request.Payload).NotNull().When(request => request.Kind is not null)
            .WithMessage("A new kind needs a payload of that kind.");
    }
}

/// <summary>PATCH /api/practice/questions/{id} — owner or admin; optimistic concurrency with <c>version</c>.</summary>
internal sealed class UpdateQuestionHandler(PracticeDbContext db, QuestionStore store)
{
    public async Task<Result<QuestionDto>> HandleAsync(Guid id, UpdateQuestionRequest request, CancellationToken cancellationToken)
    {
        var found = await store.ManageableAsync(id, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var question = found.Value;
        if (db.ExpectVersion(question, request.Version) is { } stale)
        {
            return stale;
        }

        var hasAnswers = await store.HasAnswersAsync(id, cancellationToken);
        var changesContent = request.Kind is not null || request.Payload is not null || request.Category is not null || request.ChapterId is not null;
        if (changesContent)
        {
            if (!QuestionRules.CanEditContent(question.Status, hasAnswers))
            {
                return AppError.InvalidState("Students already answered this question; duplicate it to change its content (BR-SOAL-04).");
            }

            var kind = request.Kind ?? question.Kind;
            var payload = request.Payload ?? question.Payload;
            var issues = Payloads.Validate(kind, payload);
            if (issues.Count > 0)
            {
                return AppError.Validation(issues
                    .GroupBy(issue => issue.Path.Length == 0 ? "payload" : $"payload.{issue.Path}")
                    .ToDictionary(group => group.Key, group => group.Select(issue => issue.Message).ToArray()));
            }

            var levelId = request.LevelId ?? question.LevelId;
            var chapterId = request.ChapterId ?? question.ChapterId;
            if (request.ChapterId is not null && await store.CheckChapterAsync(levelId, chapterId, cancellationToken) is { } chapterError)
            {
                return chapterError;
            }

            question.UpdateContent(kind, payload, request.Category ?? question.Category, levelId, chapterId);
        }

        question.UpdateTexts(request.Prompt, request.Explanation, request.ClearExplanation, request.Tags is null ? null : QuestionValidation.CleanTags(request.Tags));
        if (await db.SaveChangesOrConflictAsync(cancellationToken) is { } conflict)
        {
            return conflict;
        }

        return QuestionDto.From(question, hasAnswers);
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPatch("/questions/{id:guid}", async (Guid id, UpdateQuestionRequest request, UpdateQuestionHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, request, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithValidation<UpdateQuestionRequest>()
            .WithName("UpdateQuestion")
            .WithSummary("Change a question; answered questions keep their content (owner, admin)")
            .Produces<QuestionDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
