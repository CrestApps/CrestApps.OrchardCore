using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Reads the dialer decision log, the routing decisions and dial suppressions recorded as events, through their
/// indexed columns, and folds a repeated routing failure into the event that first recorded it.
/// </summary>
public interface IDialerDecisionLog
{
    /// <summary>
    /// Folds a failed routing decision into the latest decision of its queue when it repeats it exactly: the same
    /// record, reason and candidates, made soon enough after the last one. The latest event's count goes up by one and
    /// its last-seen time moves to <paramref name="decidedUtc"/>; no new event and no outbox message are written.
    /// </summary>
    /// <param name="decision">The decision routing has just made.</param>
    /// <param name="decidedUtc">The UTC time the decision was made.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// <see langword="true"/> when the decision was folded into an existing event; <see langword="false"/> when it must
    /// be published as a new event.
    /// </returns>
    Task<bool> TryCoalesceRoutingDecisionAsync(
        ActivityRoutingDecisionEventData decision,
        DateTime decidedUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the routing decisions of the supplied queues last made at or after an instant, oldest last-seen first.
    /// A coalesced decision first made long before the instant is included while it is still being repeated.
    /// </summary>
    /// <param name="queueIds">The queues to read.</param>
    /// <param name="sinceUtc">The inclusive UTC lower bound on the time the decision was last made.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching routing decision events.</returns>
    Task<IReadOnlyList<InteractionEvent>> GetRoutingDecisionsSeenSinceAsync(
        IReadOnlyCollection<string> queueIds,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the decision events that match a filter, the most recently made first.
    /// </summary>
    /// <param name="query">The filter.</param>
    /// <param name="maxCount">The most events to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching events, newest last-seen first.</returns>
    Task<IReadOnlyList<InteractionEvent>> ListAsync(
        DialerDecisionLogQuery query,
        int maxCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the indexed columns of every decision that matches a filter, without reading the events themselves, so
    /// decisions can be counted and grouped over a period.
    /// </summary>
    /// <param name="query">The filter.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>One entry per matching decision event.</returns>
    Task<IReadOnlyList<DialerDecisionIndex>> GetEntriesAsync(
        DialerDecisionLogQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the last time routing made a decision, of any outcome, for each of the supplied queues.
    /// </summary>
    /// <param name="queueIds">The queues to look up.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The UTC time of each queue's last decision. A queue routing never decided for is left out.</returns>
    Task<IReadOnlyDictionary<string, DateTime>> GetLastRoutingDecisionTimesAsync(
        IReadOnlyCollection<string> queueIds,
        CancellationToken cancellationToken = default);
}
