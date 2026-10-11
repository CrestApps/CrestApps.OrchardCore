namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// One column of a designed query's result. A column is a measure when it aggregates its field or shows an aggregate
/// calculated field; otherwise it is a dimension the result is grouped by.
/// </summary>
public sealed class ReportColumnDefinition
{
    /// <summary>
    /// Gets or sets the identifier of the column, unique within the query. Sorts, result filters, and visuals refer to
    /// columns by this identifier, and a view exposes each column as a field with this name.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the key of the field shown: <c>alias.Field</c> for a data set field, or the name of a calculated
    /// field.
    /// </summary>
    public string Field { get; set; }

    /// <summary>
    /// Gets or sets the column header. When empty, the field label is used.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the aggregate applied to the field.
    /// </summary>
    public ReportAggregate Aggregate { get; set; }

    /// <summary>
    /// Gets or sets the transform applied to each value before grouping and aggregating.
    /// </summary>
    public ReportFieldTransform Transform { get; set; }

    /// <summary>
    /// Gets or sets the .NET format string used to display values (for example <c>N2</c>, <c>C2</c>, <c>P1</c>, or
    /// <c>yyyy-MM-dd</c>). When empty, a format suited to the data type is used.
    /// </summary>
    public string Format { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is hidden from the table. A hidden column still groups the
    /// result and can feed charts and filters.
    /// </summary>
    public bool Hidden { get; set; }
}
