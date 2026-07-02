namespace CrestApps.OrchardCore.Omnichannel.Managements.Models.Reports;

/// <summary>
/// Represents the aggregated data behind the CRM activity summary report.
/// </summary>
public sealed class OmnichannelActivitySummaryData
{
    /// <summary>
    /// Gets or sets the overall completed-versus-pending progress counts.
    /// </summary>
    public OmnichannelProgressCounts Counts { get; set; } = new();

    /// <summary>
    /// Gets or sets the activity counts grouped by source.
    /// </summary>
    public IReadOnlyDictionary<string, long> BySource { get; set; } = new Dictionary<string, long>();

    /// <summary>
    /// Gets or sets the activity counts grouped by channel.
    /// </summary>
    public IReadOnlyDictionary<string, long> ByChannel { get; set; } = new Dictionary<string, long>();

    /// <summary>
    /// Gets or sets the activity counts grouped by status.
    /// </summary>
    public IReadOnlyDictionary<string, long> ByStatus { get; set; } = new Dictionary<string, long>();

    /// <summary>
    /// Gets or sets the daily created-activity trend.
    /// </summary>
    public IList<OmnichannelActivityDailyPoint> Daily { get; set; } = [];
}
