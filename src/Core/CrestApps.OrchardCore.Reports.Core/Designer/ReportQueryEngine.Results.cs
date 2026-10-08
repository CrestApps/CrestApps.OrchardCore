using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.Expressions;

namespace CrestApps.OrchardCore.Reports.Designer;

public sealed partial class ReportQueryEngine
{
    private ReportQueryResult BuildResult(
        ReportQueryPlan plan,
        List<object[]> rows,
        IReadOnlyList<ActiveFilter> filters,
        DateTime today,
        ExpressionContext expressionContext,
        ReportQueryLimits limits,
        List<string> warnings)
    {
        var columns = plan.Columns.Select(ToResultColumn).ToArray();
        var aggregated = plan.IsAggregated;
        var outputs = aggregated
            ? Group(plan, rows, Enumerable.Range(0, plan.Columns.Count).Where(index => !plan.Columns[index].IsMeasure).ToArray(), expressionContext)
            : rows.Select(row => new Output(Project(plan, row), [row])).ToList();

        outputs = ApplyResultFilters(outputs, filters, today);
        outputs = Sort(plan, outputs, aggregated);

        var limit = plan.Limit ?? int.MaxValue;
        var maxResultRows = Math.Max(1, limits.MaxResultRows);

        if (outputs.Count > limit)
        {
            outputs = outputs.Take(limit).ToList();
        }

        if (outputs.Count > maxResultRows)
        {
            warnings.Add(S["The result has more than {0} rows, so only the first {0} are shown.", maxResultRows]);
            outputs = outputs.Take(maxResultRows).ToList();
        }

        var kept = outputs;

        return new ReportQueryResult(
            columns,
            kept.Select(output => output.Values).ToArray(),
            aggregated,
            dimensionIndexes => Regroup(plan, kept, dimensionIndexes, aggregated, expressionContext));
    }

    private static ReportResultColumn ToResultColumn(PlannedColumn column)
    {
        return new ReportResultColumn
        {
            Id = column.Definition.Id,
            Field = column.Field.Key,
            Label = column.Label,
            DataType = column.DataType,
            IsMeasure = column.IsMeasure,
            Aggregate = column.Field.IsAggregate ? ReportAggregate.None : column.Definition.Aggregate,
            Transform = column.Definition.Transform,
            Format = column.Definition.Format,
            Hidden = column.Definition.Hidden,
        };
    }

    private static object[] Project(ReportQueryPlan plan, object[] row)
    {
        var values = new object[plan.Columns.Count];

        for (var index = 0; index < plan.Columns.Count; index++)
        {
            values[index] = ColumnValue(plan.Columns[index], row);
        }

        return values;
    }

    private static object ColumnValue(PlannedColumn column, object[] row)
    {
        return ReportTransforms.Apply(column.Definition.Transform, row[column.Field.Slot]);
    }

    private static List<Output> Group(
        ReportQueryPlan plan,
        IReadOnlyList<object[]> rows,
        int[] dimensionIndexes,
        ExpressionContext expressionContext)
    {
        var groups = new Dictionary<string, (object[] Dimensions, List<object[]> Rows)>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach (var row in rows)
        {
            var dimensions = new object[dimensionIndexes.Length];
            var keys = new string[dimensionIndexes.Length];

            for (var index = 0; index < dimensionIndexes.Length; index++)
            {
                dimensions[index] = ColumnValue(plan.Columns[dimensionIndexes[index]], row);
                keys[index] = ReportDataValues.ToKey(dimensions[index]);
            }

            var key = string.Join('\u001F', keys);

            if (!groups.TryGetValue(key, out var group))
            {
                group = (dimensions, []);
                groups[key] = group;
                order.Add(key);
            }

            group.Rows.Add(row);
        }

        if (dimensionIndexes.Length == 0 && groups.Count == 0)
        {
            groups[string.Empty] = ([], []);
            order.Add(string.Empty);
        }

        var outputs = new List<Output>(order.Count);

        foreach (var key in order)
        {
            var (dimensions, members) = groups[key];
            var values = new object[plan.Columns.Count];

            for (var index = 0; index < dimensionIndexes.Length; index++)
            {
                values[dimensionIndexes[index]] = dimensions[index];
            }

            for (var index = 0; index < plan.Columns.Count; index++)
            {
                if (plan.Columns[index].IsMeasure)
                {
                    values[index] = Measure(plan.Columns[index], members, expressionContext);
                }
            }

            outputs.Add(new Output(values, members));
        }

        return outputs;
    }

