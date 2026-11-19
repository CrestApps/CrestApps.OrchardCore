using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.ServiceLevels.Models;

namespace CrestApps.OrchardCore.ServiceLevels.Core.Indexes;

/// <summary>
/// The index row of a service level timer: what the monitor scans for due timers, and what reports read.
/// </summary>
public sealed class ServiceLevelTimerIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the kind of the record the timer measures.
    /// </summary>
    public string SubjectKind { get; set; }

    /// <summary>
    /// Gets or sets the id of the record the timer measures.
    /// </summary>
    public string SubjectId { get; set; }

    /// <summary>
    /// Gets or sets the metric.
    /// </summary>
    public string Metric { get; set; }

    /// <summary>
    /// Gets or sets the policy the target came from.
    /// </summary>
    public string PolicyId { get; set; }

    /// <summary>
    /// Gets or sets the priority key the target was read for.
    /// </summary>
    public string Priority { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public ServiceLevelTimerState State { get; set; }

    /// <summary>
    /// Gets or sets whether the timer is running or paused.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets whether the warning was raised.
    /// </summary>
    public bool Warned { get; set; }

    /// <summary>
    /// Gets or sets whether the timer passed its target.
    /// </summary>
    public bool Breached { get; set; }

    /// <summary>
    /// Gets or sets when the timer started, in UTC.
    /// </summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the timer warns, in UTC.
    /// </summary>
    public DateTime WarnAtUtc { get; set; }

    /// <summary>
    /// Gets or sets when the target runs out, in UTC.
    /// </summary>
    public DateTime DueUtc { get; set; }

    /// <summary>
    /// Gets or sets when the timer ended, in UTC.
    /// </summary>
    public DateTime? EndedUtc { get; set; }

    /// <summary>
    /// Gets or sets the time allowed, in seconds of the timer's hours.
    /// </summary>
    public long TargetSeconds { get; set; }

    /// <summary>
    /// Gets or sets the time the work took, in seconds of the timer's hours, once completed.
    /// </summary>
    public long? ElapsedSeconds { get; set; }
}
