namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Paces over-dialing campaign queues as things happen -- an agent becomes free, a call is answered or ends -- rather than
/// only once a minute.
/// </summary>
/// <remarks>
/// A tenant singleton over <see cref="IContactCenterDeadlineScheduler"/>. A request is debounced per queue and runs one
/// pacing cycle on a scope of its own; while the cycle places calls the queue is paced again after the pacing interval.
/// It is an accelerator only: the minute dialer pacing task requests every over-dialing queue again, and the pacing lock
/// and the concurrency-checked pacing record keep a cycle correct however many are requested.
/// </remarks>
public interface IPredictivePacingScheduler
{
    /// <summary>
    /// Asks for the campaign queue to be paced shortly. Requests that arrive while one is already pending are merged.
    /// </summary>
    /// <param name="queueId">The campaign queue.</param>
    void Request(string queueId);

    /// <summary>
    /// Asks for the campaign queue to be paced again after the pacing lock retry delay, because a cycle found its pacing
    /// lock held by another. A request already pending is kept; the retry merges into it.
    /// </summary>
    /// <param name="queueId">The campaign queue.</param>
    void RequestRetry(string queueId);
}
