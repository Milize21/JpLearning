using System.Text.Json;
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
using Nomax.SharedKernel.Identifiers;
using Nomax.SharedKernel.Results;
using Nomax.SharedKernel.Text;

namespace Nomax.Modules.Practice.Features.Questions;

/// <param name="Scope">Admins may create <c>builtin</c> questions; sensei questions are always <c>private</c> (BR-SOAL-03).</param>
/// <param name="SourceId">Admins only; default "Internal NomaX" (BR-SOAL-01).</param>
public sealed record CreateQuestionRequest(
    QuestionKind Kind,
    Guid LevelId,
    Guid ChapterId,
    Category Category,
    TextBlock Prompt,
    JsonElement Payload,
    IReadOnlyList<string>? Tags = null,
    RichText? Explanation = null,
    RefItemType? ItemType = null,
    Guid? ItemId = null,
    QuestionScope? Scope = null,
    Guid? SourceId = null);

internal sealed class CreateQuestionValidator : AbstractValidator<CreateQuestionRequest>
{
    public CreateQuestionValidator()
    {
        RuleFor(request => request.Kind).IsInEnum();
        RuleFor(request => request.Category).IsInEnum();
        RuleFor(request => request.LevelId).NotEmpty();
        RuleFor(request => request.ChapterId).NotEmpty();
        RuleFor(request => request.Prompt).ValidTextBlock(TextBlockUsage.Prompt);
        QuestionValidation.Tags(RuleFor(request => request.Tags));
        RuleFor(request => request.Explanation).ValidRichText().When(request => request.Explanation is not null);
        RuleFor(request => request.ItemId).NotNull().When(request => request.ItemType is not null)
            .WithMessage("itemType and itemId go together.");
        RuleFor(request => request.ItemType).NotNull().When(request => request.ItemId is not null)
            .WithMessage("itemType and itemId go together.");
        RuleFor(request => request).Custom((request, context) =>
        {
            if (!Enum.IsDefined(request.Kind))
            {
                return;
            }

            foreach (var issue in Payloads.Validate(request.Kind, request.Payload))
            {
                context.AddFailure(issue.Path.Length == 0 ? "payload" : $"payload.{issue.Path}", issue.Message);
            }
        });
    }
}

/// <summary>POST /api/practice/questions — staff. Sensei questions are private and start as draft (BR-SOAL-02/03).</summary>
internal sealed class CreateQuestionHandler(PracticeDbContext db, PracticeAccess access, QuestionStore store, TimeProvider timeProvider)
{
    public async Task<Result<QuestionDto>> HandleAsync(CreateQuestionRequest request, CancellationToken cancellationToken)
    {
        var scope = request.Scope ?? QuestionScope.Private;
        if ((scope == QuestionScope.Builtin || request.SourceId is not null) && !access.IsAdmin)
        {
            return AppError.Forbidden("Only admins create builtin questions or choose a source.");
        }

        if (await store.CheckChapterAsync(request.LevelId, request.ChapterId, cancellationToken) is { } chapterError)
        {
            return chapterError;
        }

        if (await store.CheckItemAsync(request.ItemType, request.ItemId, cancellationToken) is { } itemError)
        {
            return itemError;
        }

        Guid sourceId;
        if (request.SourceId is { } chosen)
        {
            if (!await db.QuestionSources.AnyAsync(source => source.Id == chosen, cancellationToken))
            {
                return AppError.Validation(new Dictionary<string, string[]> { ["sourceId"] = ["No such source."] });
            }

            sourceId = chosen;
        }
        else
        {
            sourceId = await store.InternalSourceIdAsync(cancellationToken);
        }

        var question = Question.Create(
            Uuid7.New(timeProvider), request.Kind, request.LevelId, request.ChapterId, request.Category,
            QuestionValidation.CleanTags(request.Tags), request.Prompt, request.Payload, request.Explanation,
            request.ItemType, request.ItemId, QuestionStatus.Draft, scope, sourceId, access.UserId);
        db.Questions.Add(question);
        await db.SaveChangesAsync(cancellationToken);
        return QuestionDto.From(question, hasAnswers: false);
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("/questions", async (CreateQuestionRequest request, CreateQuestionHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(request, cancellationToken))
                    .ToHttpResult(question => TypedResults.Created($"/api/practice/questions/{question.Id}", question)))
            .RequireAuthorization(Policies.StaffOnly)
            .WithValidation<CreateQuestionRequest>()
            .WithName("CreateQuestion")
            .WithSummary("Create a question; the payload is validated per kind (staff)")
            .Produces<QuestionDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
}
