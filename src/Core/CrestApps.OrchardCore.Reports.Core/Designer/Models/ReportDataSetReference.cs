namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Points a query at one data set of one data source, under an alias that qualifies its fields.
/// </summary>
public sealed class ReportDataSetReference
{
    /// <summary>
    /// Gets or sets the alias that qualifies the data set's fields, as in <c>alias.Field</c>. It is unique within the
    /// query, starts with a letter, and holds only letters, digits, and underscores.
    /// </summary>
    public string Alias { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the data source.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the data set within the source.
    /// </summary>
    public string DataSet { get; set; }

    /// <summary>
    /// Gets or sets the label of the data set when it was added, shown by the designer.
    /// </summary>
    public string DisplayName { get; set; }
}
