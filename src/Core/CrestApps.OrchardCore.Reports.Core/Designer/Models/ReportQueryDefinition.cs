namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// The data half of a designed report or a reusable view: the data sets it reads, how they are joined, the calculated
/// fields, the filters, and the columns, sorting, and limit of the result.
/// </summary>
public sealed class ReportQueryDefinition
{
    /// <summary>
    /// Gets or sets the data sets the query reads. The first one is the base data set; every other one is attached by
    /// exactly one entry of <see cref="Joins"/>.
    /// </summary>
    public IList<ReportDataSetReference> DataSets { get; set; } = [];

    /// <summary>
    /// Gets or sets how the data sets are joined, applied in order.
    /// </summary>
    public IList<ReportJoinDefinition> Joins { get; set; } = [];

    /// <summary>
    /// Gets or sets the calculated fields. A row-level calculated field may refer only to data set fields and to
    /// calculated fields listed before it.
    /// </summary>
    public IList<ReportCalculatedField> CalculatedFields { get; set; } = [];

    /// <summary>
    /// Gets or sets the filters.
    /// </summary>
    public IList<ReportFilterDefinition> Filters { get; set; } = [];

    /// <summary>
    /// Gets or sets the result columns, in display order. When any column is a measure, rows are grouped by the
    /// dimension columns.
    /// </summary>
    public IList<ReportColumnDefinition> Columns { get; set; } = [];

    /// <summary>
    /// Gets or sets the sort order of the result.
    /// </summary>
    public IList<ReportSortDefinition> Sorts { get; set; } = [];

    /// <summary>
    /// Gets or sets the most rows the result keeps after sorting, or <see langword="null"/> to keep every row.
    /// </summary>
    public int? Limit { get; set; }
}
