using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.Expressions;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// A designed query checked against the live data source schemas and compiled for execution. Rows are laid out as
/// arrays of <see cref="Width"/> slots; each field the query uses owns one slot.
/// </summary>
public sealed class ReportQueryPlan
{
    /// <summary>
    /// Gets the problems found while planning. A plan with errors cannot run.
    /// </summary>
    public IList<string> Errors { get; } = [];

    /// <summary>
    /// Gets a value indicating whether the plan has no errors.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Gets the planned data sets, in query order.
    /// </summary>
    public IList<PlannedDataSet> DataSets { get; } = [];

    /// <summary>
    /// Gets every field the query can refer to, by key: data set fields, calculated fields, and the built-in row count.
    /// </summary>
    public IDictionary<string, PlannedField> Fields { get; } = new Dictionary<string, PlannedField>(StringComparer.Ordinal);

    /// <summary>
    /// Gets the planned joins, in order.
    /// </summary>
    public IList<PlannedJoin> Joins { get; } = [];

    /// <summary>
    /// Gets the row-level calculated fields, in evaluation order.
    /// </summary>
    public IList<PlannedField> RowCalculations { get; } = [];

    /// <summary>
    /// Gets the planned filters.
    /// </summary>
    public IList<PlannedFilter> Filters { get; } = [];

    /// <summary>
    /// Gets the planned result columns.
    /// </summary>
    public IList<PlannedColumn> Columns { get; } = [];

    /// <summary>
    /// Gets the sort order as column indexes.
    /// </summary>
    public IList<(int ColumnIndex, bool Descending)> Sorts { get; } = [];

    /// <summary>
    /// Gets or sets the most result rows to keep.
    /// </summary>
    public int? Limit { get; set; }

    /// <summary>
    /// Gets or sets the number of slots in a row.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets a value indicating whether any column is a measure, so the result is grouped.
    /// </summary>
    public bool IsAggregated => Columns.Any(column => column.IsMeasure);
}

/// <summary>
/// One data set of a plan.
/// </summary>
public sealed class PlannedDataSet
{
    /// <summary>
    /// Gets or sets the reference from the query.
    /// </summary>
    public ReportDataSetReference Reference { get; set; }

    /// <summary>
    /// Gets or sets the data source.
    /// </summary>
    public IReportDataSource Source { get; set; }

    /// <summary>
    /// Gets or sets the schema of the data set.
    /// </summary>
    public ReportDataSetSchema Schema { get; set; }

    /// <summary>
    /// Gets the slot of each field the query reads, by field name.
    /// </summary>
    public IDictionary<string, PlannedField> UsedFields { get; } = new Dictionary<string, PlannedField>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets a value indicating whether an outer join can fill this data set's fields with empty values, which
    /// makes it unsafe to filter the data set while it is read.
    /// </summary>
    public bool IsNullSupplying { get; set; }
}

/// <summary>
/// Identifies where a planned field's values come from.
/// </summary>
public enum PlannedFieldKind
{
    /// <summary>
    /// A field read from a data set.
    /// </summary>
    DataSetField,

    /// <summary>
    /// A calculated field evaluated once per row.
    /// </summary>
    RowCalculation,

    /// <summary>
    /// A calculated field evaluated once per group.
    /// </summary>
    AggregateCalculation,
}

/// <summary>
/// One field of a plan.
/// </summary>
public sealed class PlannedField
{
    /// <summary>
    /// Gets or sets the field key: <c>alias.Field</c> or a calculated field name.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the field label.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the type of the field values.
    /// </summary>
    public ReportDataType DataType { get; set; }

    /// <summary>
    /// Gets or sets where the values come from.
    /// </summary>
    public PlannedFieldKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the data set alias, for a data set field.
    /// </summary>
    public string Alias { get; set; }

    /// <summary>
    /// Gets or sets the field name within its data set, for a data set field.
    /// </summary>
    public string FieldName { get; set; }

    /// <summary>
    /// Gets or sets the row slot that holds the values, or <c>-1</c> when the field has none.
    /// </summary>
    public int Slot { get; set; } = -1;

    /// <summary>
    /// Gets or sets the compiled formula of a calculated field.
    /// </summary>
    public CompiledExpression Expression { get; set; }

    /// <summary>
    /// Gets a value indicating whether the field is aggregated.
    /// </summary>
    public bool IsAggregate => Kind == PlannedFieldKind.AggregateCalculation;
}

/// <summary>
/// One join of a plan.
/// </summary>
public sealed class PlannedJoin
{
    /// <summary>
    /// Gets or sets the data set being attached.
    /// </summary>
    public PlannedDataSet DataSet { get; set; }

    /// <summary>
    /// Gets or sets the join type.
    /// </summary>
    public ReportJoinType Type { get; set; }

    /// <summary>
    /// Gets the slot pairs that must be equal, with whether the pair compares as text because the types differ.
    /// </summary>
    public IList<(int LeftSlot, int RightSlot, bool CompareAsText)> Pairs { get; } = [];
}

/// <summary>
/// One filter of a plan.
/// </summary>
public sealed class PlannedFilter
{
    /// <summary>
    /// Gets or sets the filter definition.
    /// </summary>
    public ReportFilterDefinition Definition { get; set; }

    /// <summary>
    /// Gets or sets the field a row filter reads.
    /// </summary>
    public PlannedField Field { get; set; }

    /// <summary>
    /// Gets or sets the index of the column a result filter reads.
    /// </summary>
    public int ColumnIndex { get; set; } = -1;

    /// <summary>
    /// Gets or sets the type of the values compared.
    /// </summary>
    public ReportDataType DataType { get; set; }
}

/// <summary>
/// One result column of a plan.
/// </summary>
public sealed class PlannedColumn
{
    /// <summary>
    /// Gets or sets the column definition.
    /// </summary>
    public ReportColumnDefinition Definition { get; set; }

    /// <summary>
    /// Gets or sets the field the column shows.
    /// </summary>
    public PlannedField Field { get; set; }

    /// <summary>
    /// Gets or sets the resolved column header.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the type of the column values.
    /// </summary>
    public ReportDataType DataType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is a measure.
    /// </summary>
    public bool IsMeasure { get; set; }
}
