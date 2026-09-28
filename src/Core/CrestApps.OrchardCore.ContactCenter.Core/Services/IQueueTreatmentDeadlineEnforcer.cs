namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Holds each queue's next treatment deadline — the welcome a caller who has just arrived is due, the callback offer,
/// the next periodic announcement — and plays what is due when it falls due, instead of leaving it to a sweep.
/// </summary>
public interface IQueueTreatmentDeadlineEnforcer
{
    /// <summary>
    /// Arms the queue's next treatment deadline, or drops it when nobody waiting in it is due anything.
    /// </summary>
    /// <param name="queueId">The queue.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ArmAsync(string queueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Plays whatever the queue's waiting callers are due now, commits it, and arms the queue's next deadline.
    /// </summary>
    /// <param name="queueId">The queue.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task RunAndArmAsync(string queueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Plays whatever the queue's waiting callers are due now and commits it.
    /// </summary>
    /// <param name="queueId">The queue.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>When something is next due, or <see langword="null"/> when nothing more is.</returns>
    Task<DateTime?> RunDueAsync(string queueId, CancellationToken cancellationToken = default);
}
