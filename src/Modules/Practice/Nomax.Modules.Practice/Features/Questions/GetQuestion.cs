using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.Modules.Practice.Domain;
using Nomax.Modules.Practice.Features.Common;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Practice.Features.Questions;

/// <summary>
/// GET /api/practice/questions/{id} — staff who may see it (BR-SOAL-03); GET /questions/{id}/student-view shows the
/// version students receive (docs/03 §8.3) so authors can check that nothing leaks.
/// </summary>
internal sealed class GetQuestionHandler(QuestionStore store)
{
    public async Task<Result<QuestionDto>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var found = await store.VisibleAsync(id, cancellationToken);
        return found.IsSuccess
            ? QuestionDto.From(found.Value, await store.HasAnswersAsync(id, cancellationToken))
            : found.Error;
    }

    public async Task<Result<SafeQuestion>> StudentViewAsync(Guid id, CancellationToken cancellationToken)
    {
        var found = await store.VisibleAsync(id, cancellationToken);
        return found.IsSuccess ? SafeQuestions.For(found.Value, SafeQuestions.SeededRandom(id, id)) : found.Error;
    }

    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/questions/{id:guid}", async (Guid id, GetQuestionHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("GetQuestion")
            .WithSummary("One question with its payload and key (staff who may see it)")
            .Produces<QuestionDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/questions/{id:guid}/student-view", async (Guid id, GetQuestionHandler handler, CancellationToken cancellationToken) =>
                (await handler.StudentViewAsync(id, cancellationToken)).ToOkResult())
            .RequireAuthorization(Policies.StaffOnly)
            .WithName("GetQuestionStudentView")
            .WithSummary("The question as students receive it, without the key (staff)")
            .Produces<SafeQuestion>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
