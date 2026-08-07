namespace CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;

/// <summary>
/// Represents the subject inventory report: per-subject completed-versus-pending progress across the
/// activity inventory in a reporting period.
/// </summary>
public sealed class SubjectInventoryReport
{
    /// <summary>
    /// Gets or sets the inclusive lower UTC bound of the reporting period.
    /// </summary>
    public DateTime FromUtc { get; set; }

    /// <summary>
    /// Gets or sets the inclusive upper UTC bound of the reporting period.
    /// </summary>
    public DateTime ToUtc { get; set; }

    /// <summary>
    /// Gets or sets the per-subject inventory rows, ordered by total activities.
    /// </summary>
    public IList<SubjectInventoryRow> Rows { get; set; } = [];

    /// <summary>
    /// Gets or sets the combined progress counts across every subject in the report.
    /// </summary>
    public ActivityProgressCounts Totals { get; set; } = new();
}
