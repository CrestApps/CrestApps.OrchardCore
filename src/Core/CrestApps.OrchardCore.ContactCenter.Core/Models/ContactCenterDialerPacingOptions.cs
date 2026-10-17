namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the tenant-level timings of event-driven pacing for the dialing modes that reserve an agent before each
/// call: Power, Progressive, and Predictive on the reserved pacing model. It is bound from the
/// <c>CrestApps:ContactCenter:DialerPacing</c> configuration section and validated on start.
/// </summary>
/// <remarks>
/// These change only when a campaign is paced, never how many calls a cycle places: that stays with the dialer profile.
/// </remarks>
public sealed class ContactCenterDialerPacingOptions
{
    /// <summary>
    /// The configuration section the options are bound from.
    /// </summary>
    public const string SectionName = "CrestApps:ContactCenter:DialerPacing";

    /// <summary>
    /// Gets or sets how long a pacing request waits for others before it runs, so a burst of agent and call changes
    /// paces a campaign once.
    /// </summary>
    public TimeSpan Debounce { get; set; } = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Gets or sets how long one node may hold a campaign's pacing before another may take it, should the first stop
    /// without releasing it.
    /// </summary>
    public TimeSpan LockExpiration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets how long a request that found the campaign's pacing lock held waits before it tries again. Must be
    /// shorter than <see cref="LockExpiration"/>.
    /// </summary>
    public TimeSpan LockRetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets how long one pacing cycle may run before it is abandoned. An in-process lock never expires on its own,
    /// so a cycle that waits on something that never answers would otherwise hold the campaign's pacing lock, and stop the
    /// campaign dialing, until the application restarts.
    /// </summary>
    public TimeSpan CycleTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets how long work may wait in a paced campaign queue without a pacing cycle of that queue completing
    /// before the dialer is reported as stalled, on the dialer activity report, the live dashboard and the dialer pacing
    /// health check. The minute dialer pacing task paces every queue with waiting work at least once a minute, so a
    /// healthy dialer completes a cycle well within the default of five minutes.
    /// </summary>
    public TimeSpan StalledAfter { get; set; } = TimeSpan.FromMinutes(5);
}
