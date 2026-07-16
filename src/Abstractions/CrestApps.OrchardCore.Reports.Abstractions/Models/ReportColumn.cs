namespace CrestApps.OrchardCore.Reports.Models;

/// <summary>
/// Represents a single column of a report table section.
/// </summary>
public sealed class ReportColumn
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportColumn"/> class.
    /// </summary>
    public ReportColumn()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportColumn"/> class.
    /// </summary>
    /// <param name="label">The column header label.</param>
    /// <param name="align">The column alignment.</param>
    public ReportColumn(string label, ReportColumnAlign align = ReportColumnAlign.Start)
    {
        Label = label;
        Align = align;
    }

    /// <summary>
    /// Gets or sets the column header label.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the column alignment.
    /// </summary>
    public ReportColumnAlign Align { get; set; }
}
