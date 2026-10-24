using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.Modules.Practice.Domain;
using Nomax.Modules.Practice.Features.Common;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Practice.Features.Questions;

/// <param name="Answer">Shape of docs/03 §8.2 for the question's kind.</param>
public sealed record PreviewAnswerRequest(JsonElement Answer);

/// <summary>
/// POST /api/practice/questions/{id}/preview-answer — staff try a question as a student: the answer is evaluated
/// (BR-DRILL-05) and nothing is stored, no mastery and no event (BR-AKSES-05).
/// </summary>
internal sealed class PreviewAnswerHandler(QuestionStore store)
{
    public async Task<Result<AnswerResultDto>> HandleAsync(Guid id, PreviewAnswerRequest request, CancellationToken cancellationToken)
    {
        var found = await store.VisibleAsync(id, cancellationToken);
        if (!found.IsSuccess)
        {
            return found.Error;
        }

        var question = found.Value;
        if (question.Kind == QuestionKind.LongAnswer)
        {
            return AppError.InvalidState("Long answers are graded by a teacher.");
        }

        var evaluation = Evaluate(question, request.Answer);
        return evaluation.IsSuccess
            ? new AnswerResultDto(evaluation.Value.IsCorrect, evaluation.Value.CorrectAnswer, question.Explanation, null)
            : evaluation.Error;
    }

    /// <summary>Evaluates or reports a wrongly shaped answer as 400 on <c>answer</c>.</summary>
    public static Result<Evaluation> Evaluate(Question question, JsonElement answer)
    {
        try
        {
            return AnswerEvaluator.Evaluate(question.Kind, question.Payload, answer);
        }
        catch (JsonException)
        {
            return AppError.Validation(new Dictionary<string, string[]> { ["answer"] = ["Does not match the answer shape of this question kind."] });
        }
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapPost("/questions/{id:guid}/preview-answer", async (Guid id, PreviewAnswerRequest request, PreviewAnswerHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, request, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("PreviewQuestionAnswer")
            .WithSummary("Evaluate an answer without saving anything (staff preview)")
            .Produces<AnswerResultDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
}
