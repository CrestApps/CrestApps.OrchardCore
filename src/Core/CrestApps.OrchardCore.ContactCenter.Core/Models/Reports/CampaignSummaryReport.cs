namespace CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;

/// <summary>
/// Represents the campaign summary report: per-campaign completed-versus-pending progress across the
/// activity inventory in a reporting period.
/// </summary>
public sealed class CampaignSummaryReport
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
    /// Gets or sets the per-campaign summary rows, ordered by total activities.
    /// </summary>
    public IList<CampaignSummaryRow> Rows { get; set; } = [];

    /// <summary>
    /// Gets or sets the campaign-group summary rows.
    /// </summary>
    public IList<CampaignGroupSummaryRow> GroupRows { get; set; } = [];

    /// <summary>
    /// Gets or sets the combined progress counts across every campaign in the report.
    /// </summary>
    public ActivityProgressCounts Totals { get; set; } = new();
}
