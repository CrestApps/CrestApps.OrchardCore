namespace CrestApps.OrchardCore.Reports.Models;

/// <summary>
/// Represents one named series in a report chart.
/// </summary>
public sealed class ReportChartDataset
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportChartDataset"/> class.
    /// </summary>
    /// <param name="label">The series label.</param>
    /// <param name="values">The numeric values aligned with the chart labels.</param>
    public ReportChartDataset(string label, IEnumerable<double> values)
    {
        Label = label;
        Values = values is null ? [] : [.. values];
    }

    /// <summary>
    /// Gets or sets the series label.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the numeric values aligned with the chart labels.
    /// </summary>
    public IList<double> Values { get; set; } = [];
}
