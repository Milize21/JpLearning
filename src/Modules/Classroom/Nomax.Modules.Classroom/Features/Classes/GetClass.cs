using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nomax.BuildingBlocks.Errors;
using Nomax.Modules.Classroom.Features.Common;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Classroom.Features.Classes;

/// <summary>GET /api/classroom/classes/{id} — members, teachers and admins; others get 404.</summary>
internal sealed class GetClassHandler(ClassAccess access, ClassViews views)
{
    public async Task<Result<ClassDto>> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await access.ForMemberAsync(id, cancellationToken);
        return item.IsSuccess ? await views.BuildAsync(item.Value, access.UserId, cancellationToken) : item.Error;
    }

    public static void Map(IEndpointRouteBuilder group) =>
        group.MapGet("/classes/{id:guid}", async (Guid id, GetClassHandler handler, CancellationToken cancellationToken) =>
                (await handler.HandleAsync(id, cancellationToken)).ToOkResult())
            .WithName("GetClass")
            .WithSummary("A class the caller teaches or belongs to")
            .Produces<ClassDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
}
