using System.Runtime.CompilerServices;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Expressions;
using OrchardCore.DataSources.Operations;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Runs designed queries: reads each data set, joins them, evaluates calculated fields, applies filters, groups and
/// aggregates, then sorts and limits the result. The rows stream through these steps a batch at a time: the joins,
/// groupings and sorts are Orchard Core's data operations, which keep a bounded amount of rows in memory and write the
/// others to temporary files, so a run takes about the same memory whatever the size of its data. The same features
/// work for every source; the sources only need to describe and read their data sets.
/// </summary>
public sealed partial class ReportQueryEngine
{
    private readonly ReportQueryPlanner _planner;
    private readonly ReportValueFormatter _formatter;
    private readonly ReportQueryGate _gate;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportQueryEngine"/> class.
    /// </summary>
    /// <param name="planner">The query planner.</param>
    /// <param name="formatter">The value formatter used for filter options.</param>
    /// <param name="gate">The gate that bounds how many runs the tenant runs at once.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReportQueryEngine(
        ReportQueryPlanner planner,
        ReportValueFormatter formatter,
        ReportQueryGate gate,
        IStringLocalizer<ReportQueryEngine> stringLocalizer)
    {
        _planner = planner;
        _formatter = formatter;
        _gate = gate;
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
    /// Runs a planned query, and keeps the first <see cref="ReportQueryLimits.MaxResultRows"/> rows of its result, with
    /// the groupings of <see cref="ReportQueryExecutionContext.Groupings"/>.
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

        await using var lease = await _gate.EnterAsync(context, cancellationToken);
        using var run = new QueryRun(plan, context, groupings: true);

        var grouped = await TryAggregateAsync(run, cancellationToken);
        var rows = grouped is null ? ReadFilteredRowsAsync(run, cancellationToken) : ToAsync(grouped, cancellationToken);
        var maxResultRows = Math.Max(1, context.Limits.MaxResultRows);

        run.Weighted = grouped is not null;

        var kept = await TakeAsync(ReadResultRowsAsync(run, rows, cancellationToken), maxResultRows + 1, drain: run.ReadsOnlyWhatItKeeps && run.ListsOptions, cancellationToken);

        if (kept.Count > maxResultRows)
        {
            run.Warnings.Add(S["The result has more than {0} rows, so only the first {0} are shown.", maxResultRows]);
            kept.RemoveRange(maxResultRows, kept.Count - maxResultRows);
        }

        var result = await CreateResultAsync(run, kept, cancellationToken);

        result.SourceRowCount = (int)Math.Min(int.MaxValue, run.SourceRows);
        result.FilteredRowCount = (int)Math.Min(int.MaxValue, run.FilteredRows);
        result.GroupedBySource = grouped is not null;

        foreach (var (filterId, options) in run.Options)
        {
            result.FilterOptions[filterId] = options
                .Take(Math.Max(1, context.Limits.MaxFilterOptions))
                .Select(value => new ReportFilterOption(DataValues.ToText(value), _formatter.Format(value, run.OptionTypes[filterId])))
                .ToArray();
        }

        foreach (var warning in run.Warnings.Distinct(StringComparer.Ordinal))
        {
            result.Warnings.Add(warning);
        }

        return result;
    }

    /// <summary>
    /// Runs a planned query and streams its result rows, in their order, up to <paramref name="maxRows"/>, without
    /// keeping them, such as for a view that another report reads.
    /// </summary>
    /// <param name="plan">The valid plan.</param>
    /// <param name="context">The run context.</param>
    /// <param name="maxRows">The most rows to read.</param>
    /// <param name="warnings">Receives the warnings of the run, such as a data set read in part.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The rows, each holding the values of the columns of the plan.</returns>
    public async IAsyncEnumerable<object[]> ReadAsync(
        ReportQueryPlan plan,
        ReportQueryExecutionContext context,
        int maxRows,
        ICollection<string> warnings,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(context);

        if (!plan.IsValid)
        {
            throw new ReportQueryException([.. plan.Errors]);
        }

        await using var lease = await _gate.EnterAsync(context, cancellationToken);
        using var run = new QueryRun(plan, context, groupings: false);

        try
        {
            var grouped = await TryAggregateAsync(run, cancellationToken);
            var rows = grouped is null ? ReadFilteredRowsAsync(run, cancellationToken) : ToAsync(grouped, cancellationToken);
            var read = 0;

            run.Weighted = grouped is not null;

            await foreach (var row in ReadResultRowsAsync(run, rows, cancellationToken).WithCancellation(cancellationToken))
            {
                if (read++ >= maxRows)
                {
                    yield break;
                }

                yield return row;
            }
        }
        finally
        {
            foreach (var warning in run.Warnings.Distinct(StringComparer.Ordinal))
            {
                warnings?.Add(warning);
            }
        }
    }

    /// <summary>
    /// Describes the columns of the result of a plan.
    /// </summary>
    /// <param name="plan">The plan.</param>
    /// <returns>The columns.</returns>
    public static IReadOnlyList<ReportResultColumn> DescribeColumns(ReportQueryPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return plan.Columns.Select(ToResultColumn).ToArray();
    }

    /// <summary>
    /// Determines whether an exposed filter renders as a list of the values found in the data.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="dataType">The type of the filtered values.</param>
    /// <returns><see langword="true"/> for drop-down and multi-select filters.</returns>
    public static bool UsesOptions(ReportFilterDefinition filter, DataFieldType dataType)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return filter.Control switch
        {
            ReportFilterControl.Select or ReportFilterControl.MultiSelect => true,
            ReportFilterControl.Auto => dataType == DataFieldType.Text && filter.Operator is DataFilterOperator.Equals or DataFilterOperator.In or DataFilterOperator.NotEquals or DataFilterOperator.NotIn,
            _ => false,
        };
    }

    // The rows of the data sets, joined, calculated and filtered.
    private async IAsyncEnumerable<object[]> ReadFilteredRowsAsync(QueryRun run, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var plan = run.Plan;

        // Data sets are read in join order, so a joined data set can be read for the keys the rows before it hold.
        var rows = ReadDataSetAsync(run, plan.DataSets[0], keys: null, cancellationToken);

        foreach (var join in plan.Joins)
        {
            rows = JoinAsync(run, rows, join, cancellationToken);
        }

        var rowFilters = run.Filters.Where(filter => filter.Filter.Definition.Stage == ReportFilterStage.Rows).ToList();
        var fixedFilters = Predicates(rowFilters.Where(filter => !filter.Filter.Definition.Exposed), run.Today);
        var exposedFilters = Predicates(rowFilters.Where(filter => filter.Filter.Definition.Exposed), run.Today);
        var expressionContext = new ExpressionContext
        {
            Now = run.Today,
        };

        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            if (plan.RowCalculations.Count > 0)
            {
                expressionContext.Row = row;

                foreach (var calculation in plan.RowCalculations)
                {
                    row[calculation.Slot] = SafeEvaluate(calculation.Expression, expressionContext);
                }
            }

            if (!Passes(row, fixedFilters))
            {
                continue;
            }

            // The options of an exposed filter are the values the other exposed filters leave out too.
            run.AddOptions(row);

            if (!Passes(row, exposedFilters))
            {
                continue;
            }

            run.FilteredRows++;

            yield return row;
        }
    }

    private async IAsyncEnumerable<object[]> ReadDataSetAsync(
        QueryRun run,
        PlannedDataSet dataSet,
        JoinKeyFilter keys,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var limits = run.Context.Limits;
        var maxRows = Math.Max(1, limits.MaxRowsPerDataSet);
        var batches = keys is null
            ? [null]
            : keys.Values.Chunk(Math.Max(1, limits.JoinKeyBatchSize)).ToList();
        var read = 0L;
        var truncated = false;

        try
        {
            for (var index = 0; index < batches.Count && !truncated; index++)
            {
                var query = CreateQuery(dataSet, run.Filters, run.Context);

                // One more row than kept, to tell whether the data set has more.
                query.MaxRows = (int)Math.Min(int.MaxValue, maxRows - read + 1);

                if (batches[index] is { } batch)
                {
                    query.Conditions.Add(new DataCondition
                    {
                        Field = keys.FieldName,
                        Operator = DataFilterOperator.In,
                        Values = batch,
                        IsJoinKey = true,
                    });
                }

                IReadOnlyList<DataField> fields = null;
                (int Slot, int SourceIndex, DataFieldType DataType)[] mappings = [];

                await foreach (var source in dataSet.Source.ReadAsync(query, cancellationToken).WithCancellation(cancellationToken))
                {
                    if (!ReferenceEquals(source.Fields, fields))
                    {
                        fields = source.Fields;
                        mappings = dataSet.UsedFields.Values
                            .Select(field => (field.Slot, SourceIndex: fields.IndexOf(field.FieldName), field.DataType))
                            .ToArray();
                    }

                    foreach (var sourceRow in source.Rows)
                    {
                        if (read >= maxRows)
                        {
                            truncated = true;

                            break;
                        }

                        read++;

                        yield return MapRow(run, sourceRow, mappings);
                    }

                    if (truncated)
                    {
                        break;
                    }
                }
            }
        }
        finally
        {
            run.SourceRows += read;

            if (truncated)
            {
                var label = dataSet.Reference.DisplayName ?? dataSet.Schema.DataSet?.DisplayName ?? dataSet.Reference.DataSet;

                run.Warnings.Add(S["Only the first {0} rows of '{1}' were read. Add filters to narrow the data.", maxRows, label]);
            }
        }
    }

    private static object[] MapRow(QueryRun run, object[] sourceRow, (int Slot, int SourceIndex, DataFieldType DataType)[] mappings)
    {
        var row = new object[run.Plan.Width];

        foreach (var (slot, sourceIndex, dataType) in mappings)
        {
            if (sourceIndex >= 0 && sourceIndex < sourceRow.Length)
            {
                row[slot] = Normalize(sourceRow[sourceIndex], dataType, run.Context.ToLocal);
            }
        }

        return row;
    }

    private static DataSourceQuery CreateQuery(PlannedDataSet dataSet, IReadOnlyList<ActiveFilter> filters, ReportQueryExecutionContext context)
    {
        var query = new DataSourceQuery
        {
            DataSet = dataSet.Reference.DataSet,
            Fields = new HashSet<string>(dataSet.UsedFields.Keys, StringComparer.Ordinal),
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

                var condition = DataFilterPredicates.BuildCondition(field.FieldName, filter.Filter.Definition.Operator, field.DataType, filter.Values, context.ToUtc, context.ToLocal(context.UtcNow));

                if (condition is not null)
                {
                    query.Conditions.Add(condition);
                }
            }
        }

        return query;
    }

    private static object Normalize(object value, DataFieldType dataType, Func<DateTime, DateTime> toLocal)
    {
        if (value is null)
        {
            return null;
        }

        switch (dataType)
        {
            case DataFieldType.DateTime:
                if (DataValues.Coerce(value, DataFieldType.DateTime) is not DateTime date)
                {
                    return null;
                }

                if (date.Kind == DateTimeKind.Local)
                {
                    date = date.ToUniversalTime();
                }

                return toLocal(DateTime.SpecifyKind(date, DateTimeKind.Utc));

            case DataFieldType.Text when value is string:
            case DataFieldType.Integer when value is long:
            case DataFieldType.Decimal when value is decimal:
            case DataFieldType.Boolean when value is bool:
                return value;

            default:
                return DataValues.Coerce(value, dataType);
        }
    }

    // Joins the rows read so far with a data set. The rows read so far are held in a buffer that writes the rows that
    // don't fit in memory to a temporary file, while the keys they hold are collected, so the joined data set is read
    // only for the records that can match; then Orchard Core's join matches them with a bounded amount of memory too.
    private async IAsyncEnumerable<object[]> JoinAsync(
        QueryRun run,
        IAsyncEnumerable<object[]> left,
        PlannedJoin join,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var width = run.Plan.Width;
        var pairs = join.Pairs;
        var rightSlots = join.DataSet.UsedFields.Values.Select(field => field.Slot).ToArray();
        var keyField = FindKeyField(join, run.Context.Limits);
        var keys = keyField is null ? null : new Dictionary<string, object>(StringComparer.Ordinal);
        var maxJoinKeys = run.Context.Limits.MaxJoinKeys;

        using var leftRows = new DataRowBuffer(run.CreateSpillOptions());

        // Each row carries its key values after its fields: the text of a pair compared as text, the value otherwise.
        await foreach (var row in left.WithCancellation(cancellationToken))
        {
            leftRows.Add(WithKeys(row, width, pairs));

            if (keys is not null && DataValues.Coerce(row[pairs[keyField.Pair].LeftSlot], keyField.DataType) is { } value && !DataValues.IsEmpty(value))
            {
                keys.TryAdd(DataValues.ToKey(value), value);

                // With more keys than the limit, the joined data set is read like the others.
                if (keys.Count > maxJoinKeys)
                {
                    keys = null;
                }
            }
        }

        var right = keys is null
            ? ReadDataSetAsync(run, join.DataSet, keys: null, cancellationToken)
            : keys.Count == 0
                ? Empty()
                : ReadDataSetAsync(run, join.DataSet, new JoinKeyFilter(keyField.FieldName, [.. keys.Values]), cancellationToken);

        var leftWidth = width + pairs.Count;
        var rightWidth = rightSlots.Length + pairs.Count;
        var joined = DataJoin.JoinAsync(
            ToAsync(leftRows.Read(), cancellationToken),
            CompactAsync(right, rightSlots, pairs, cancellationToken),
            leftWidth,
            rightWidth,
            [.. Enumerable.Range(width, pairs.Count)],
            [.. Enumerable.Range(rightSlots.Length, pairs.Count)],
            join.Type,
            ignoreCase: true,
            run.CreateSpillOptions(),
            cancellationToken);

        var maxRows = Math.Max(1, run.Context.Limits.MaxJoinedRows);
        var produced = 0L;

        await foreach (var pair in joined.WithCancellation(cancellationToken))
        {
            if (produced++ >= maxRows)
            {
                run.Warnings.Add(S["The joins produced more than {0} rows, so the result is incomplete. Add filters or change the joins.", maxRows]);

                yield break;
            }

            // The fields of the left row, with the fields of the joined data set in their slots.
            var row = new object[width];

            Array.Copy(pair, row, width);

            for (var index = 0; index < rightSlots.Length; index++)
            {
                row[rightSlots[index]] = pair[leftWidth + index];
            }

            yield return row;
        }
    }

    // The rows of a joined data set with only the values of its fields, followed by their key values.
    private static async IAsyncEnumerable<object[]> CompactAsync(
        IAsyncEnumerable<object[]> rows,
        int[] slots,
        IList<(int LeftSlot, int RightSlot, bool CompareAsText)> pairs,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            var compact = new object[slots.Length + pairs.Count];

            for (var index = 0; index < slots.Length; index++)
            {
                compact[index] = row[slots[index]];
            }

            for (var index = 0; index < pairs.Count; index++)
            {
                compact[slots.Length + index] = KeyValue(row[pairs[index].RightSlot], pairs[index].CompareAsText);
            }

            yield return compact;
        }
    }

    // A row read so far, followed by its key values.
    private static object[] WithKeys(object[] row, int width, IList<(int LeftSlot, int RightSlot, bool CompareAsText)> pairs)
    {
        var keyed = new object[width + pairs.Count];

        Array.Copy(row, keyed, Math.Min(row.Length, width));

        for (var index = 0; index < pairs.Count; index++)
        {
            keyed[width + index] = KeyValue(row[pairs[index].LeftSlot], pairs[index].CompareAsText);
        }

        return keyed;
    }

    // The value a join matches: a missing value never matches, and the values of a pair compared as text match by their
    // text. The join ignores the case of text, like a lookup by DataValues.ToKey.
    private static object KeyValue(object value, bool compareAsText)
    {
        if (DataValues.IsEmpty(value))
        {
            return null;
        }

        return compareAsText ? DataValues.ToText(value) : value;
    }

    // The field of a joined data set the keys of the rows read so far can be sent to, so it reads only the records that
    // can match: the right side of a pair the source can filter on exactly. Only joins that drop the joined data set's
    // unmatched records (inner and left) qualify, and not keys compared as text across types.
    private static JoinKeyField FindKeyField(PlannedJoin join, ReportQueryLimits limits)
    {
        if (join.Type is not (DataJoinType.Inner or DataJoinType.Left) || limits.MaxJoinKeys < 1)
        {
            return null;
        }

        for (var index = 0; index < join.Pairs.Count; index++)
        {
            var (_, rightSlot, compareAsText) = join.Pairs[index];
            var field = join.DataSet.UsedFields.Values.FirstOrDefault(candidate => candidate.Slot == rightSlot);
            var descriptor = field is null ? null : join.DataSet.Schema?.FindField(field.FieldName);

            if (!compareAsText &&
                descriptor is { IsKeyFilterable: true } &&
                field.DataType is DataFieldType.Text or DataFieldType.Integer)
            {
                return new JoinKeyField(index, field.FieldName, field.DataType);
            }
        }

        return null;
    }

    private static List<(int Slot, Func<object, bool> Predicate)> Predicates(IEnumerable<ActiveFilter> filters, DateTime today)
    {
        return filters
            .Select(filter => (filter.Filter.Field.Slot, Predicate: DataFilterPredicates.Build(filter.Filter.Definition.Operator, filter.Filter.DataType, filter.Values, today)))
            .Where(entry => entry.Predicate is not null)
            .ToList();
    }

    private static bool Passes(object[] row, List<(int Slot, Func<object, bool> Predicate)> predicates)
    {
        foreach (var (slot, predicate) in predicates)
        {
            if (!predicate(row[slot]))
            {
                return false;
            }
        }

        return true;
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

    private static async Task<List<object[]>> TakeAsync(IAsyncEnumerable<object[]> rows, int count, bool drain, CancellationToken cancellationToken)
    {
        var taken = new List<object[]>();

        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            if (taken.Count < count)
            {
                taken.Add(row);
            }
            else if (!drain)
            {
                break;
            }
        }

        return taken;
    }

    private static async IAsyncEnumerable<object[]> ToAsync(IEnumerable<object[]> rows, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var count = 0;

        foreach (var row in rows)
        {
            if (++count % 4096 == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await Task.Yield();
            }

            yield return row;
        }
    }

