using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// A rule that colors the cells of a column whose value meets a condition, such as red below 20, orange from 20 to 50
/// and green from 50. The rules of a column are tried in order and the first one a value meets colors its cell.
/// </summary>
public sealed class ReportColorRule
{
    /// <summary>
    /// Gets or sets the comparison operator, as a filter uses it.
    /// </summary>
    public DataFilterOperator Operator { get; set; }

    /// <summary>
    /// Gets or sets the values compared, written with the invariant culture, as a filter's values are.
    /// </summary>
    public IList<string> Values { get; set; } = [];

    /// <summary>
    /// Gets or sets the color, as a <c>#RRGGBB</c> hex value or a CSS color name.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets what part of the cell the color paints.
    /// </summary>
    public ReportColorTarget Target { get; set; }
}
