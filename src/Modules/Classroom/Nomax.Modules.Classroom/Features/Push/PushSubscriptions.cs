using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nomax.BuildingBlocks.Auth;
using Nomax.BuildingBlocks.Errors;
using Nomax.BuildingBlocks.Validation;
using Nomax.Modules.Classroom.Domain;
using Nomax.Modules.Classroom.Infrastructure;
using Nomax.Modules.Classroom.Infrastructure.Notifications;
using Nomax.SharedKernel.Identifiers;
using Nomax.SharedKernel.Results;

namespace Nomax.Modules.Classroom.Features.Push;

/// <param name="PublicKey">VAPID applicationServerKey (base64url); null when web push is not configured.</param>
public sealed record PushPublicKeyDto(string? PublicKey);

/// <summary>The browser's <c>PushSubscription.toJSON()</c> keys.</summary>
public sealed record PushKeys(string P256dh, string Auth);

public sealed record SubscribePushRequest(string Endpoint, PushKeys Keys, string? UserAgent = null);

public sealed record PushSubscriptionDto(Guid Id, string? UserAgent, DateTimeOffset CreatedAt, DateTimeOffset? LastSuccessAt);

internal sealed class SubscribePushValidator : AbstractValidator<SubscribePushRequest>
{
    public SubscribePushValidator()
    {
        RuleFor(request => request.Endpoint).HttpsUrl(4096);
        RuleFor(request => request.Endpoint).Must(PushServices.IsKnown).WithMessage("Must be the endpoint of a browser push service.");
        RuleFor(request => request.Keys).NotNull();
        RuleFor(request => request.Keys.P256dh).NotEmpty().MaximumLength(PushSubscription.P256dhMaxLength).When(request => request.Keys is not null);
        RuleFor(request => request.Keys.Auth).NotEmpty().MaximumLength(PushSubscription.AuthMaxLength).When(request => request.Keys is not null);
    }
}

/// <summary>
/// The server posts to the subscription endpoint, so only the push services of browsers are accepted; anything else could
/// turn reminders into requests against internal hosts (SSRF).
/// </summary>
internal static class PushServices
{
    private static readonly string[] Hosts =
        ["fcm.googleapis.com", "push.services.mozilla.com", "push.apple.com", "notify.windows.com"];

    public static bool IsKnown(string? endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && Hosts.Any(host => uri.Host == host || uri.Host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Web push devices of the caller (docs/05 §6). Each browser endpoint exists once.</summary>
internal sealed class PushSubscriptionsHandler(ClassroomDbContext db, ICurrentUser currentUser, IOptions<WebPushOptions> options, TimeProvider timeProvider)
{
    public PushPublicKeyDto PublicKey() => new(options.Value.IsConfigured ? options.Value.PublicKey : null);

    public async Task<IReadOnlyList<PushSubscriptionDto>> ListAsync(CancellationToken cancellationToken) =>
        await db.PushSubscriptions.AsNoTracking()
            .Where(subscription => subscription.UserId == currentUser.Id)
            .OrderByDescending(subscription => subscription.CreatedAt)
            .Select(subscription => new PushSubscriptionDto(subscription.Id, subscription.UserAgent, subscription.CreatedAt, subscription.LastSuccessAt))
            .ToListAsync(cancellationToken);

    public async Task<PushSubscriptionDto> SubscribeAsync(SubscribePushRequest request, CancellationToken cancellationToken)
    {
        var subscription = await db.PushSubscriptions.FirstOrDefaultAsync(subscription => subscription.Endpoint == request.Endpoint, cancellationToken);
        if (subscription is null)
        {
            subscription = PushSubscription.Create(Uuid7.New(timeProvider), currentUser.Id, request.Endpoint, request.Keys.P256dh, request.Keys.Auth, request.UserAgent);
            db.PushSubscriptions.Add(subscription);
        }
        else
        {
            subscription.Renew(currentUser.Id, request.Keys.P256dh, request.Keys.Auth, request.UserAgent);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new PushSubscriptionDto(subscription.Id, subscription.UserAgent, subscription.CreatedAt, subscription.LastSuccessAt);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await db.PushSubscriptions.Where(subscription => subscription.Id == id && subscription.UserId == currentUser.Id)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted == 0 ? AppError.NotFound() : Result.Success();
    }

    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/push/public-key", (PushSubscriptionsHandler handler) => TypedResults.Ok(handler.PublicKey()))
            .WithName("GetPushPublicKey")
            .WithSummary("VAPID public key for PushManager.subscribe")
            .Produces<PushPublicKeyDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/me/push-subscriptions", async (PushSubscriptionsHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.ListAsync(cancellationToken)))
            .WithName("ListPushSubscriptions")
            .WithSummary("The caller's devices that receive reminders")
            .Produces<IReadOnlyList<PushSubscriptionDto>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/me/push-subscriptions", async (SubscribePushRequest request, PushSubscriptionsHandler handler, CancellationToken cancellationToken) =>
                TypedResults.Ok(await handler.SubscribeAsync(request, cancellationToken)))
            .WithValidation<SubscribePushRequest>()
            .WithName("SubscribePush")
            .WithSummary("Save this browser's push subscription")
            .Produces<PushSubscriptionDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapDelete("/me/push-subscriptions/{id:guid}", async (Guid id, PushSubscriptionsHandler handler, CancellationToken cancellationToken) =>
                (await handler.DeleteAsync(id, cancellationToken)).ToHttpResult())
            .WithName("DeletePushSubscription")
            .WithSummary("Stop reminders on one device")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
