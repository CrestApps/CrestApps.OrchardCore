using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// The dialer's state for one paced campaign queue with waiting work.
/// </summary>
public sealed class DialerPacingQueueHealth
{
    /// <summary>
    /// Gets or sets the campaign queue.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the campaign whose queue it is.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets the dialer profile the record at the head of the queue was loaded under.
    /// </summary>
    public string DialerProfileId { get; set; }

    /// <summary>
    /// Gets or sets what the queue's dialer is doing.
    /// </summary>
    public DialerPacingHealthState State { get; set; }

    /// <summary>
    /// Gets or sets how many records wait in the queue.
    /// </summary>
    public int WaitingCount { get; set; }

    /// <summary>
    /// Gets or sets when the record that has waited longest was queued.
    /// </summary>
    public DateTime WaitingSinceUtc { get; set; }

    /// <summary>
    /// Gets or sets the queue's latest pacing cycle record, however it ended.
    /// </summary>
    public DialerPacingCycle LastCycle { get; set; }

    /// <summary>
    /// Gets or sets the queue's latest pacing cycle record that ran to the end and decided something.
    /// </summary>
    public DialerPacingCycle LastCompletedCycle { get; set; }

    /// <summary>
    /// Gets or sets how long work has waited without a pacing cycle of the queue completing: from the later of the last
    /// completed cycle and the moment the longest-waiting record was queued.
    /// </summary>
    public TimeSpan TimeWithoutCompletedCycle { get; set; }

    /// <summary>
    /// Gets or sets, when every pacing cycle of the queue since then could not take its pacing lock, the last time a cycle
    /// did or, when no record says, when the first cycle that could not did; otherwise <see langword="null"/>.
    /// </summary>
    public DateTime? LockNotAcquiredSinceUtc { get; set; }
}
