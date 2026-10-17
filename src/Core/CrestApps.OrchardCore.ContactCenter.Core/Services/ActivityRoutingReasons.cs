namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The outcomes routing records on a decision.
/// </summary>
/// <remarks>
/// They are stored verbatim on every routing decision event and in the dialer decision log, where a report filters
/// and groups decisions by them, so changing one splits the history of that outcome in two.
/// </remarks>
internal static class ActivityRoutingReasons
{
    /// <summary>
    /// No agent was available to the queue at all.
    /// </summary>
    public const string NoAgentsAvailable = "No agents are currently available for this queue.";

    /// <summary>
    /// Agents were available, but the queue's routing policy turned every one of them away.
    /// </summary>
    public const string NoAgentMatchedPolicy = "No available agent matched the queue routing policy.";

    /// <summary>
    /// An agent was selected.
    /// </summary>
    public const string Selected = "Selected the highest-scoring eligible agent.";

    /// <summary>
    /// An agent was selected, but the agent or the queue item was taken before the reservation was written.
    /// </summary>
    public const string ReservationLost = "The selected agent or queue item was no longer available when the reservation was created.";

    /// <summary>
    /// Gets every outcome, in the order they are offered as a filter.
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        NoAgentsAvailable,
        NoAgentMatchedPolicy,
        ReservationLost,
        Selected,
    ];
}
