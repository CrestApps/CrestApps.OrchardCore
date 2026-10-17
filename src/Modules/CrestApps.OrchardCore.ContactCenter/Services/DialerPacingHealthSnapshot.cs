namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// The dialer's state across every paced campaign queue with waiting work, at one moment.
/// </summary>
public sealed class DialerPacingHealthSnapshot
{
    /// <summary>
    /// Gets or sets when the state was read.
    /// </summary>
    public DateTime EvaluatedUtc { get; set; }

    /// <summary>
    /// Gets or sets how long work may wait without a completed pacing cycle before the queue counts as stalled.
    /// </summary>
    public TimeSpan StalledAfter { get; set; }

    /// <summary>
    /// Gets or sets the state of each paced campaign queue with waiting work, the most serious first.
    /// </summary>
    public IList<DialerPacingQueueHealth> Queues { get; set; } = [];

    /// <summary>
    /// Gets the most serious state of any queue, or <see cref="DialerPacingHealthState.NothingToCall"/> when no paced
    /// campaign has work waiting.
    /// </summary>
    public DialerPacingHealthState State
        => Queues.Count == 0
            ? DialerPacingHealthState.NothingToCall
            : Queues.MaxBy(queue => DialerPacingHealthEvaluator.Severity(queue.State)).State;
}
