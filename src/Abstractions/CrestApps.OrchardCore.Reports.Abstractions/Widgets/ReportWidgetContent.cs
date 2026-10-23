namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// What of a report a dashboard widget shows.
/// </summary>
public enum ReportWidgetContent
{
    /// <summary>
    /// The figures and the tables, without charts: what fits a dashboard tile.
    /// </summary>
    FiguresAndTables,

    /// <summary>
    /// Only the figures (metrics).
    /// </summary>
    FiguresOnly,

    /// <summary>
    /// Only the tables.
    /// </summary>
    TablesOnly,

    /// <summary>
    /// Only the charts.
    /// </summary>
    ChartsOnly,

    /// <summary>
    /// Everything the report shows.
    /// </summary>
    Everything,
}
