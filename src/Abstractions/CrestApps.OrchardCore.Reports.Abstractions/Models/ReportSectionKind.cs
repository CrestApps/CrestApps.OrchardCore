namespace CrestApps.OrchardCore.Reports.Models;

/// <summary>
/// Identifies the kind of content a report section renders.
/// </summary>
public enum ReportSectionKind
{
    /// <summary>
    /// The section renders a set of headline metric cards.
    /// </summary>
    Metrics,

    /// <summary>
    /// The section renders a tabular grid of columns and rows.
    /// </summary>
    Table,

    /// <summary>
    /// The section renders a set of horizontal bars.
    /// </summary>
    Bars,
}
