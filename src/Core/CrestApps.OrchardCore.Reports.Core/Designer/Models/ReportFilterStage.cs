namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Identifies when a filter applies.
/// </summary>
public enum ReportFilterStage
{
    /// <summary>
    /// The filter removes rows before they are grouped.
    /// </summary>
    Rows,

    /// <summary>
    /// The filter removes result rows after grouping, so it can compare aggregated values.
    /// </summary>
    Result,
}
