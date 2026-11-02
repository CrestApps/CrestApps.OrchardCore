namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Identifies which values a quick table calculation (<see cref="ReportShowAs"/>) is computed along.
/// </summary>
public enum ReportShowAsDirection
{
    /// <summary>
    /// Down the rows: within each column of a cross-tab, along the rows of a table, and along the categories of each
    /// series of a chart.
    /// </summary>
    Down,

    /// <summary>
    /// Across the columns: within each row of a cross-tab, and across the series at each category of a chart. A table
    /// without columns across the top computes down its rows.
    /// </summary>
    Across,

    /// <summary>
    /// Over the whole table: every cell of a cross-tab or point of a chart, row by row.
    /// </summary>
    Table,
}
