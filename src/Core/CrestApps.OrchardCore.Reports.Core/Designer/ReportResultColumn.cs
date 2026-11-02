using OrchardCore.DataSources;
using OrchardCore.DataSources.Operations;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Describes one column of a designed query result.
/// </summary>
public sealed class ReportResultColumn
{
    /// <summary>
    /// Gets or sets the column identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the key of the field the column shows.
    /// </summary>
    public string Field { get; set; }

    /// <summary>
    /// Gets or sets the column header.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the type of the column values.
    /// </summary>
    public DataFieldType DataType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is a measure.
    /// </summary>
    public bool IsMeasure { get; set; }

    /// <summary>
    /// Gets or sets the aggregate the column applies.
    /// </summary>
    public DataAggregate Aggregate { get; set; }

    /// <summary>
    /// Gets or sets the transform the column applies.
    /// </summary>
    public DataFieldTransform Transform { get; set; }

    /// <summary>
    /// Gets or sets the display format.
    /// </summary>
    public string Format { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is hidden from tables.
    /// </summary>
    public bool Hidden { get; set; }

    /// <summary>
    /// Gets or sets the shelf the column is placed on.
    /// </summary>
    public ReportColumnShelf Shelf { get; set; }

    /// <summary>
    /// Gets or sets how the column is colored in tables, or <see langword="null"/> for the default colors.
    /// </summary>
    public ReportColumnColors Colors { get; set; }

    /// <summary>
    /// Gets or sets how the column shows its values.
    /// </summary>
    public ReportShowAs ShowAs { get; set; }

    /// <summary>
    /// Gets or sets which values the calculation of <see cref="ShowAs"/> is computed along.
    /// </summary>
    public ReportShowAsDirection ShowAsDirection { get; set; }
}
