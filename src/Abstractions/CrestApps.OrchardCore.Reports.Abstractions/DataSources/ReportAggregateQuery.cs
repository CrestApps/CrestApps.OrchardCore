namespace CrestApps.OrchardCore.Reports.DataSources;

/// <summary>
/// A data source that can group and aggregate a data set itself, usually in its database, so a report that only counts,
/// sums, averages, or takes the smallest or largest values reads a few groups instead of every row. The engine asks
/// only for simple reports over one data set and falls back to reading rows whenever the source answers
/// <see langword="null"/>, so a source may decline anything it cannot do exactly.
/// </summary>
public interface IReportAggregateDataSource
{
    /// <summary>
    /// Groups and aggregates a data set.
    /// </summary>
    /// <param name="query">What to group by, what to compute, and which records to include.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The groups, or <see langword="null"/> when the source cannot answer this query exactly (a field it cannot group
    /// or aggregate, a condition it cannot apply exactly, or more groups than <see cref="ReportAggregateQuery.MaxGroups"/>).
    /// </returns>
    Task<ReportAggregateTable> AggregateAsync(ReportAggregateQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// A grouping and aggregation of one data set.
/// </summary>
public sealed class ReportAggregateQuery
{
    /// <summary>
    /// Gets or sets the data set.
    /// </summary>
    public string DataSet { get; set; }

    /// <summary>
    /// Gets or sets the context of the run; the source must answer only with records its principal may read.
    /// </summary>
    public ReportDataSourceContext Context { get; set; } = new();

    /// <summary>
    /// Gets the fields to group by, in the order of the result's first columns.
    /// </summary>
    public IList<ReportAggregateGroup> Groups { get; } = [];

    /// <summary>
    /// Gets the values to compute per group, in the order of the result's columns after the groups.
    /// </summary>
    public IList<ReportAggregateMeasure> Measures { get; } = [];

    /// <summary>
    /// Gets the conditions every record must meet. Unlike the conditions of a row query, which may be applied loosely,
    /// these must be applied exactly, or the source must decline the query:
    /// <list type="bullet">
    /// <item><description>comparisons are exact, with date-times in UTC;</description></item>
    /// <item><description>text compares ignoring case, the way the report does;</description></item>
    /// <item><description><see cref="ReportFilterOperator.NotEquals"/> and <see cref="ReportFilterOperator.NotIn"/>
    /// keep records whose value is empty;</description></item>
    /// <item><description>a source that cannot apply one of them exactly must return <see langword="null"/>.</description></item>
    /// </list>
    /// </summary>
    public IList<ReportDataCondition> Conditions { get; } = [];

    /// <summary>
    /// Gets or sets the most groups the result may have; a source with more returns <see langword="null"/>.
    /// </summary>
    public int MaxGroups { get; set; } = 10_000;
}

/// <summary>
/// A field to group by.
/// </summary>
public sealed class ReportAggregateGroup
{
    /// <summary>
    /// Gets or sets the field name.
    /// </summary>
    public string Field { get; set; }

    /// <summary>
    /// Gets or sets the bucket boundaries of a date field, in ascending order (UTC for date-time fields), or
    /// <see langword="null"/> to group by the value itself. With boundaries, the group's value is the zero-based index
    /// of the bucket the record falls in: bucket <c>i</c> holds values from <c>Boundaries[i]</c> (inclusive) to
    /// <c>Boundaries[i + 1]</c> (exclusive). Records outside every bucket, or without a value, fall in a group whose
    /// value is <see langword="null"/>.
    /// </summary>
    public IList<DateTime> Boundaries { get; set; }
}

/// <summary>
/// What an aggregate measure computes.
/// </summary>
public enum ReportAggregateKind
{
    /// <summary>
    /// The number of records with a value for the field, or of all records when the field is <see langword="null"/>.
    /// </summary>
    Count,

    /// <summary>
    /// The sum of the field's values.
    /// </summary>
    Sum,

    /// <summary>
    /// The smallest value of the field.
    /// </summary>
    Min,

    /// <summary>
    /// The largest value of the field.
    /// </summary>
    Max,
}

/// <summary>
/// A value computed per group.
/// </summary>
public sealed class ReportAggregateMeasure
{
    /// <summary>
    /// Gets or sets the field, or <see langword="null"/> for <see cref="ReportAggregateKind.Count"/> of all records.
    /// </summary>
    public string Field { get; set; }

    /// <summary>
    /// Gets or sets what to compute.
    /// </summary>
    public ReportAggregateKind Kind { get; set; }
}

/// <summary>
/// The groups of an aggregated data set.
/// </summary>
public sealed class ReportAggregateTable
{
    /// <summary>
    /// Gets or sets one row per group: the group values (in the order of <see cref="ReportAggregateQuery.Groups"/>)
    /// followed by the measures (in the order of <see cref="ReportAggregateQuery.Measures"/>). Values use the CLR types of
    /// <see cref="ReportDataType"/>, with date-times in UTC; a bucket is its index as a <see cref="long"/>.
    /// </summary>
    public IList<object[]> Rows { get; set; } = [];
}

/// <summary>
/// A data set of a <see cref="ReportRecordDataSource"/> that can group and aggregate itself.
/// </summary>
public interface IReportAggregateDataSet
{
    /// <summary>
    /// Groups and aggregates the data set; see <see cref="IReportAggregateDataSource.AggregateAsync"/>.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The groups, or <see langword="null"/> when the data set cannot answer exactly.</returns>
    Task<ReportAggregateTable> AggregateAsync(ReportAggregateQuery query, CancellationToken cancellationToken);
}
