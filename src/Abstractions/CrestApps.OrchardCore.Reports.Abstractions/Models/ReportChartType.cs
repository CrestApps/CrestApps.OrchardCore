namespace CrestApps.OrchardCore.Reports.Models;

/// <summary>
/// Identifies the visualization used to render a report chart.
/// </summary>
public enum ReportChartType
{
    /// <summary>
    /// Renders values as a line chart.
    /// </summary>
    Line,

    /// <summary>
    /// Renders values as a vertical bar chart.
    /// </summary>
    Bar,

    /// <summary>
    /// Renders values as a doughnut chart.
    /// </summary>
    Doughnut,
}
