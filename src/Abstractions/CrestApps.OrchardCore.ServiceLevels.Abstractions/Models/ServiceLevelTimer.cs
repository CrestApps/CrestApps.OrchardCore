using System.Text.Json.Serialization;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.ServiceLevels.Models;

/// <summary>
/// The service level timer of one record and one metric: when it started, when it is due on the policy's hours, and
/// what happened to it. Reports read these instead of deriving service levels from a record's current state.
/// </summary>
public sealed class ServiceLevelTimer : CatalogItem
{
    /// <summary>
    /// Gets or sets the record the timer measures.
    /// </summary>
    public RecordRef Subject { get; set; }

    /// <summary>
    /// Gets or sets the metric, one of <see cref="ServiceLevelMetrics"/> or a metric a module adds.
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
    /// Gets or sets the time allowed, in seconds of the timer's hours.
    /// </summary>
    public long TargetSeconds { get; set; }

    /// <summary>
    /// Gets or sets the Business Hours schedule the timer counts on, copied from the policy when it started. Empty
    /// counts around the clock.
    /// </summary>
    public string HoursScheduleId { get; set; }

    /// <summary>
    /// Gets or sets the share of the target, from 1 to 99, after which the timer warns.
    /// </summary>
    public int WarningPercent { get; set; } = ServiceLevelsConstants.DefaultWarningPercent;

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public ServiceLevelTimerState State { get; set; }

    /// <summary>
    /// Gets or sets when the timer started, in UTC.
    /// </summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the target runs out, in UTC. While the timer is paused it is the due time it had when it was
    /// paused; resuming moves it.
    /// </summary>
    public DateTime DueUtc { get; set; }

    /// <summary>
    /// Gets or sets when the timer reaches its warning point, in UTC.
    /// </summary>
    public DateTime WarnAtUtc { get; set; }

    /// <summary>
    /// Gets or sets since when the timer is paused, in UTC, or <see langword="null"/> while it isn't.
    /// </summary>
    public DateTime? PausedSinceUtc { get; set; }

    /// <summary>
    /// Gets or sets the time used so far, in seconds of the timer's hours, as of the last pause, resume or change of
    /// target. It can exceed <see cref="TargetSeconds"/> once the timer is breached.
    /// </summary>
    public long UsedSeconds { get; set; }

    /// <summary>
    /// Gets or sets from when the timer has been counting since <see cref="UsedSeconds"/> was last settled, in UTC.
    /// </summary>
    public DateTime CountingSinceUtc { get; set; }

    /// <summary>
    /// Gets or sets when the warning was raised, in UTC, or <see langword="null"/>.
    /// </summary>
    public DateTime? WarnedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the timer passed its target, in UTC, or <see langword="null"/> while it is within it.
    /// </summary>
    public DateTime? BreachedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the work was done or the timer was cancelled, in UTC.
    /// </summary>
    public DateTime? EndedUtc { get; set; }

    /// <summary>
    /// Gets or sets the time the work took, in seconds of the timer's hours, once it is completed.
    /// </summary>
    public long? ElapsedSeconds { get; set; }

    /// <summary>
    /// Gets or sets whether the timer's business hours schedule is never open (it was changed after the timer started),
    /// so the timer counts around the clock until the schedule opens again.
    /// </summary>
    public bool NoBusinessHours { get; set; }

    /// <summary>
    /// Gets whether the timer still measures something: it is running or paused.
    /// </summary>
    [JsonIgnore]
    public bool IsActive => State is ServiceLevelTimerState.Running or ServiceLevelTimerState.Paused;

    /// <summary>
    /// Gets whether the timer passed its target.
    /// </summary>
    [JsonIgnore]
    public bool IsBreached => BreachedUtc is not null;
}
