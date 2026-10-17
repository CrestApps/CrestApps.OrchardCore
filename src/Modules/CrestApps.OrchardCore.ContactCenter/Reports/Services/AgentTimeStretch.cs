using CrestApps.OrchardCore.ContactCenter.Reports.Models;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// The agents' state intervals over one stretch of the log, and what was read to compute them.
/// </summary>
/// <param name="Intervals">The intervals, clipped to the stretch.</param>
/// <param name="AgentsWithEvents">The agents with a state change in the stretch.</param>
/// <param name="EventCount">How many state changes the stretch holds.</param>
/// <param name="EventsRead">How many events were read, including each agent's state before the stretch.</param>
internal sealed record AgentTimeStretch(
    IReadOnlyList<AgentPresenceInterval> Intervals,
    IReadOnlySet<string> AgentsWithEvents,
    int EventCount,
    int EventsRead);
