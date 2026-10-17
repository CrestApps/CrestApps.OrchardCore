using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.Expressions;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Runs designed queries: reads each data set, joins them, evaluates calculated fields, applies filters, groups and
/// aggregates, then sorts and limits the result. Every step runs in memory over the rows the data sources return, so the
/// same features work for every source; the sources only need to describe and read their data sets.
/// </summary>
public sealed partial class ReportQueryEngine
{
    private readonly ReportQueryPlanner _planner;
    private readonly ReportValueFormatter _formatter;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportQueryEngine"/> class.
    /// </summary>
    /// <param name="planner">The query planner.</param>
    /// <param name="formatter">The value formatter used for filter options.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReportQueryEngine(
        ReportQueryPlanner planner,
        ReportValueFormatter formatter,
        IStringLocalizer<ReportQueryEngine> stringLocalizer)
    {
        _planner = planner;
        _formatter = formatter;
        S = stringLocalizer;
    }

    /// <summary>
    /// Plans and runs a query.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="context">The run context.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ReportQueryException">The query is not valid.</exception>
    public async Task<ReportQueryResult> ExecuteAsync(
        ReportQueryDefinition query,
        ReportQueryExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(context);

        var plan = await _planner.PlanAsync(query, context.DataSourceContext, cancellationToken);

        if (!plan.IsValid)
        {
            throw new ReportQueryException([.. plan.Errors]);
        }

        return await ExecuteAsync(plan, context, cancellationToken);
    }

    /// <summary>
    /// Runs a planned query.
    /// </summary>
    /// <param name="plan">The valid plan.</param>
    /// <param name="context">The run context.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result.</returns>
    public async Task<ReportQueryResult> ExecuteAsync(
        ReportQueryPlan plan,
        ReportQueryExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(context);

        if (!plan.IsValid)
        {
            throw new ReportQueryException([.. plan.Errors]);
        }

        var warnings = new List<string>();
        var today = context.ToLocal(context.UtcNow);
        var filters = plan.Filters
            .Select(filter => new ActiveFilter(filter, EffectiveValues(filter.Definition, context)))
            .ToList();

        // A simple aggregated report over one data set can be grouped by its source, which then reads a few groups
        // instead of every row.
        if (await TryAggregateAsync(plan, filters, context, today, warnings, cancellationToken) is { } grouped)
        {
            return grouped;
        }

        // Data sets are read in join order, so a joined data set can be read for the keys the rows before it hold.
        var current = await ReadAsync(plan, plan.DataSets[0], filters, context, warnings, keys: null, cancellationToken);
        var sourceRows = current.Count;

        foreach (var join in plan.Joins)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var keys = JoinKeys(join, current, context.Limits);
            var right = keys is { Values.Count: 0 }
                ? []
                : await ReadAsync(plan, join.DataSet, filters, context, warnings, keys, cancellationToken);

            sourceRows += right.Count;
            current = Join(current, right, join, context.Limits.MaxJoinedRows, warnings);
        }

        var expressionContext = new ExpressionContext
        {
            Now = today,
        };

        EvaluateRowCalculations(plan, current, expressionContext);

        var rowFilters = filters.Where(filter => filter.Filter.Definition.Stage == ReportFilterStage.Rows).ToList();
        var filtered = ApplyRowFilters(current, rowFilters.Where(filter => !filter.Filter.Definition.Exposed), today);
        var filterOptions = BuildFilterOptions(filtered, rowFilters, context.Limits.MaxFilterOptions);

        filtered = ApplyRowFilters(filtered, rowFilters.Where(filter => filter.Filter.Definition.Exposed), today);

        var result = BuildResult(plan, filtered, filters, today, expressionContext, context.Limits, warnings);

        result.SourceRowCount = sourceRows;
        result.FilteredRowCount = filtered.Count;

        foreach (var (filterId, options) in filterOptions)
        {
            result.FilterOptions[filterId] = options;
        }

        foreach (var warning in warnings.Distinct(StringComparer.Ordinal))
        {
            result.Warnings.Add(warning);
        }

        return result;
    }

    private static IList<string> EffectiveValues(ReportFilterDefinition filter, ReportQueryExecutionContext context)
    {
        if (filter.Exposed && context.FilterValues.TryGetValue(filter.Id, out var values))
        {
            return values ?? [];
        }

        return filter.Values ?? [];
    }

    // The keys a join can send to the data set it joins, so that data set reads only the records that can match: the
    // distinct values of a pair of fields whose right side the source can filter on exactly. Only joins that drop the
    // joined data set's unmatched records (inner and left) qualify. Returns null when the joined data set must be read
    // in full: no such pair, keys compared as text across types, or more keys than the limit.
    private static JoinKeyFilter JoinKeys(PlannedJoin join, List<object[]> left, ReportQueryLimits limits)
    {
        if (join.Type is not (ReportJoinType.Inner or ReportJoinType.Left) || limits.MaxJoinKeys < 1)
        {
            return null;
        }

        foreach (var (leftSlot, rightSlot, compareAsText) in join.Pairs)
        {
            var field = join.DataSet.UsedFields.Values.FirstOrDefault(candidate => candidate.Slot == rightSlot);
            var descriptor = field is null ? null : join.DataSet.Schema?.FindField(field.FieldName);

            if (compareAsText ||
                descriptor is not { IsKeyFilterable: true } ||
                field.DataType is not (ReportDataType.Text or ReportDataType.Integer))
            {
                continue;
            }

            var values = new Dictionary<string, object>(StringComparer.Ordinal);

            foreach (var row in left)
            {
                var value = ReportDataValues.Coerce(row[leftSlot], field.DataType);

                if (ReportDataValues.IsEmpty(value))
                {
                    continue;
                }

                values.TryAdd(ReportDataValues.ToKey(value), value);

                if (values.Count > limits.MaxJoinKeys)
                {
                    return null;
                }
            }

            return new JoinKeyFilter(field.FieldName, values.Values.ToList());
        }

        return null;
    }

    private async Task<List<object[]>> ReadAsync(
        ReportQueryPlan plan,
        PlannedDataSet dataSet,
        IReadOnlyList<ActiveFilter> filters,
        ReportQueryExecutionContext context,
        List<string> warnings,
        JoinKeyFilter keys,
        CancellationToken cancellationToken)
    {
        var maxRows = Math.Max(1, context.Limits.MaxRowsPerDataSet);
        var batches = keys is null
            ? [null]
            : keys.Values.Chunk(Math.Max(1, context.Limits.JoinKeyBatchSize)).ToList();
        var rows = new List<object[]>();
        var truncated = false;

        for (var index = 0; index < batches.Count; index++)
        {
            var query = CreateQuery(dataSet, filters, context, maxRows - rows.Count);

            if (batches[index] is { } batch)
            {
                query.Conditions.Add(new ReportDataCondition
                {
                    Field = keys.FieldName,
                    Operator = ReportFilterOperator.In,
                    Values = batch,
                    IsJoinKey = true,
                });
            }

            var read = await ReadTableAsync(plan, dataSet, query, context, cancellationToken);

            rows.AddRange(read.Rows);
            truncated |= read.Truncated;

            if (rows.Count >= maxRows)
            {
                truncated |= index < batches.Count - 1;

                break;
            }
        }

        if (truncated)
        {
            var label = dataSet.Reference.DisplayName ?? dataSet.Schema.DataSet?.DisplayName ?? dataSet.Reference.DataSet;

            warnings.Add(S["Only the first {0} rows of '{1}' were read. Add filters to narrow the data.", maxRows, label]);
        }

        return rows;
    }

    private static ReportDataSourceQuery CreateQuery(PlannedDataSet dataSet, IReadOnlyList<ActiveFilter> filters, ReportQueryExecutionContext context, int maxRows)
    {
        var query = new ReportDataSourceQuery
        {
            DataSet = dataSet.Reference.DataSet,
            Fields = new HashSet<string>(dataSet.UsedFields.Keys, StringComparer.Ordinal),
            MaxRows = Math.Max(1, maxRows),
            Context = context.DataSourceContext,
        };

        if (!dataSet.IsNullSupplying)
        {
            foreach (var filter in filters)
            {
                var field = filter.Filter.Field;

                if (filter.Filter.Definition.Stage != ReportFilterStage.Rows ||
                    field is null ||
                    field.Kind != PlannedFieldKind.DataSetField ||
                    !string.Equals(field.Alias, dataSet.Reference.Alias, StringComparison.Ordinal))
                {
                    continue;
                }

                var condition = ReportFilterPredicates.BuildCondition(field.FieldName, filter.Filter.Definition.Operator, field.DataType, filter.Values, context.ToUtc, context.ToLocal(context.UtcNow));

                if (condition is not null)
                {
                    query.Conditions.Add(condition);
                }
            }
        }

        return query;
    }

    private static async Task<(List<object[]> Rows, bool Truncated)> ReadTableAsync(
        ReportQueryPlan plan,
        PlannedDataSet dataSet,
        ReportDataSourceQuery query,
        ReportQueryExecutionContext context,
        CancellationToken cancellationToken)
    {
        var table = await dataSet.Source.QueryAsync(query, cancellationToken) ?? new ReportDataTable();
        var mappings = dataSet.UsedFields.Values
            .Select(field => (field.Slot, SourceIndex: table.IndexOf(field.FieldName), field.DataType))
            .ToArray();
        var rows = new List<object[]>(Math.Min(table.Rows.Count, query.MaxRows));

        foreach (var sourceRow in table.Rows.Take(query.MaxRows))
        {
            var row = new object[plan.Width];

            foreach (var (slot, sourceIndex, dataType) in mappings)
            {
                if (sourceIndex >= 0 && sourceIndex < sourceRow.Length)
                {
                    row[slot] = Normalize(sourceRow[sourceIndex], dataType, context.ToLocal);
                }
            }

            rows.Add(row);
        }

        return (rows, table.Truncated || table.Rows.Count > query.MaxRows);
    }

    private static object Normalize(object value, ReportDataType dataType, Func<DateTime, DateTime> toLocal)
    {
        if (value is null)
        {
            return null;
        }

        switch (dataType)
        {
            case ReportDataType.DateTime:
                if (ReportDataValues.Coerce(value, ReportDataType.DateTime) is not DateTime date)
                {
                    return null;
                }

                if (date.Kind == DateTimeKind.Local)
                {
                    date = date.ToUniversalTime();
                }

                return toLocal(DateTime.SpecifyKind(date, DateTimeKind.Utc));

            case ReportDataType.Text when value is string:
            case ReportDataType.Integer when value is long:
            case ReportDataType.Decimal when value is decimal:
            case ReportDataType.Boolean when value is bool:
                return value;

            default:
                return ReportDataValues.Coerce(value, dataType);
        }
    }

    private List<object[]> Join(
        List<object[]> left,
        List<object[]> right,
        PlannedJoin join,
        int maxRows,
        List<string> warnings)
    {
        var rightSlots = join.DataSet.UsedFields.Values.Select(field => field.Slot).ToArray();
        var index = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        for (var rightIndex = 0; rightIndex < right.Count; rightIndex++)
        {
            var key = JoinKey(right[rightIndex], join, useRight: true);

            if (key is null)
            {
                continue;
            }

            if (!index.TryGetValue(key, out var matches))
            {
                index[key] = matches = [];
            }

            matches.Add(rightIndex);
        }

        var matchedRight = new bool[right.Count];
        var output = new List<object[]>();
        var keepLeft = join.Type is ReportJoinType.Left or ReportJoinType.Full;
        var keepRight = join.Type is ReportJoinType.Right or ReportJoinType.Full;

        foreach (var leftRow in left)
        {
            var key = JoinKey(leftRow, join, useRight: false);

            if (key is not null && index.TryGetValue(key, out var matches))
            {
                foreach (var rightIndex in matches)
                {
                    var merged = (object[])leftRow.Clone();
                    var rightRow = right[rightIndex];

                    foreach (var slot in rightSlots)
                    {
                        merged[slot] = rightRow[slot];
                    }

                    matchedRight[rightIndex] = true;

                    if (!TryAdd(output, merged, maxRows, warnings))
                    {
                        return output;
                    }
                }
            }
            else if (keepLeft && !TryAdd(output, leftRow, maxRows, warnings))
            {
                return output;
            }
        }

        if (keepRight)
        {
            for (var rightIndex = 0; rightIndex < right.Count; rightIndex++)
            {
                if (!matchedRight[rightIndex] && !TryAdd(output, right[rightIndex], maxRows, warnings))
                {
                    return output;
                }
            }
        }

        return output;
    }

    private bool TryAdd(List<object[]> output, object[] row, int maxRows, List<string> warnings)
    {
        if (output.Count >= maxRows)
        {
            warnings.Add(S["The joins produced more than {0} rows, so the result is incomplete. Add filters or change the joins.", maxRows]);

            return false;
        }

        output.Add(row);

        return true;
    }

    private static string JoinKey(object[] row, PlannedJoin join, bool useRight)
    {
        var parts = new string[join.Pairs.Count];

        for (var index = 0; index < join.Pairs.Count; index++)
        {
            var (leftSlot, rightSlot, compareAsText) = join.Pairs[index];
            var value = row[useRight ? rightSlot : leftSlot];

            if (ReportDataValues.IsEmpty(value))
            {
                return null;
            }

            parts[index] = compareAsText
                ? "s:" + ReportDataValues.ToText(value).ToUpperInvariant()
                : ReportDataValues.ToKey(value);
        }

        return string.Join('\u001F', parts);
    }

    private static void EvaluateRowCalculations(ReportQueryPlan plan, List<object[]> rows, ExpressionContext context)
    {
        if (plan.RowCalculations.Count == 0)
        {
            return;
        }

        foreach (var row in rows)
        {
            context.Row = row;

            foreach (var calculation in plan.RowCalculations)
            {
                row[calculation.Slot] = SafeEvaluate(calculation.Expression, context);
            }
        }

        context.Row = null;
    }

    private static object SafeEvaluate(CompiledExpression expression, ExpressionContext context)
    {
        try
        {
            return expression.Evaluate(context);
        }
        catch (Exception exception) when (exception is ArithmeticException or FormatException or ArgumentException or InvalidCastException)
        {
            return null;
        }
    }

    private static List<object[]> ApplyRowFilters(List<object[]> rows, IEnumerable<ActiveFilter> filters, DateTime today)
    {
        var predicates = filters
            .Select(filter => (filter.Filter.Field.Slot, Predicate: ReportFilterPredicates.Build(filter.Filter.Definition.Operator, filter.Filter.DataType, filter.Values, today)))
            .Where(entry => entry.Predicate is not null)
            .ToArray();

        if (predicates.Length == 0)
        {
            return rows;
        }

        return rows
            .Where(row => predicates.All(entry => entry.Predicate(row[entry.Slot])))
            .ToList();
    }

    private Dictionary<string, IReadOnlyList<ReportFilterOption>> BuildFilterOptions(
        List<object[]> rows,
        IEnumerable<ActiveFilter> filters,
        int maxOptions)
    {
        var options = new Dictionary<string, IReadOnlyList<ReportFilterOption>>(StringComparer.Ordinal);

        foreach (var filter in filters)
        {
            var definition = filter.Filter.Definition;

            if (!definition.Exposed || !UsesOptions(definition, filter.Filter.DataType))
            {
                continue;
            }

            var slot = filter.Filter.Field.Slot;
            var distinct = new Dictionary<string, object>(StringComparer.Ordinal);

            foreach (var row in rows)
            {
                var value = row[slot];

                if (!ReportDataValues.IsEmpty(value))
                {
                    distinct.TryAdd(ReportDataValues.ToKey(value), value);
                }
            }

            options[definition.Id] = distinct.Values
                .Order(Comparer<object>.Create(ReportDataValues.Compare))
                .Take(Math.Max(1, maxOptions))
                .Select(value => new ReportFilterOption(ReportDataValues.ToText(value), _formatter.Format(value, filter.Filter.DataType)))
                .ToArray();
        }

        return options;
    }

    /// <summary>
    /// Determines whether an exposed filter renders as a list of the values found in the data.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="dataType">The type of the filtered values.</param>
    /// <returns><see langword="true"/> for drop-down and multi-select filters.</returns>
    public static bool UsesOptions(ReportFilterDefinition filter, ReportDataType dataType)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return filter.Control switch
        {
            ReportFilterControl.Select or ReportFilterControl.MultiSelect => true,
            ReportFilterControl.Auto => dataType == ReportDataType.Text && filter.Operator is ReportFilterOperator.Equals or ReportFilterOperator.In or ReportFilterOperator.NotEquals or ReportFilterOperator.NotIn,
            _ => false,
        };
    }

    private sealed record ActiveFilter(PlannedFilter Filter, IList<string> Values);

    // The keys of a join, for the field of the joined data set they are matched against.
    private sealed record JoinKeyFilter(string FieldName, IReadOnlyList<object> Values);
}
