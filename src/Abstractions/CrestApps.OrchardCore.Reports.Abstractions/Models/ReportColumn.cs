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
    /// <param name="headerStyle">The optional style applied to the column header cell.</param>
    public ReportColumn(string label, ReportColumnAlign align = ReportColumnAlign.Start, ReportStyle headerStyle = null)
    {
        Label = label;
        Align = align;
        HeaderStyle = headerStyle;
    }

    /// <summary>
    /// Gets or sets the column header label.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the column alignment.
    /// </summary>
    public ReportColumnAlign Align { get; set; }

    /// <summary>
    /// Gets or sets the optional style applied to the column header cell, allowing headers to be
    /// color-coded. Ignored by export formats that do not support styling.
    /// </summary>
    public ReportStyle HeaderStyle { get; set; }
}
