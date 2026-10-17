namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// A field computed with a formula. A formula that uses an aggregate function (such as <c>SUM</c>) is evaluated once
/// per group of the result; any other formula is evaluated once per row.
/// </summary>
public sealed class ReportCalculatedField
{
    /// <summary>
    /// Gets or sets the unique name of the field, used to refer to it as <c>[Name]</c>. It starts with a letter and
    /// holds only letters, digits, and underscores.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the label shown in the designer and as the default column header.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the formula.
    /// </summary>
    public string Expression { get; set; }
}
