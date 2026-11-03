namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// What clicking a visual does: the value of one of its columns in the row, bar, or slice clicked filters other visuals
/// of the report to that value, like picking an agent in a scorecard to show only that agent's calls and trend.
/// </summary>
public sealed class ReportVisualAction
{
    /// <summary>
    /// Gets or sets the identifier of the dimension column whose value is picked: a column of a table, or the category
    /// or series column of a chart.
    /// </summary>
    public string ColumnId { get; set; }

    /// <summary>
    /// Gets or sets the visuals the picked values filter.
    /// </summary>
    public IList<ReportActionTarget> Targets { get; set; } = [];
}
