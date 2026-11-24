using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.ServiceLevels.Models;

/// <summary>
/// A request to start the timer of one metric for a record.
/// </summary>
public sealed class ServiceLevelStart
{
    /// <summary>
    /// Gets or sets the record the timer measures.
    /// </summary>
    public RecordRef Subject { get; set; }

    /// <summary>
    /// Gets or sets the metric.
    /// </summary>
    public string Metric { get; set; }

    /// <summary>
    /// Gets or sets the policy to read the target from.
    /// </summary>
    public string PolicyId { get; set; }

    /// <summary>
    /// Gets or sets the record's priority key, as the policy's scope keys its priorities.
    /// </summary>
    public string Priority { get; set; }

    /// <summary>
    /// Gets or sets when the timer starts, in UTC, or <see langword="null"/> for now.
    /// </summary>
    public DateTime? StartUtc { get; set; }

    /// <summary>
    /// Gets or sets whether the timer starts paused, for a record created in a state that doesn't count.
    /// </summary>
    public bool Paused { get; set; }

    /// <summary>
    /// Gets or sets whether the warning was already raised, for a timer copied from another system, so the monitor
    /// doesn't raise it again.
    /// </summary>
    public bool AlreadyWarned { get; set; }

    /// <summary>
    /// Gets or sets whether the breach was already raised, for a timer copied from another system, so the monitor
    /// doesn't raise it again.
    /// </summary>
    public bool AlreadyBreached { get; set; }

    /// <summary>
    /// Gets or sets the due time the timer already had, in UTC, for a timer copied from another system: it wins over the
    /// policy's target, which becomes the open time between the start and this due time.
    /// </summary>
    public DateTime? DueUtc { get; set; }

    /// <summary>
    /// Gets or sets since when a timer that starts paused has been paused, in UTC: the time before it counts as used.
    /// Without it, a paused timer counts nothing.
    /// </summary>
    public DateTime? PausedSinceUtc { get; set; }

    /// <summary>
    /// Gets or sets when the metric was already met, in UTC, for a timer copied from another system: the timer starts
    /// completed then, and nothing is announced.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }
}
