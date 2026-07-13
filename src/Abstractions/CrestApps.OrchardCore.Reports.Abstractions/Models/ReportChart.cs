namespace CrestApps.OrchardCore.Reports.Models;

/// <summary>
/// Represents a chart visualization in a report document.
/// </summary>
public sealed class ReportChart
{
    /// <summary>
    /// Gets or sets the chart visualization type.
    /// </summary>
    public ReportChartType Type { get; set; }

    /// <summary>
    /// Gets or sets the category labels shared by every dataset.
    /// </summary>
    public IList<string> Labels { get; set; } = [];

    /// <summary>
    /// Gets or sets the chart datasets.
    /// </summary>
    public IList<ReportChartDataset> Datasets { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether bar datasets are stacked.
    /// </summary>
    public bool Stacked { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the chart legend is displayed.
    /// </summary>
    public bool ShowLegend { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the vertical axis displays percentages.
    /// </summary>
    public bool PercentageScale { get; set; }
}
