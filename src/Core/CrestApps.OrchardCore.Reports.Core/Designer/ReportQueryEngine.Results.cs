using System.Runtime.CompilerServices;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Expressions;
using OrchardCore.DataSources.Operations;

namespace CrestApps.OrchardCore.Reports.Designer;

public sealed partial class ReportQueryEngine
{
    // The rows of the result, in their order: the values of the columns of each row, or of each group, filtered by the
    // result filters, sorted and limited. Groups are computed by Orchard Core's aggregator and rows sorted by its
    // sorter, both with a bounded amount of memory; a result that is neither grouped nor sorted streams its rows.
    private static async IAsyncEnumerable<object[]> ReadResultRowsAsync(
        QueryRun run,
        IAsyncEnumerable<object[]> rows,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var plan = run.Plan;
        var aggregation = plan.IsAggregated ? new Aggregation(plan, run.Weighted) : null;
        var outputs = aggregation is null
            ? ProjectAsync(plan, rows, cancellationToken)
            : AggregateAsync(run, aggregation, rows, cancellationToken);

        outputs = ApplyResultFilters(run, outputs, cancellationToken);

        var sorts = plan.Sorts.ToList();

        // A grouped result without sorts is ordered by its groups.
        if (sorts.Count == 0 && aggregation is not null)
        {
            sorts = aggregation.DimensionColumns.Select(index => (index, false)).ToList();
        }

        if (sorts.Count > 0)
        {
            outputs = DataSorter.SortAsync(outputs, DataSorter.CreateComparer(sorts), run.CreateSpillOptions(), cancellationToken);
        }

        var limit = plan.Limit ?? int.MaxValue;
        var count = 0;

        await foreach (var output in outputs.WithCancellation(cancellationToken))
        {
            if (count >= limit)
            {
                // The options of the exposed filters are read from every row.
                if (run.ReadsOnlyWhatItKeeps && run.ListsOptions)
                {
                    continue;
                }

                yield break;
            }

            count++;

            yield return output;
        }
    }

