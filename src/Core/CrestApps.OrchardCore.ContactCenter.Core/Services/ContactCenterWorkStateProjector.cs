using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Copies routing-owned work state onto the CRM activity that projects it. The copy lives here rather than
/// inside the projection service so that every caller that has to reconcile the read model — the projection,
/// the backfill, and tests — uses one definition of what the read model contains.
/// </summary>
public static class ContactCenterWorkStateProjector
{
    /// <summary>
    /// Copies every projected field from the work state onto the activity.
    /// </summary>
    /// <param name="activity">The CRM activity that carries the read model.</param>
    /// <param name="workState">The routing-owned work state that is authoritative.</param>
    public static void Apply(OmnichannelActivity activity, ContactCenterWorkState workState)
    {
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(workState);

        activity.AssignmentStatus = workState.AssignmentStatus;
        activity.ReservationId = workState.ReservationId;
        activity.ReservedById = workState.ReservedById;
        activity.ReservedByUsername = workState.ReservedByUsername;
        activity.ReservedUtc = workState.ReservedUtc;
        activity.ReservationExpiresUtc = workState.ReservationExpiresUtc;
        activity.AssignedToId = workState.AssignedToId;
        activity.AssignedToUsername = workState.AssignedToUsername;
        activity.AssignedToUtc = workState.AssignedToUtc;
        activity.Attempts = workState.Attempts;
    }

    /// <summary>
    /// Copies the projected fields an activity already carries back onto a work state. This is how routing
    /// adopts work that existed before the work state document did, so an upgraded installation does not need
    /// a separate backfill pass to keep in-flight work routable.
    /// </summary>
    /// <param name="workState">The work state to seed.</param>
    /// <param name="activity">The CRM activity whose projected fields describe the pre-existing routing state.</param>
    public static void SeedFromActivity(ContactCenterWorkState workState, OmnichannelActivity activity)
    {
        ArgumentNullException.ThrowIfNull(workState);
        ArgumentNullException.ThrowIfNull(activity);

        workState.AssignmentStatus = activity.AssignmentStatus;
        workState.ReservationId = activity.ReservationId;
        workState.ReservedById = activity.ReservedById;
        workState.ReservedByUsername = activity.ReservedByUsername;
        workState.ReservedUtc = activity.ReservedUtc;
        workState.ReservationExpiresUtc = activity.ReservationExpiresUtc;
        workState.AssignedToId = activity.AssignedToId;
        workState.AssignedToUsername = activity.AssignedToUsername;
        workState.AssignedToUtc = activity.AssignedToUtc;
        workState.Attempts = activity.Attempts;
    }

    /// <summary>
    /// Determines whether the activity's read model differs from the authoritative work state.
    /// </summary>
    /// <param name="activity">The CRM activity that carries the read model.</param>
    /// <param name="workState">The routing-owned work state that is authoritative.</param>
    /// <returns><see langword="true"/> when at least one projected field differs; otherwise, <see langword="false"/>.</returns>
    public static bool HasDivergence(OmnichannelActivity activity, ContactCenterWorkState workState)
    {
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(workState);

        return activity.AssignmentStatus != workState.AssignmentStatus ||
            !string.Equals(activity.ReservationId, workState.ReservationId, StringComparison.Ordinal) ||
            !string.Equals(activity.ReservedById, workState.ReservedById, StringComparison.Ordinal) ||
            !string.Equals(activity.ReservedByUsername, workState.ReservedByUsername, StringComparison.Ordinal) ||
            activity.ReservedUtc != workState.ReservedUtc ||
            activity.ReservationExpiresUtc != workState.ReservationExpiresUtc ||
            !string.Equals(activity.AssignedToId, workState.AssignedToId, StringComparison.Ordinal) ||
            !string.Equals(activity.AssignedToUsername, workState.AssignedToUsername, StringComparison.Ordinal) ||
            activity.AssignedToUtc != workState.AssignedToUtc ||
            activity.Attempts != workState.Attempts;
    }
}
