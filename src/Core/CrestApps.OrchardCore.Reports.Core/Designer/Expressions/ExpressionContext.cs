namespace CrestApps.OrchardCore.Reports.Designer.Expressions;

/// <summary>
/// The state a compiled formula reads while it is evaluated: the current row for a row-level formula, or the rows of
/// the current group for an aggregate formula.
/// </summary>
public sealed class ExpressionContext
{
    /// <summary>
    /// Gets or sets the current row, laid out as the scope the formula was compiled against describes.
    /// </summary>
    public object[] Row { get; set; }

    /// <summary>
    /// Gets or sets the rows of the current group, read by aggregate functions.
    /// </summary>
    public IReadOnlyList<object[]> Group { get; set; }

    /// <summary>
    /// Gets or sets the current date and time in the tenant time zone, returned by <c>NOW()</c> and <c>TODAY()</c>.
    /// </summary>
    public DateTime Now { get; set; }
}
