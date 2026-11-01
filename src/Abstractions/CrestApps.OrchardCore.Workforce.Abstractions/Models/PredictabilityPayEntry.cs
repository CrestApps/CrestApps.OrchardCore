using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// Premium a worker is owed because their published schedule changed inside the notice window.
/// </summary>
public sealed class PredictabilityPayEntry : CatalogItem
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the schedule.
    /// </summary>
    public string PeriodId { get; set; }

    /// <summary>
    /// Gets or sets the change that caused it.
    /// </summary>
    public string ScheduleChangeId { get; set; }

    /// <summary>
    /// Gets or sets the shift.
    /// </summary>
    public string ShiftKey { get; set; }

    /// <summary>
    /// Gets or sets what changed.
    /// </summary>
    public ScheduleChangeKind ChangeKind { get; set; }

    /// <summary>
    /// Gets or sets when the shift starts (or started, before the change).
    /// </summary>
    public DateTime ShiftStartUtc { get; set; }

    /// <summary>
    /// Gets or sets how many hours before the shift the change came.
    /// </summary>
    public decimal NoticeHours { get; set; }

    /// <summary>
    /// Gets or sets the premium, in hours of pay.
    /// </summary>
    public decimal Hours { get; set; }

    /// <summary>
    /// Gets or sets the rule set.
    /// </summary>
    public string RuleSetId { get; set; }

    /// <summary>
    /// Gets or sets where it stands.
    /// </summary>
    public PredictabilityPayStatus Status { get; set; } = PredictabilityPayStatus.Pending;

    /// <summary>
    /// Gets or sets why it was waived, or another note.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets who approved or waived it.
    /// </summary>
    public string DecidedBy { get; set; }

    /// <summary>
    /// Gets or sets the UTC time it was decided.
    /// </summary>
    public DateTime? DecidedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time it was recorded.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