#pragma warning disable CS1998 // An empty sequence awaits nothing.
    private static async IAsyncEnumerable<object[]> Empty()
    {
        yield break;
    }
#pragma warning restore CS1998

    private static IList<string> EffectiveValues(ReportFilterDefinition filter, ReportQueryExecutionContext context)
    {
        if (filter.Exposed && context.FilterValues.TryGetValue(filter.Id, out var values))
        {
            return values ?? [];
        }

        return filter.Values ?? [];
    }

    private sealed record ActiveFilter(PlannedFilter Filter, IList<string> Values);

    // The keys of a join, for the field of the joined data set they are matched against.
    private sealed record JoinKeyFilter(string FieldName, IReadOnlyList<object> Values);

    // The field of a joined data set its keys can be read for, and the pair it is the right side of.
    private sealed record JoinKeyField(int Pair, string FieldName, DataFieldType DataType);

    // The state of one run: its filters, counts, filter options and warnings, and the directory of its temporary files,
    // deleted when it ends.
    private sealed class QueryRun : IDisposable
    {
        private readonly string _directory;
        private readonly long _memoryLimit;
        private readonly List<(string FilterId, int Slot, SortedSet<object> Values)> _optionFilters = [];
        private readonly int _maxOptions;
        private int _spills;

        public QueryRun(ReportQueryPlan plan, ReportQueryExecutionContext context, bool groupings)
        {
            Plan = plan;
            Context = context;
            Today = context.ToLocal(context.UtcNow);
            Filters = plan.Filters
                .Select(filter => new ActiveFilter(filter, EffectiveValues(filter.Definition, context)))
                .ToList();
            Groupings = groupings ? [.. context.Groupings] : [];
            _maxOptions = Math.Max(1, context.Limits.MaxFilterOptions);
            _memoryLimit = Math.Max(1, context.Limits.MemoryLimitMegabytes) * 1024L * 1024L;
            _directory = Path.Combine(
                string.IsNullOrEmpty(context.Limits.TemporaryDirectory) ? Path.GetTempPath() : context.Limits.TemporaryDirectory,
                "CrestApps.Reports",
                Guid.NewGuid().ToString("n"));

            foreach (var filter in Filters)
            {
                var definition = filter.Filter.Definition;

                if (definition.Stage == ReportFilterStage.Rows && definition.Exposed && UsesOptions(definition, filter.Filter.DataType))
                {
                    _optionFilters.Add((definition.Id, filter.Filter.Field.Slot, new SortedSet<object>(Comparer<object>.Create(DataValues.Compare))));
                    OptionTypes[definition.Id] = filter.Filter.DataType;
                }
            }

            // A grouped result keeps a copy of the rows it grouped, reduced to what its groups need, to regroup them.
            if (plan.IsAggregated && Groupings.Count > 0)
            {
                GroupedRows = new DataRowBuffer(CreateSpillOptions());
            }
        }

        public ReportQueryPlan Plan { get; }

        public ReportQueryExecutionContext Context { get; }

        public DateTime Today { get; }

        public List<ActiveFilter> Filters { get; }

        public IList<IReadOnlyList<string>> Groupings { get; }

        public List<string> Warnings { get; } = [];

        public long SourceRows { get; set; }

        public long FilteredRows { get; set; }

        // Whether each row is a group a data source computed, which stands for several rows.
        public bool Weighted { get; set; }

        public DataRowBuffer GroupedRows { get; }

        // Whether the run must read every row for the options of its exposed filters, even once its result is complete.
        public bool ListsOptions => _optionFilters.Count > 0;

        // Whether the result streams the rows it reads, so it can stop reading once it has them: a result that is
        // grouped or sorted reads every row before its first one.
        public bool ReadsOnlyWhatItKeeps => !Plan.IsAggregated && Plan.Sorts.Count == 0;

        public Dictionary<string, DataFieldType> OptionTypes { get; } = new(StringComparer.Ordinal);

        // The smallest distinct values of each exposed filter that lists options, in order.
        public IEnumerable<(string FilterId, IEnumerable<object> Values)> Options => _optionFilters.Select(filter => (filter.FilterId, (IEnumerable<object>)filter.Values));

        public void AddOptions(object[] row)
        {
            foreach (var (_, slot, values) in _optionFilters)
            {
                var value = row[slot];

                // Only the smallest values are listed, so only they are kept.
                if (!DataValues.IsEmpty(value) && values.Add(value) && values.Count > _maxOptions)
                {
                    values.Remove(values.Max);
                }
            }
        }

        public DataSpillOptions CreateSpillOptions()
        {
            return new DataSpillOptions(Path.Combine(_directory, (++_spills).ToString(System.Globalization.CultureInfo.InvariantCulture)), _memoryLimit);
        }

        public void Dispose()
        {
            GroupedRows?.Dispose();

            try
            {
                if (Directory.Exists(_directory))
                {
                    Directory.Delete(_directory, recursive: true);
                }
            }
            catch (IOException)
            {
                // A file still open is deleted with the temporary directory of the system.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
