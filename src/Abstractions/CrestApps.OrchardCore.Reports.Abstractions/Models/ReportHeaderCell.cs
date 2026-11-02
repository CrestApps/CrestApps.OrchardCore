namespace CrestApps.OrchardCore.Reports.Models;

/// <summary>
/// One cell of a header row shown above the column headers of a table, spanning one or more columns, such as
/// <c>Disposition</c> over the columns of each disposition of a cross-tab.
/// </summary>
public sealed class ReportHeaderCell
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportHeaderCell"/> class.
    /// </summary>
    public ReportHeaderCell()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportHeaderCell"/> class.
    /// </summary>
    /// <param name="label">The text of the cell; empty for a blank cell.</param>
    /// <param name="span">The number of columns the cell spans.</param>
    public ReportHeaderCell(string label, int span)
    {
        Label = label;
        Span = span;
    }

    /// <summary>
    /// Gets or sets the text of the cell; empty for a blank cell.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the number of columns the cell spans.
    /// </summary>
    public int Span { get; set; } = 1;
}
