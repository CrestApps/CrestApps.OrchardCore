namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// The result of running a designed query: its columns, rows, and the information needed to aggregate the same rows
/// again by fewer dimensions (for charts, metrics, pivots, and totals).
/// </summary>
public sealed class ReportQueryResult
{
    private readonly Func<IReadOnlyList<int>, IReadOnlyList<ReportResultGroup>> _regroup;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportQueryResult"/> class.
    /// </summary>
    /// <param name="columns">The result columns.</param>
    /// <param name="rows">The result rows, each aligned with <paramref name="columns"/>.</param>
    /// <param name="isAggregated">Whether the rows are groups.</param>
    /// <param name="regroup">Aggregates the rows again by the dimension columns at the given indexes.</param>
    public ReportQueryResult(
        IReadOnlyList<ReportResultColumn> columns,
        IReadOnlyList<object[]> rows,
        bool isAggregated,
        Func<IReadOnlyList<int>, IReadOnlyList<ReportResultGroup>> regroup)
    {
        Columns = columns ?? [];
        Rows = rows ?? [];
        IsAggregated = isAggregated;
        _regroup = regroup;
    }

    /// <summary>
    /// Gets the result columns.
    /// </summary>
    public IReadOnlyList<ReportResultColumn> Columns { get; }

    /// <summary>
    /// Gets the result rows, each aligned with <see cref="Columns"/>.
    /// </summary>
    public IReadOnlyList<object[]> Rows { get; }

    /// <summary>
    /// Gets a value indicating whether the rows are groups because at least one column is a measure.
    /// </summary>
    public bool IsAggregated { get; }

    /// <summary>
    /// Gets the warnings raised while running, such as a data set that had more rows than the limit.
    /// </summary>
    public IList<string> Warnings { get; } = [];

    /// <summary>
    /// Gets the values offered by exposed drop-down filters, by filter identifier.
    /// </summary>
    public IDictionary<string, IReadOnlyList<ReportFilterOption>> FilterOptions { get; } = new Dictionary<string, IReadOnlyList<ReportFilterOption>>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the number of rows read from the data sets.
    /// </summary>
    public int SourceRowCount { get; set; }

    /// <summary>
    /// Gets or sets the number of rows left after the joins and row filters.
    /// </summary>
    public int FilteredRowCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the data source grouped and aggregated the rows itself, so the report
    /// read groups instead of rows.
    /// </summary>
    public bool GroupedBySource { get; set; }

    /// <summary>
    /// Finds the index of a column.
    /// </summary>
    /// <param name="columnId">The column identifier.</param>
    /// <returns>The index, or <c>-1</c> when there is no such column.</returns>
    public int IndexOf(string columnId)
    {
        for (var index = 0; index < Columns.Count; index++)
        {
            if (string.Equals(Columns[index].Id, columnId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Aggregates the rows behind the result again, grouped only by the given dimension columns. Measures are
    /// recomputed from the underlying rows, so an average stays a true average. Only rows that contributed to a result
    /// row count, so result filters and the row limit are respected. When the result is not aggregated, numeric columns
    /// are summed and other columns keep their first value.
    /// </summary>
    /// <param name="dimensionColumnIds">The identifiers of the dimension columns to group by; empty for a grand total.</param>
    /// <returns>The groups, ordered by where each first appears in the result.</returns>
    public IReadOnlyList<ReportResultGroup> Regroup(IEnumerable<string> dimensionColumnIds)
    {
        var indexes = (dimensionColumnIds ?? [])
            .Select(IndexOf)
            .Where(index => index >= 0)
            .Distinct()
            .ToArray();

        return _regroup?.Invoke(indexes) ?? [];
    }

    /// <summary>
    /// Computes the grand total of every measure over the whole result.
    /// </summary>
    /// <returns>The totals aligned with <see cref="Columns"/>; dimension positions are <see langword="null"/>.</returns>
    public object[] GetTotals()
    {
        var groups = Regroup([]);

        return groups.Count > 0
            ? groups[0].Values
            : new object[Columns.Count];
    }
}

/// <summary>
/// One group produced by <see cref="ReportQueryResult.Regroup(IEnumerable{string})"/>.
/// </summary>
/// <param name="Values">The values aligned with the result columns; dimensions outside the grouping are <see langword="null"/>.</param>
public sealed record ReportResultGroup(object[] Values);

/// <summary>
/// One value offered by an exposed drop-down filter.
/// </summary>
/// <param name="Value">The value as invariant text, submitted back as the filter value.</param>
/// <param name="Text">The value as shown to people.</param>
public sealed record ReportFilterOption(string Value, string Text);