    private static object Measure(PlannedColumn column, List<object[]> rows, ExpressionContext expressionContext)
    {
        if (column.Field.IsAggregate)
        {
            expressionContext.Group = rows;
            expressionContext.Row = null;

            try
            {
                return ReportTransforms.Apply(column.Definition.Transform, SafeEvaluate(column.Field.Expression, expressionContext));
            }
            finally
            {
                expressionContext.Group = null;
            }
        }

        var values = new object[rows.Count];

        for (var index = 0; index < rows.Count; index++)
        {
            values[index] = ColumnValue(column, rows[index]);
        }

        return ReportAggregations.Compute(column.Definition.Aggregate, values);
    }

    private static List<Output> ApplyResultFilters(List<Output> outputs, IReadOnlyList<ActiveFilter> filters, DateTime today)
    {
        var predicates = filters
            .Where(filter => filter.Filter.Definition.Stage == ReportFilterStage.Result)
            .Select(filter => (filter.Filter.ColumnIndex, Predicate: ReportFilterPredicates.Build(filter.Filter.Definition.Operator, filter.Filter.DataType, filter.Values, today)))
            .Where(entry => entry.Predicate is not null)
            .ToArray();

        if (predicates.Length == 0)
        {
            return outputs;
        }

        return outputs
            .Where(output => predicates.All(entry => entry.Predicate(output.Values[entry.ColumnIndex])))
            .ToList();
    }

    private static List<Output> Sort(ReportQueryPlan plan, List<Output> outputs, bool aggregated)
    {
        var sorts = plan.Sorts.ToList();

        if (sorts.Count == 0 && aggregated)
        {
            sorts = Enumerable.Range(0, plan.Columns.Count)
                .Where(index => !plan.Columns[index].IsMeasure)
                .Select(index => (index, false))
                .ToList();
        }

        if (sorts.Count == 0)
        {
            return outputs;
        }

        var comparer = Comparer<Output>.Create((left, right) =>
        {
            foreach (var (columnIndex, descending) in sorts)
            {
                var comparison = ReportDataValues.Compare(left.Values[columnIndex], right.Values[columnIndex]);

                if (comparison != 0)
                {
                    return descending ? -comparison : comparison;
                }
            }

            return 0;
        });

        return outputs.Order(comparer).ToList();
    }

    private static ReportResultGroup[] Regroup(
        ReportQueryPlan plan,
        IReadOnlyList<Output> outputs,
        IReadOnlyList<int> dimensionIndexes,
        bool aggregated,
        ExpressionContext expressionContext)
    {
        var dimensions = dimensionIndexes
            .Where(index => index >= 0 && index < plan.Columns.Count && !plan.Columns[index].IsMeasure)
            .ToArray();

        if (aggregated)
        {
            var rows = outputs.SelectMany(output => output.Rows).ToList();

            return Group(plan, rows, dimensions, expressionContext)
                .Select(output => new ReportResultGroup(output.Values))
                .ToArray();
        }

        var groups = new Dictionary<string, object[]>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach (var output in outputs)
        {
            var key = string.Join('\u001F', dimensions.Select(index => ReportDataValues.ToKey(output.Values[index])));

            if (!groups.TryGetValue(key, out var values))
            {
                values = new object[plan.Columns.Count];

                foreach (var index in dimensions)
                {
                    values[index] = output.Values[index];
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

                var value = output.Values[index];

                values[index] = ReportDataValues.IsNumeric(plan.Columns[index].DataType)
                    ? ReportAggregations.Sum([values[index], value])
                    : values[index] ?? value;
            }
        }

        return order.Select(key => new ReportResultGroup(groups[key])).ToArray();
    }

    private sealed record Output(object[] Values, IReadOnlyList<object[]> Rows);
}
