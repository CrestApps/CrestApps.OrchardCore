namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// The tunables of the automated-activity processing pass, bound from <c>CrestApps:Omnichannel:Automation</c>.
/// They are deployment characteristics rather than product constants: how much work one pass may take on, and
/// how long it may hold its lease, depend on how fast the node is and how much it is carrying.
/// </summary>
public sealed class OmnichannelAutomationOptions
{
    /// <summary>
    /// Gets or sets how long, in milliseconds, one pass holds its distributed lease. A pass stops well before
    /// this so it never runs past a lease a peer would then take.
    /// </summary>
    public int ProcessorLeaseMilliseconds { get; set; } = 600_000;

    /// <summary>
    /// Gets or sets how many activities one query materializes.
    /// </summary>
    public int ProcessorBatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the ceiling on how many activities one pass processes, so a large backlog is drained over
    /// several passes rather than in one that overruns its lease.
    /// </summary>
    public int MaxActivitiesPerInvocation { get; set; } = 1_000;

    /// <summary>
    /// Gets or sets how many times an activity is attempted before it is treated as failed.
    /// </summary>
    public int MaxProcessingAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets the base delay, in minutes, before a failed activity is retried. The delay grows with the
    /// number of attempts already made.
    /// </summary>
    public int RetryDelayMinutes { get; set; } = 5;
}
