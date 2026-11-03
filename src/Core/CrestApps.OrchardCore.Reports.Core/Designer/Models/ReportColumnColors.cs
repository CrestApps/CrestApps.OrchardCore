namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// How a column of a designed report is colored in its tables: its header, every cell, a color scale over its numbers,
/// and rules that color the cells whose value meets a condition. Rules win over the scale, and the scale over the
/// color of every cell. Totals and subtotals keep their own style.
/// </summary>
public sealed class ReportColumnColors
{
    /// <summary>
    /// Gets or sets the text color of the column header.
    /// </summary>
    public string HeaderColor { get; set; }

    /// <summary>
    /// Gets or sets the background color of the column header.
    /// </summary>
    public string HeaderBackgroundColor { get; set; }

    /// <summary>
    /// Gets or sets the text color of every cell of the column.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets the background color of every cell of the column.
    /// </summary>
    public string BackgroundColor { get; set; }

    /// <summary>
    /// Gets or sets the color scale that shades the numbers of the column, or <see langword="null"/> for none.
    /// </summary>
    public ReportColorScale Scale { get; set; }

    /// <summary>
    /// Gets or sets the rules that color the cells whose value meets a condition, tried in order.
    /// </summary>
    public IList<ReportColorRule> Rules { get; set; } = [];
}
