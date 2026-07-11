using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

internal static class ActivityPurgeHelper
{
    internal static void Purge(
        OmnichannelActivity activity,
        DateTime purgedAtUtc,
        string purgedById,
        string purgedByUsername)
    {
        ArgumentNullException.ThrowIfNull(activity);

        activity.Status = ActivityStatus.Purged;
        activity.PurgedAtUtc = purgedAtUtc;
        activity.PurgedById = purgedById;
        activity.PurgedByUsername = purgedByUsername;
        activity.ReservationId = null;
        activity.ReservedById = null;
        activity.ReservedByUsername = null;
        activity.ReservedUtc = null;
        activity.ReservationExpiresUtc = null;
    }
}
