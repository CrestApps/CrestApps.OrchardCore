namespace CrestApps.OrchardCore.Reports.Models;

/// <summary>
/// Represents a single labeled bar in a report bars section, used for simple horizontal-bar breakdowns
/// (for example volume by channel or a daily trend).
/// </summary>
public sealed class ReportBar
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportBar"/> class.
    /// </summary>
    public ReportBar()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportBar"/> class.
    /// </summary>
    /// <param name="label">The bar label.</param>
    /// <param name="value">The pre-formatted value shown next to the bar.</param>
    /// <param name="ratio">The fill ratio of the bar, between 0 and 1.</param>
    public ReportBar(string label, string value, double ratio)
    {
        Label = label;
        Value = value;
        Ratio = ratio;
    }

    /// <summary>
    /// Gets or sets the bar label.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the pre-formatted value shown next to the bar.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets the fill ratio of the bar, between 0 and 1.
    /// </summary>
    public double Ratio { get; set; }
}
