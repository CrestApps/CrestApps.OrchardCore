namespace CrestApps.OrchardCore.Reports.Models;

/// <summary>
/// A line drawn across a chart at a value of its value axis, such as an average or a target.
/// </summary>
public sealed class ReportChartReferenceLine
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportChartReferenceLine"/> class.
    /// </summary>
    /// <param name="label">The text shown with the line, such as <c>Average: 1,250</c>.</param>
    /// <param name="value">The value the line is drawn at.</param>
    public ReportChartReferenceLine(string label, double value)
    {
        Label = label;
        Value = value;
    }

    /// <summary>
    /// Gets or sets the text shown with the line.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the value the line is drawn at, on the chart's value axis.
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// Gets or sets the index of the dataset whose color the line takes, or <see langword="null"/> for a neutral color.
    /// </summary>
    public int? DatasetIndex { get; set; }
}
