using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

internal static class ActivityPurgeHelper
{
    internal static void Purge(OmnichannelActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        activity.Status = ActivityStatus.Purged;
        activity.ReservationId = null;
        activity.ReservedById = null;
        activity.ReservedByUsername = null;
        activity.ReservedUtc = null;
        activity.ReservationExpiresUtc = null;
    }
}
