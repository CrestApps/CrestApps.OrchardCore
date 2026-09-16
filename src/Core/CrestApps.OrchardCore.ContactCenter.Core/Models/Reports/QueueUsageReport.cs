namespace CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;

/// <summary>
/// Represents the queue usage report: per-queue handled volume and current waiting depth over a
/// reporting period.
/// </summary>
public sealed class QueueUsageReport
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
    /// Gets or sets the per-queue usage rows, ordered by handled volume.
    /// </summary>
    public IList<QueueUsageRow> Rows { get; set; } = [];

    /// <summary>
    /// Gets or sets usage aggregated by each queue's current queue-group membership.
    /// </summary>
    public IList<QueueGroupUsageRow> GroupRows { get; set; } = [];

    /// <summary>
    /// Gets or sets the combined usage across every queue in the filtered report.
    /// </summary>
    public QueueUsageTotals Totals { get; set; } = new();
}