    private static async IAsyncEnumerable<object[]> ProjectAsync(ReportQueryPlan plan, IAsyncEnumerable<object[]> rows, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            var values = new object[plan.Columns.Count];

            for (var index = 0; index < plan.Columns.Count; index++)
            {
                values[index] = ColumnValue(plan.Columns[index], row);
            }

            yield return values;
        }
    }

    // The groups of the rows, each as the values of the columns. Each row is reduced to the values of the dimensions and
    // what each measure aggregates, kept in a buffer that writes them to a temporary file when they don't fit in memory
    // when the run is regrouped later, such as for subtotals.
    private static async IAsyncEnumerable<object[]> AggregateAsync(
        QueryRun run,
        Aggregation aggregation,
        IAsyncEnumerable<object[]> rows,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var expressionContext = new ExpressionContext
        {
            Now = run.Today,
        };
        var reduced = ReduceAsync(aggregation, rows, run.GroupedRows, expressionContext, cancellationToken);
        var groups = DataAggregator.AggregateAsync(
            reduced,
            [.. Enumerable.Range(0, aggregation.DimensionColumns.Length)],
            aggregation.Measures,
            run.CreateSpillOptions(),
            cancellationToken);

        await foreach (var group in groups.WithCancellation(cancellationToken))
        {
            yield return aggregation.ToValues(group, aggregation.DimensionColumns, expressionContext);
        }
    }

    private static async IAsyncEnumerable<object[]> ReduceAsync(
        Aggregation aggregation,
        IAsyncEnumerable<object[]> rows,
        DataRowBuffer copy,
        ExpressionContext expressionContext,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            var reduced = aggregation.Reduce(row, expressionContext);

            copy?.Add(reduced);

            yield return reduced;
        }
    }

    private static async IAsyncEnumerable<object[]> ApplyResultFilters(QueryRun run, IAsyncEnumerable<object[]> outputs, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var predicates = run.Filters
            .Where(filter => filter.Filter.Definition.Stage == ReportFilterStage.Result)
            .Select(filter => (filter.Filter.ColumnIndex, Predicate: DataFilterPredicates.Build(filter.Filter.Definition.Operator, filter.Filter.DataType, filter.Values, run.Today)))
            .Where(entry => entry.Predicate is not null)
            .ToArray();

        await foreach (var output in outputs.WithCancellation(cancellationToken))
        {
            if (predicates.All(entry => entry.Predicate(output[entry.ColumnIndex])))
            {
                yield return output;
            }
        }
    }

    // The result of a run, with the groupings the run was asked for, computed from the rows of the groups it kept.
    private async Task<ReportQueryResult> CreateResultAsync(QueryRun run, List<object[]> kept, CancellationToken cancellationToken)
    {
        var plan = run.Plan;
        var columns = DescribeColumns(plan);

        if (!plan.IsAggregated)
        {
            return new ReportQueryResult(columns, kept, isAggregated: false, indexes => RegroupRows(plan, kept, indexes));
        }

        var groupings = new Dictionary<string, ReportResultGroup[]>(StringComparer.Ordinal);

        if (run.GroupedRows is { } groupedRows)
        {
            var aggregation = new Aggregation(plan, run.Weighted);

            foreach (var ids in run.Groupings)
            {
                var indexes = ReportQueryResult.ResolveIndexes(columns, ids);
                var key = GroupingKey(indexes);

                if (!groupings.ContainsKey(key))
                {
                    groupings[key] = await RegroupAsync(run, aggregation, groupedRows, kept, indexes, cancellationToken);
                }
            }
        }

        return new ReportQueryResult(columns, kept, isAggregated: true, indexes =>
            groupings.TryGetValue(GroupingKey(indexes), out var groups)
                ? groups
                : throw new InvalidOperationException($"The result was not grouped by the columns {string.Join(", ", indexes.Select(index => columns[index].Id))}. Ask for the grouping in {nameof(ReportQueryExecutionContext)}.{nameof(ReportQueryExecutionContext.Groupings)}."));
    }

    // Groups the rows of the groups a grouped result kept again, by some of its dimensions, so averages and distinct
    // counts are computed over the rows, not over the groups. The groups come out in the order of the first group of
    // the result they contain.
    private static async Task<ReportResultGroup[]> RegroupAsync(
        QueryRun run,
        Aggregation aggregation,
        DataRowBuffer groupedRows,
        List<object[]> kept,
        IReadOnlyList<int> indexes,
        CancellationToken cancellationToken)
    {
        var plan = run.Plan;
        var dimensions = aggregation.DimensionColumns;
        var allPositions = Enumerable.Range(0, dimensions.Length).ToArray();
        var keptKeys = kept.Select(values => DataKeys.Build(values, dimensions, ignoreCase: false)).ToHashSet(StringComparer.Ordinal);
        var subset = indexes.Where(index => index >= 0 && index < plan.Columns.Count && !plan.Columns[index].IsMeasure).ToArray();
        var positions = subset.Select(index => Array.IndexOf(dimensions, index)).ToArray();
        var order = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var position = 0; position < kept.Count; position++)
        {
            order.TryAdd(DataKeys.Build(kept[position], subset, ignoreCase: false), position);
        }

        async IAsyncEnumerable<object[]> RowsOfKeptGroups([EnumeratorCancellation] CancellationToken token = default)
        {
            await foreach (var row in ToAsync(groupedRows.Read(), token).WithCancellation(token))
            {
                if (keptKeys.Contains(DataKeys.Build(row, allPositions, ignoreCase: false)))
                {
                    yield return row;
                }
            }
        }

        var expressionContext = new ExpressionContext
        {
            Now = run.Today,
        };
        var groups = new List<(int Order, object[] Values)>();

        await foreach (var group in DataAggregator.AggregateAsync(RowsOfKeptGroups(cancellationToken), positions, aggregation.Measures, run.CreateSpillOptions(), cancellationToken).WithCancellation(cancellationToken))
        {
            var values = aggregation.ToValues(group, subset, expressionContext);

            groups.Add((order.GetValueOrDefault(DataKeys.Build(values, subset, ignoreCase: false), int.MaxValue), values));
        }

        return groups
            .OrderBy(group => group.Order)
            .Select(group => new ReportResultGroup(group.Values))
            .ToArray();
    }

    // Groups the rows of a result that isn't grouped, adding up the numbers of the other columns. Text that differs only
    // in case is one group, shown with its first spelling.
    private static ReportResultGroup[] RegroupRows(ReportQueryPlan plan, IReadOnlyList<object[]> rows, IReadOnlyList<int> dimensionIndexes)
    {
        var dimensions = dimensionIndexes
            .Where(index => index >= 0 && index < plan.Columns.Count && !plan.Columns[index].IsMeasure)
            .ToArray();
        var groups = new Dictionary<string, object[]>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach (var row in rows)
        {
            var key = DataKeys.Build(row, dimensions, ignoreCase: true);

            if (!groups.TryGetValue(key, out var values))
            {
                values = new object[plan.Columns.Count];

                foreach (var index in dimensions)
                {
                    values[index] = row[index];
                }

                groups[key] = values;
                order.Add(key);
            }

            for (var index = 0; index < plan.Columns.Count; index++)
            {
                if (dimensions.Contains(index))
                {
                    continue;
                }

                var value = row[index];

                values[index] = DataValues.IsNumeric(plan.Columns[index].DataType)
                    ? DataAggregations.Sum([values[index], value])
                    : values[index] ?? value;
            }
        }

        return order.Select(key => new ReportResultGroup(groups[key])).ToArray();
    }

    private static string GroupingKey(IReadOnlyList<int> indexes) => string.Join(',', indexes);

    private static ReportResultColumn ToResultColumn(PlannedColumn column)
    {
        return new ReportResultColumn
        {
            Id = column.Definition.Id,
            Field = column.Field.Key,
            Label = column.Label,
            DataType = column.DataType,
            IsMeasure = column.IsMeasure,
            Aggregate = column.Field.IsAggregate ? DataAggregate.None : column.Definition.Aggregate,
            Transform = column.Definition.Transform,
            Format = column.Definition.Format,
            Hidden = column.Definition.Hidden,
        };
    }

    private static object ColumnValue(PlannedColumn column, object[] row)
    {
        return DataFieldTransforms.Apply(column.Definition.Transform, row[column.Field.Slot]);
    }

    // How a grouped result reduces a row to what its groups need: the values of its dimensions, then what each measure
    // aggregates, and how a group of these reduced rows gives the values of the columns. Text that differs only in case
    // groups and counts as one value, like filters and joins compare it: each dimension and distinct count takes the
    // first spelling it reads, since Orchard Core's aggregator compares text exactly.
    private sealed class Aggregation
    {
        private readonly ReportQueryPlan _plan;
        private readonly int? _weightSlot;
        private readonly MeasureColumn[] _measureColumns;
        private readonly Dictionary<string, string>[] _spellings;

        public Aggregation(ReportQueryPlan plan, bool weighted)
        {
            _plan = plan;
            _weightSlot = weighted ? plan.Width : null;
            DimensionColumns = Enumerable.Range(0, plan.Columns.Count).Where(index => !plan.Columns[index].IsMeasure).ToArray();

            var measures = new List<DataMeasure>();
            var measureColumns = new List<MeasureColumn>();
            var position = DimensionColumns.Length;

            for (var index = 0; index < plan.Columns.Count; index++)
            {
                var column = plan.Columns[index];

                if (!column.IsMeasure)
                {
                    continue;
                }

                MeasureKind kind;
                DataAggregate[] functions;

                // Rows a data source grouped itself each stand for several rows; the row count adds them up.
                if (_weightSlot is not null && column.Field.Key == ReportQueryPlanner.RowCountField)
                {
                    kind = MeasureKind.Weight;
                    functions = [DataAggregate.Sum];
                }
                else if (column.Field.IsAggregate)
                {
                    kind = MeasureKind.Formula;
                    functions = [.. column.Field.Expression.Aggregates.Select(aggregate => aggregate.Function)];
                }
                else
                {
                    kind = MeasureKind.Value;
                    functions = [column.Definition.Aggregate];
                }

                measureColumns.Add(new MeasureColumn(index, kind, measures.Count, functions.Length));

                foreach (var function in functions)
                {
                    measures.Add(new DataMeasure(function, position++));
                }
            }

            Measures = measures;
            _measureColumns = [.. measureColumns];
            Width = position;
            _spellings = new Dictionary<string, string>[plan.Columns.Count];
        }

        public int[] DimensionColumns { get; }

        public IReadOnlyList<DataMeasure> Measures { get; }

        public int Width { get; }

        public object[] Reduce(object[] row, ExpressionContext context)
        {
            var reduced = new object[Width];

            for (var index = 0; index < DimensionColumns.Length; index++)
            {
                reduced[index] = Spelling(DimensionColumns[index], ColumnValue(_plan.Columns[DimensionColumns[index]], row));
            }

            var position = DimensionColumns.Length;

            foreach (var measure in _measureColumns)
            {
                var column = _plan.Columns[measure.ColumnIndex];

                switch (measure.Kind)
                {
                    case MeasureKind.Weight:
                        reduced[position++] = row[_weightSlot.Value];
                        break;

                    case MeasureKind.Formula:
                        context.Row = row;

                        foreach (var aggregate in column.Field.Expression.Aggregates)
                        {
                            reduced[position++] = SafeArgument(aggregate, context);
                        }

                        context.Row = null;
                        break;

                    default:
                        var value = ColumnValue(column, row);

                        reduced[position++] = column.Definition.Aggregate == DataAggregate.CountDistinct ? Spelling(measure.ColumnIndex, value) : value;
                        break;
                }
            }

            return reduced;
        }

        // The first spelling read of a text value, ignoring case.
        private object Spelling(int columnIndex, object value)
        {
            if (value is not string text)
            {
                return value;
            }

            var spellings = _spellings[columnIndex] ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!spellings.TryGetValue(text, out var spelling))
            {
                spellings[text] = spelling = text;
            }

            return spelling;
        }

        // The values of the columns of a group: its dimensions, then the value of each measure.
        public object[] ToValues(object[] group, int[] dimensionColumns, ExpressionContext context)
        {
            var values = new object[_plan.Columns.Count];

            for (var index = 0; index < dimensionColumns.Length; index++)
            {
                values[dimensionColumns[index]] = group[index];
            }

            var results = dimensionColumns.Length;

            foreach (var measure in _measureColumns)
            {
                var column = _plan.Columns[measure.ColumnIndex];
                var start = results + measure.Start;

                values[measure.ColumnIndex] = measure.Kind switch
                {
                    MeasureKind.Weight => DataFieldTransforms.Apply(column.Definition.Transform, DataValues.Coerce(group[start], DataFieldType.Integer) ?? 0L),
                    MeasureKind.Formula => DataFieldTransforms.Apply(column.Definition.Transform, Evaluate(column.Field.Expression, group[start..(start + measure.Count)], context)),
                    _ => group[start],
                };
            }

            return values;
        }

        private static object Evaluate(CompiledExpression expression, object[] aggregateValues, ExpressionContext context)
        {
            context.AggregateValues = aggregateValues;

            try
            {
                return SafeEvaluate(expression, context);
            }
            finally
            {
                context.AggregateValues = null;
            }
        }

        private static object SafeArgument(ExpressionAggregate aggregate, ExpressionContext context)
        {
            try
            {
                return aggregate.Argument(context);
            }
            catch (Exception exception) when (exception is ArithmeticException or FormatException or ArgumentException or InvalidCastException)
            {
                return null;
            }
        }

        private enum MeasureKind
        {
            Value,
            Formula,
            Weight,
        }

        private sealed record MeasureColumn(int ColumnIndex, MeasureKind Kind, int Start, int Count);
    }
}
