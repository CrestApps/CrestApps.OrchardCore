namespace CrestApps.OrchardCore.Reports.Models;

/// <summary>
/// A header row shown above the column headers of a table, whose cells group the columns below them.
/// </summary>
public sealed class ReportHeaderRow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReportHeaderRow"/> class.
    /// </summary>
    public ReportHeaderRow()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportHeaderRow"/> class.
    /// </summary>
    /// <param name="cells">The cells, left to right. Their spans add up to the number of columns of the table.</param>
    public ReportHeaderRow(IEnumerable<ReportHeaderCell> cells)
    {
        Cells = cells is null ? [] : [.. cells];
    }

    /// <summary>
    /// Gets or sets the cells, left to right. Their spans add up to the number of columns of the table.
    /// </summary>
    public IList<ReportHeaderCell> Cells { get; set; } = [];
}
