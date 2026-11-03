namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// A line drawn across a chart at the average or median of a value column, or at a constant value such as a target.
/// </summary>
public sealed class ReportReferenceLine
{
    /// <summary>
    /// Gets or sets the identifier of the value column the line is computed from, one the chart plots. A constant line
    /// uses the column to format its value.
    /// </summary>
    public string ColumnId { get; set; }

    /// <summary>
    /// Gets or sets what value the line is drawn at.
    /// </summary>
    public ReportReferenceLineKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the value of a <see cref="ReportReferenceLineKind.Constant"/> line, as the chart's value axis reads
    /// it: a percentage shown by the chart is read in percent, so 25 is 25%.
    /// </summary>
    public decimal? Value { get; set; }

    /// <summary>
    /// Gets or sets the label shown with the line's value. When empty, the kind of the line is shown, such as Average.
    /// </summary>
    public string Label { get; set; }
}
