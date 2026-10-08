namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Identifies the kind of a designed report visual.
/// </summary>
public enum ReportVisualType
{
    /// <summary>
    /// A table of the result rows.
    /// </summary>
    Table,

    /// <summary>
    /// A chart of measure columns by a category column.
    /// </summary>
    Chart,

    /// <summary>
    /// Headline metric cards showing each measure over the whole result.
    /// </summary>
    Metrics,

    /// <summary>
    /// A cross-tab with row columns down the side and the values of one dimension across the top.
    /// </summary>
    Pivot,
}
