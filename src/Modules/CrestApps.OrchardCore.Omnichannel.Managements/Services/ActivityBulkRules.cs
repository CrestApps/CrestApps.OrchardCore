using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// The rules of the Manage Activities bulk actions for which activities an action may touch, shared by the page and the
/// AI tools.
/// </summary>
internal static class ActivityBulkRules
{
    /// <summary>
    /// Whether a bulk action may change the activity: it has not been worked to an end, or it failed or was cancelled.
    /// </summary>
    public static bool IsBulkManageable(OmnichannelActivity activity)
    {
        return activity is not null &&
            activity.Status is ActivityStatus.NotStated or ActivityStatus.Scheduled or ActivityStatus.Pending or ActivityStatus.AwaitingAgentResponse or ActivityStatus.Failed or ActivityStatus.Cancelled;
    }

    /// <summary>
    /// Removes the activities the dialer is dialing, ringing an agent with, or an agent is on a call for. That work
    /// belongs to the call until it ends: purging, reassigning or requeueing it would pull the record out from under a
    /// live customer.
    /// </summary>
    /// <returns>How many activities were removed.</returns>
    public static async Task<int> RemoveDialerWorkInFlightAsync(
        IEnumerable<IActivityDialerContributor> dialerContributors,
        List<OmnichannelActivity> activities,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(activities);

        var dialerContributor = dialerContributors?.FirstOrDefault();

        if (dialerContributor is null || activities.Count == 0)
        {
            return 0;
        }

        // The dialer's own queue says what it is working; the activity's copy of its assignment can lag behind, and
        // trusting it could keep a record stuck in a stale state out of reach of the very action that clears it.
        var inFlight = await dialerContributor.GetActivitiesInFlightAsync(
            activities.Select(activity => activity.ItemId),
            cancellationToken);

        return inFlight.Count == 0
            ? 0
            : activities.RemoveAll(activity => inFlight.Contains(activity.ItemId));
    }
}
