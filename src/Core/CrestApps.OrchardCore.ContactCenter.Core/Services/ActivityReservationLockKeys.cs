namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The distributed lock keys routing serializes an activity, an agent and a reservation on. Everything that claims one of
/// them takes the same key, so a reservation, an over-dialed call and the claim of an agent at answer cannot interleave.
/// </summary>
internal static class ActivityReservationLockKeys
{
    /// <summary>
    /// The lock held while an activity is claimed, by a reservation or by a call placed without one.
    /// </summary>
    /// <param name="activityItemId">The activity.</param>
    public static string ForActivity(string activityItemId)
        => $"ContactCenterActivityReservation:{activityItemId}";

    /// <summary>
    /// The lock held while an agent is given work.
    /// </summary>
    /// <param name="agentId">The agent.</param>
    public static string ForAgent(string agentId)
        => $"ContactCenterAgentReservation:{agentId}";

    /// <summary>
    /// The lock held while a reservation is settled.
    /// </summary>
    /// <param name="reservationId">The reservation.</param>
    public static string ForReservation(string reservationId)
        => $"ContactCenterReservation:{reservationId}";
}
