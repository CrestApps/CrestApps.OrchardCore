using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A change to a published schedule, kept for the record and for predictable-scheduling rules.
/// </summary>
public sealed class ScheduleChange : CatalogItem
{
    /// <summary>
    /// Gets or sets the period.
    /// </summary>
    public string PeriodId { get; set; }

    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the worker whose shift changed.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the shift.
    /// </summary>
    public string ShiftKey { get; set; }

    /// <summary>
    /// Gets or sets what changed.
    /// </summary>
    public ScheduleChangeKind Kind { get; set; }

    /// <summary>
    /// Gets or sets when the shift started before the change.
    /// </summary>
    public DateTime? BeforeStartUtc { get; set; }

    /// <summary>
    /// Gets or sets when the shift ended before the change.
    /// </summary>
    public DateTime? BeforeEndUtc { get; set; }

    /// <summary>
    /// Gets or sets when the shift starts after the change.
    /// </summary>
    public DateTime? AfterStartUtc { get; set; }

    /// <summary>
    /// Gets or sets when the shift ends after the change.
    /// </summary>
    public DateTime? AfterEndUtc { get; set; }

    /// <summary>
    /// Gets or sets who worked the shift before a reassignment.
    /// </summary>
    public string PreviousWorkerId { get; set; }

    /// <summary>
    /// Gets or sets who asked for it.
    /// </summary>
    public ScheduleChangeInitiator InitiatedBy { get; set; } = ScheduleChangeInitiator.Employer;

    /// <summary>
    /// Gets or sets why.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets what made the change: <c>Publish</c> for a republished schedule, or empty for a change made to
    /// one shift, such as a trade or a claim.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the publish that made the change.
    /// </summary>
    public int PublishedVersion { get; set; }

    /// <summary>
    /// Gets or sets who made it.
    /// </summary>
    public string ChangedBy { get; set; }

    /// <summary>
    /// Gets or sets when it was made.
    /// </summary>
    public DateTime ChangedUtc { get; set; }
}
