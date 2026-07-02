namespace CrestApps.OrchardCore.Reports.Models;

/// <summary>
/// Represents a single headline metric shown as a card in a report metrics section.
/// </summary>
public sealed class ReportMetric
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportMetric"/> class.
    /// </summary>
    public ReportMetric()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportMetric"/> class.
    /// </summary>
    /// <param name="label">The metric label.</param>
    /// <param name="value">The pre-formatted metric value.</param>
    /// <param name="hint">An optional secondary hint shown under the value.</param>
    public ReportMetric(string label, string value, string hint = null)
    {
        Label = label;
        Value = value;
        Hint = hint;
    }

    /// <summary>
    /// Gets or sets the metric label.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the pre-formatted metric value.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets an optional secondary hint shown under the value.
    /// </summary>
    public string Hint { get; set; }
}
