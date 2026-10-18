using Microsoft.EntityFrameworkCore;
using Nomax.BuildingBlocks.Messaging;
using Nomax.Modules.Classroom.Domain;
using Nomax.Modules.Classroom.Infrastructure;
using Nomax.Modules.Identity.Contracts;

namespace Nomax.Modules.Classroom.IntegrationEvents;

/// <summary>BR-AKUN-05: a deactivated account gets no more notifications; its pending ones are cancelled.</summary>
internal sealed class UserDeactivatedConsumer(ClassroomDbContext db, TimeProvider timeProvider)
    : InboxConsumer<ClassroomDbContext, UserDeactivated>(db, timeProvider)
{
    protected override async Task ApplyAsync(IntegrationEventEnvelope<UserDeactivated> envelope, CancellationToken cancellationToken)
    {
        var pending = await Db.Notifications
            .Where(notification => notification.UserId == envelope.Payload.UserId && notification.Status == NotificationStatus.Pending)
            .ToListAsync(cancellationToken);
        pending.ForEach(notification => notification.Cancel("Account deactivated."));
    }
}
