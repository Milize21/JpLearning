using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Nomax.BuildingBlocks.Errors;
using Nomax.SharedKernel.Results;

namespace Nomax.BuildingBlocks.Auth;

/// <summary>
/// Rejects requests from deactivated accounts with 403 <c>account_inactive</c> (docs/01 §6, BR-AKUN-05).
/// Keycloak also blocks their login, but an access token stays valid for up to five minutes.
/// </summary>
internal sealed class ActiveProfileMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!RequiresActiveProfile(context))
        {
            await next(context);
            return;
        }

        if (context.User.FindUserId() is not { } userId)
        {
            await ProblemResponses.WriteAsync(context, ErrorCodes.Unauthorized, "The token has no valid subject.");
            return;
        }

        // Fail closed: without a reader no signed-in request may pass.
        var reader = context.RequestServices.GetService<IAccountStatusReader>()
            ?? throw new InvalidOperationException(
                "No IAccountStatusReader is registered; the Identity module must provide one before authenticated endpoints can be served.");

        if (!await reader.IsActiveAsync(userId, context.RequestAborted))
        {
            await ProblemResponses.WriteAsync(context, ErrorCodes.AccountInactive, "The account is deactivated.");
            return;
        }

        await next(context);
    }

    // Anonymous endpoints (health, ICS feed) and unmatched routes are not checked.
    private static bool RequiresActiveProfile(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true
        && context.GetEndpoint() is { } endpoint
        && endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null;
}
