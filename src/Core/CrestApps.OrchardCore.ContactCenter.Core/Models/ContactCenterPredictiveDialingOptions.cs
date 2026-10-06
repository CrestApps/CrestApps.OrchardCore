namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the tenant-level timings and limits of predictive dialing. It is bound from the
/// <c>CrestApps:ContactCenter:PredictiveDialing</c> configuration section and validated on start.
/// </summary>
/// <remarks>
/// The per-campaign pacing choices (target abandonment, lines per agent, sample floors) live on the dialer profile. These
/// are the engine's own timings, which an operator tunes for the platform rather than for a campaign.
/// </remarks>
public sealed class ContactCenterPredictiveDialingOptions
{
    /// <summary>
    /// The configuration section the options are bound from.
    /// </summary>
    public const string SectionName = "CrestApps:ContactCenter:PredictiveDialing";

    /// <summary>
    /// Gets or sets how often an over-dialing campaign is paced while it has calls to place and agents to take them.
    /// </summary>
    public TimeSpan PacingInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets or sets how long a pacing request waits for others before it runs, so a burst of agent and call changes paces
    /// a campaign once. Must be shorter than <see cref="PacingInterval"/>.
    /// </summary>
    public TimeSpan PacingDebounce { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Gets or sets how long one node may hold a campaign's pacing before another may take it, should the first stop
    /// without releasing it. Must be longer than <see cref="PacingInterval"/>.
    /// </summary>
    public TimeSpan PacingLockExpiration { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets the most calls one pacing cycle may place for a campaign, whatever the calculation allows, so a
    /// sudden swing in the measurements is followed in steps.
    /// </summary>
    public int MaxDialsPerCycle { get; set; } = 25;

    /// <summary>
    /// Gets or sets how long connecting an answered call waits for an agent's lock before it tries the next agent.
    /// Must be shorter than the two seconds after which the call counts as abandoned.
    /// </summary>
    public TimeSpan ConnectLockWait { get; set; } = TimeSpan.FromMilliseconds(150);

    /// <summary>
    /// Gets or sets the number of days the long-run abandonment rate is measured over: over-dialing stops for a campaign
    /// whose rate over this period has reached its profile's cap. The common abandoned-call rules measure over thirty days.
    /// </summary>
    public int ComplianceWindowDays { get; set; } = 30;

    /// <summary>
    /// Gets or sets how long a call is assumed to take from being placed to being answered until enough calls have been
    /// measured. It is the horizon within which an agent finishing their current call can take a new one.
    /// </summary>
    public TimeSpan DefaultRingHorizon { get; set; } = TimeSpan.FromSeconds(12);

    /// <summary>
    /// Gets or sets how long after the answer a call no agent was connected to and no message was played is swept up and
    /// given the abandoned-call message, should every other path have missed it.
    /// </summary>
    public TimeSpan AnsweredUnconnectedSweepAfter { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets how long a profile's measured pacing statistics are reused before they are read again.
    /// </summary>
    public TimeSpan StatisticsCacheDuration { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the most recent calls the per-call timings (ring time, connect time, talk and wrap-up time) and the
    /// answer rate are measured from, so measuring a busy campaign stays cheap.
    /// </summary>
    public int MaxTimingSamples { get; set; } = 2000;
}
