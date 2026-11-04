namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Identifies the value a chart reference line is drawn at.
/// </summary>
public enum ReportReferenceLineKind
{
    /// <summary>
    /// The average of the values the chart plots for the column.
    /// </summary>
    Average,

    /// <summary>
    /// The median of the values the chart plots for the column.
    /// </summary>
    Median,

    /// <summary>
    /// A constant value, such as a target.
    /// </summary>
    Constant,
}
