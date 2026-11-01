using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// A change a publish would make to a published shift.
/// </summary>
public sealed class SchedulePublishChange
{
    /// <summary>
    /// Gets or sets what changes.
    /// </summary>
    public ScheduleChangeKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the shift.
    /// </summary>
    public string ShiftKey { get; set; }

    /// <summary>
    /// Gets or sets the worker after the change, or before it for a removed shift.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the worker before, for a reassigned shift.
    /// </summary>
    public string PreviousWorkerId { get; set; }

    /// <summary>
    /// Gets or sets when it started before.
    /// </summary>
    public DateTime? BeforeStartUtc { get; set; }

    /// <summary>
    /// Gets or sets when it starts after.
    /// </summary>
    public DateTime? AfterStartUtc { get; set; }
}
