namespace CrestApps.Core.Hosting.Background;

/// <summary>
/// How often a cycle runs, and how long it may hold the lock that stops two nodes running it at once.
/// </summary>
/// <remarks>
/// Bound per cycle by name, so one cycle's schedule can be changed without touching the others.
/// </remarks>
public sealed class BackgroundCycleOptions
{
    /// <summary>
    /// Gets or sets whether the cycle runs at all.
    /// </summary>
    /// <remarks>
    /// An operator's off switch for a sweep that is misbehaving, without taking the feature down.
    /// </remarks>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets how long to wait between passes.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets how long to wait for the lock before giving up on this pass.
    /// </summary>
    /// <remarks>
    /// Short, because another node holding it means the pass is already being done. Waiting would only
    /// queue up a second run of work that is no longer due.
    /// </remarks>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets how long the lock survives if the holder never releases it.
    /// </summary>
    /// <remarks>
    /// Longer than a pass is expected to take, so a slow run is not overlapped by the next tick, and
    /// short enough that a node that died mid-pass does not block the sweep indefinitely.
    /// </remarks>
    public TimeSpan LockExpiration { get; set; } = TimeSpan.FromMinutes(2);
}
