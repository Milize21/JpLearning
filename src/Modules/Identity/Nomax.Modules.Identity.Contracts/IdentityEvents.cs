using Nomax.SharedKernel.Events;

namespace Nomax.Modules.Identity.Contracts;

// Integration events published by Identity (docs/05 §8).

/// <param name="Role">One of <c>Nomax.SharedKernel.Roles</c>.</param>
/// <param name="CreatedBy">Admin who created the account.</param>
public sealed record UserAccountCreated(Guid UserId, string Role, string DisplayName, Guid CreatedBy) : IIntegrationEvent
{
    public static string EventType => "UserAccountCreated.v1";
}

public sealed record UserDeactivated(Guid UserId) : IIntegrationEvent
{
    public static string EventType => "UserDeactivated.v1";
}

public sealed record UserReactivated(Guid UserId) : IIntegrationEvent
{
    public static string EventType => "UserReactivated.v1";
}

public sealed record UserDisplayNameChanged(Guid UserId, string DisplayName) : IIntegrationEvent
{
    public static string EventType => "UserDisplayNameChanged.v1";
}
