using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Routes the follow-up a subject action creates to an agent or to the dialer.
/// </summary>
internal static class FollowUpRoutes
{
    /// <summary>
    /// Whether an activity an agent works may carry the source: not the AI's, and not a dialer's, which a dialer
    /// profile sets.
    /// </summary>
    internal static bool IsAgentSource(string source)
        => !string.IsNullOrWhiteSpace(source) &&
            !ActivitySources.IsDialer(source) &&
            !string.Equals(source, ActivitySources.Automatic, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Moves an activity to an agent: off the dialer, with a source the dialer does not drive, owned by the named user
    /// or by the owner it already has.
    /// </summary>
    internal static void ApplyAgentRoute(OmnichannelActivity activity, string source, string campaignId, string ownerId, string ownerName, DateTime utcNow)
    {
        activity.Source = source;
        activity.CampaignId = campaignId;
        activity.DialerProfileId = null;
        activity.InteractionType = ActivityInteractionType.Manual;
        activity.AISessionId = null;
        ClearReservation(activity);

        if (!string.IsNullOrEmpty(ownerId))
        {
            activity.AssignedToId = ownerId;
            activity.AssignedToUsername = ownerName;
            activity.AssignedToUtc = utcNow;
            activity.AssignmentStatus = ActivityAssignmentStatus.Assigned;
        }
        else if (string.IsNullOrEmpty(activity.AssignedToId))
        {
            activity.AssignmentStatus = ActivityAssignmentStatus.Available;
        }

        ApplyInitialStatus(activity);
    }

    /// <summary>
    /// Moves an activity to the dialer: a phone call on the campaign, under the profile, offered by the dialer to
    /// whichever agent signed in to the campaign is free, so it has no owner of its own.
    /// </summary>
    internal static void ApplyDialerRoute(OmnichannelActivity activity, ActivityDialerProfileDescriptor profile, string campaignId, string destination)
    {
        activity.CampaignId = campaignId;
        activity.Source = profile.ActivitySource;
        activity.DialerProfileId = profile.ProfileId;
        activity.InteractionType = ActivityInteractionType.Manual;
        activity.Channel = OmnichannelConstants.Channels.Phone;
        activity.Kind = ActivityKind.Call;
        activity.PreferredDestination = destination;
        activity.AISessionId = null;
        activity.AssignedToId = null;
        activity.AssignedToUsername = null;
        activity.AssignedToUtc = null;
        activity.AssignmentStatus = ActivityAssignmentStatus.Available;
        ClearReservation(activity);
        ApplyInitialStatus(activity);
    }

    private static void ApplyInitialStatus(OmnichannelActivity activity)
    {
        activity.Status = OmnichannelAutomationHelper.GetInitialActivityStatus(activity.InteractionType, !string.IsNullOrEmpty(activity.AssignedToId));

        // Re-arming an activity to a due status starts a fresh automated-processing budget.
        activity.ProcessingAttempts = 0;
    }

    private static void ClearReservation(OmnichannelActivity activity)
    {
        activity.ReservationId = null;
        activity.ReservedById = null;
        activity.ReservedByUsername = null;
        activity.ReservedUtc = null;
        activity.ReservationExpiresUtc = null;
    }
}
