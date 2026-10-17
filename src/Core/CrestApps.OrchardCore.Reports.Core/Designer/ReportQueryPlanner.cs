using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.Expressions;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Checks a designed query against the live data source schemas and compiles it into a <see cref="ReportQueryPlan"/>.
/// Every problem is collected rather than stopping at the first, so the designer can show them all at once.
/// </summary>
public sealed partial class ReportQueryPlanner
{
    /// <summary>
    /// The key of the built-in measure that counts rows.
    /// </summary>
    public const string RowCountField = "$count";

    private readonly IReportDataSourceManager _dataSourceManager;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportQueryPlanner"/> class.
    /// </summary>
    /// <param name="dataSourceManager">The data source manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReportQueryPlanner(
        IReportDataSourceManager dataSourceManager,
        IStringLocalizer<ReportQueryPlanner> stringLocalizer)
    {
        _dataSourceManager = dataSourceManager;
        S = stringLocalizer;
    }

    /// <summary>
    /// Plans a query.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="context">The caller context used to read the data set schemas.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The plan; check <see cref="ReportQueryPlan.Errors"/> before running it.</returns>
    public async Task<ReportQueryPlan> PlanAsync(
        ReportQueryDefinition query,
        ReportDataSourceContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(context);

        var plan = new ReportQueryPlan();

        await PlanDataSetsAsync(query, context, plan, cancellationToken);

        if (!plan.IsValid)
        {
            return plan;
        }

        var scope = new PlanScope(plan);

        AddRowCount(plan);
        PlanJoins(query, plan, scope);
        PlanCalculatedFields(query, plan, scope);
        PlanColumns(query, plan, scope);
        PlanFilters(query, plan, scope);
        PlanSorts(query, plan);

        if (query.Limit.HasValue && query.Limit.Value < 1)
        {
            plan.Errors.Add(S["The row limit must be at least 1."]);
        }

        plan.Limit = query.Limit;
        plan.Width = scope.Width;

        return plan;
    }

    private async Task PlanDataSetsAsync(
        ReportQueryDefinition query,
        ReportDataSourceContext context,
        ReportQueryPlan plan,
        CancellationToken cancellationToken)
    {
        if (query.DataSets is null || query.DataSets.Count == 0)
        {
            plan.Errors.Add(S["Add at least one data set."]);

            return;
        }

        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var reference in query.DataSets)
        {
            if (reference is null || string.IsNullOrEmpty(reference.Alias) || !NamePattern().IsMatch(reference.Alias))
            {
                plan.Errors.Add(S["The data set alias '{0}' is invalid. Use a letter followed by letters, digits, or underscores.", reference?.Alias]);

                continue;
            }

            if (!aliases.Add(reference.Alias))
            {
                plan.Errors.Add(S["The data set alias '{0}' is used more than once.", reference.Alias]);

                continue;
            }

            var source = _dataSourceManager.FindDataSource(reference.Source);

            if (source is null)
            {
                plan.Errors.Add(S["The data source '{0}' is not available. Enable the feature that provides it.", reference.Source]);

                continue;
            }

            var schema = await source.GetSchemaAsync(reference.DataSet, context, cancellationToken);

            if (schema is null)
            {
                plan.Errors.Add(S["The data set '{0}' of '{1}' does not exist or is not available to you.", reference.DisplayName ?? reference.DataSet, source.DisplayName]);

                continue;
            }

            var dataSet = new PlannedDataSet
            {
                Reference = reference,
                Source = source,
                Schema = schema,
            };

            plan.DataSets.Add(dataSet);

            foreach (var field in schema.Fields)
            {
                var key = reference.Alias + "." + field.Name;

                plan.Fields[key] = new PlannedField
                {
                    Key = key,
                    Label = field.DisplayName ?? field.Name,
                    DataType = field.DataType,
                    Kind = PlannedFieldKind.DataSetField,
                    Alias = reference.Alias,
                    FieldName = field.Name,
                };
            }
        }
    }

    private void AddRowCount(ReportQueryPlan plan)
    {
        plan.Fields[RowCountField] = new PlannedField
        {
            Key = RowCountField,
            Label = S["Number of rows"],
            DataType = ReportDataType.Integer,
            Kind = PlannedFieldKind.AggregateCalculation,
            Expression = ExpressionCompiler.Compile("COUNT()", new PlanScope(plan), S),
        };
    }

    private void PlanJoins(ReportQueryDefinition query, ReportQueryPlan plan, PlanScope scope)
    {
        var joins = query.Joins ?? [];

        foreach (var join in joins)
        {
            if (join is null || !plan.DataSets.Any(dataSet => string.Equals(dataSet.Reference.Alias, join.Alias, StringComparison.OrdinalIgnoreCase)))
            {
                plan.Errors.Add(S["A join refers to the unknown data set '{0}'.", join?.Alias]);
            }
        }

        for (var index = 1; index < plan.DataSets.Count; index++)
        {
            var dataSet = plan.DataSets[index];
            var alias = dataSet.Reference.Alias;
            var matches = joins
                .Where(join => join is not null && string.Equals(join.Alias, alias, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count != 1)
            {
                plan.Errors.Add(S["Join the data set '{0}' to the data sets before it.", dataSet.Reference.DisplayName ?? alias]);

                continue;
            }

            var join = matches[0];
            var planned = new PlannedJoin
            {
                DataSet = dataSet,
                Type = join.Type,
            };

            if (join.Conditions is null || join.Conditions.Count == 0)
            {
                plan.Errors.Add(S["The join of '{0}' needs at least one pair of matching fields.", dataSet.Reference.DisplayName ?? alias]);

                continue;
            }

            var earlierAliases = plan.DataSets
                .Take(index)
                .Select(earlier => earlier.Reference.Alias)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var condition in join.Conditions)
            {
                var left = scope.UseDataSetField(condition?.LeftField);
                var right = scope.UseDataSetField(condition?.RightField);

                if (left is null || !earlierAliases.Contains(left.Alias))
                {
                    plan.Errors.Add(S["The join of '{0}' must match a field of a data set listed before it, but uses '{1}'.", dataSet.Reference.DisplayName ?? alias, condition?.LeftField]);

                    continue;
                }

                if (right is null || !string.Equals(right.Alias, alias, StringComparison.Ordinal))
                {
                    plan.Errors.Add(S["The join of '{0}' must match a field of '{0}', but uses '{1}'.", dataSet.Reference.DisplayName ?? alias, condition?.RightField]);

                    continue;
                }

                var compareAsText = left.DataType != right.DataType &&
                    !(ReportDataValues.IsNumeric(left.DataType) && ReportDataValues.IsNumeric(right.DataType)) &&
                    !(ReportDataValues.IsTemporal(left.DataType) && ReportDataValues.IsTemporal(right.DataType));

                planned.Pairs.Add((left.Slot, right.Slot, compareAsText));
            }

            plan.Joins.Add(planned);

            if (join.Type is ReportJoinType.Left or ReportJoinType.Full)
            {
                dataSet.IsNullSupplying = true;
            }

            if (join.Type is ReportJoinType.Right or ReportJoinType.Full)
            {
                foreach (var earlier in plan.DataSets.Take(index))
                {
                    earlier.IsNullSupplying = true;
                }
            }
        }
    }

    private void PlanCalculatedFields(ReportQueryDefinition query, ReportQueryPlan plan, PlanScope scope)
    {
        var aliases = plan.DataSets
            .Select(dataSet => dataSet.Reference.Alias)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var calculated in query.CalculatedFields ?? [])
        {
            if (calculated is null || string.IsNullOrEmpty(calculated.Name) || !NamePattern().IsMatch(calculated.Name))
            {
                plan.Errors.Add(S["The calculated field name '{0}' is invalid. Use a letter followed by letters, digits, or underscores.", calculated?.Name]);

                continue;
            }

            if (aliases.Contains(calculated.Name) || plan.Fields.ContainsKey(calculated.Name))
            {
                plan.Errors.Add(S["The name '{0}' is used by more than one calculated field or data set.", calculated.Name]);

                continue;
            }

            CompiledExpression compiled;

            try
            {
                compiled = ExpressionCompiler.Compile(calculated.Expression, scope, S);
            }
            catch (ExpressionException exception)
            {
                plan.Errors.Add(S["The calculated field '{0}' is invalid: {1}", calculated.Label ?? calculated.Name, exception.Message]);

                continue;
            }

            var field = new PlannedField
            {
                Key = calculated.Name,
                Label = string.IsNullOrWhiteSpace(calculated.Label) ? calculated.Name : calculated.Label,
                DataType = compiled.DataType,
                Kind = compiled.IsAggregate ? PlannedFieldKind.AggregateCalculation : PlannedFieldKind.RowCalculation,
                Expression = compiled,
            };

            plan.Fields[field.Key] = field;

            if (!compiled.IsAggregate)
            {
                field.Slot = scope.AllocateSlot();
                plan.RowCalculations.Add(field);
            }
        }
    }

    private void PlanColumns(ReportQueryDefinition query, ReportQueryPlan plan, PlanScope scope)
    {
        if (query.Columns is null || query.Columns.Count == 0)
        {
            plan.Errors.Add(S["Add at least one column."]);

            return;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var column in query.Columns)
        {
            if (column is null || string.IsNullOrEmpty(column.Id) || !ids.Add(column.Id))
            {
                plan.Errors.Add(S["Every column needs a unique identifier."]);

                continue;
            }

            var field = scope.Use(column.Field);

            if (field is null)
            {
                plan.Errors.Add(S["The column '{0}' shows the unknown field '{1}'.", column.Label ?? column.Id, column.Field]);

                continue;
            }

            var planned = new PlannedColumn
            {
                Definition = column,
                Field = field,
            };

            if (!ReportTransforms.Supports(column.Transform, field.DataType))
            {
                plan.Errors.Add(S["The column '{0}' cannot apply the transform {1} to a {2} field.", column.Label ?? field.Label, column.Transform, field.DataType]);

                continue;
            }

            var transformedType = ReportTransforms.GetResultType(column.Transform, field.DataType);

            if (field.IsAggregate)
            {
                if (column.Aggregate != ReportAggregate.None)
                {
                    plan.Errors.Add(S["The column '{0}' shows a field that is already aggregated, so it cannot apply another aggregate.", column.Label ?? field.Label]);

                    continue;
                }

                planned.IsMeasure = true;
                planned.DataType = transformedType;
            }
            else if (column.Aggregate != ReportAggregate.None)
            {
                if (!ReportAggregations.Supports(column.Aggregate, transformedType))
                {
                    plan.Errors.Add(S["The column '{0}' cannot apply {1} to a {2} field.", column.Label ?? field.Label, column.Aggregate, transformedType]);

                    continue;
                }

                planned.IsMeasure = true;
                planned.DataType = ReportAggregations.GetResultType(column.Aggregate, transformedType);
            }
            else
            {
                planned.DataType = transformedType;
            }

            planned.Label = string.IsNullOrWhiteSpace(column.Label)
                ? DefaultLabel(column, field)
                : column.Label.Trim();

            plan.Columns.Add(planned);
        }
    }

    private void PlanFilters(ReportQueryDefinition query, ReportQueryPlan plan, PlanScope scope)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var filter in query.Filters ?? [])
        {
            if (filter is null || string.IsNullOrEmpty(filter.Id) || !ids.Add(filter.Id))
            {
                plan.Errors.Add(S["Every filter needs a unique identifier."]);

                continue;
            }

            var planned = new PlannedFilter
            {
                Definition = filter,
            };

            if (filter.Stage == ReportFilterStage.Result)
            {
                planned.ColumnIndex = IndexOfColumn(plan, filter.Field);

                if (planned.ColumnIndex < 0)
                {
                    plan.Errors.Add(S["The filter '{0}' refers to the unknown column '{1}'.", filter.Label ?? filter.Id, filter.Field]);

                    continue;
                }

                planned.DataType = plan.Columns[planned.ColumnIndex].DataType;
            }
            else
            {
                var field = scope.Use(filter.Field);

                if (field is null)
                {
                    plan.Errors.Add(S["The filter '{0}' refers to the unknown field '{1}'.", filter.Label ?? filter.Id, filter.Field]);

                    continue;
                }

                if (field.IsAggregate)
                {
                    plan.Errors.Add(S["The filter '{0}' compares an aggregated field, so it must filter the result instead of the rows.", filter.Label ?? field.Label]);

                    continue;
                }

                planned.Field = field;
                planned.DataType = field.DataType;
            }

            if (filter.Operator is ReportFilterOperator.InLastDays or ReportFilterOperator.InNextDays && !ReportDataValues.IsTemporal(planned.DataType))
            {
                plan.Errors.Add(S["The filter '{0}' compares days, so it needs a date field.", filter.Label ?? filter.Id]);

                continue;
            }

            plan.Filters.Add(planned);
        }
    }

    private void PlanSorts(ReportQueryDefinition query, ReportQueryPlan plan)
    {
        foreach (var sort in query.Sorts ?? [])
        {
            var index = IndexOfColumn(plan, sort?.ColumnId);

            if (index < 0)
            {
                plan.Errors.Add(S["The sort refers to the unknown column '{0}'.", sort?.ColumnId]);

                continue;
            }

            plan.Sorts.Add((index, sort.Descending));
        }
    }

    private string DefaultLabel(ReportColumnDefinition column, PlannedField field)
    {
        var label = column.Transform == ReportFieldTransform.None
            ? field.Label
            : S["{0} ({1})", field.Label, column.Transform].Value;

        return column.Aggregate switch
        {
            ReportAggregate.Count => S["Count of {0}", label],
            ReportAggregate.CountDistinct => S["Distinct count of {0}", label],
            ReportAggregate.Sum => S["Sum of {0}", label],
            ReportAggregate.Average => S["Average {0}", label],
            ReportAggregate.Min => S["Minimum {0}", label],
            ReportAggregate.Max => S["Maximum {0}", label],
            ReportAggregate.Median => S["Median {0}", label],
            _ => label,
        };
    }

    private static int IndexOfColumn(ReportQueryPlan plan, string columnId)
    {
        for (var index = 0; index < plan.Columns.Count; index++)
        {
            if (string.Equals(plan.Columns[index].Definition.Id, columnId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*$")]
    private static partial Regex NamePattern();

    private sealed class PlanScope : IExpressionScope
    {
        private readonly ReportQueryPlan _plan;

        public PlanScope(ReportQueryPlan plan)
        {
            _plan = plan;
        }

        public int Width { get; private set; }

        public int AllocateSlot()
        {
            return Width++;
        }

        public PlannedField Use(string key)
        {
            if (string.IsNullOrEmpty(key) || !_plan.Fields.TryGetValue(key, out var field))
            {
                return null;
            }

            if (field.Kind == PlannedFieldKind.DataSetField && field.Slot < 0)
            {
                field.Slot = AllocateSlot();

                var dataSet = _plan.DataSets.First(candidate => string.Equals(candidate.Reference.Alias, field.Alias, StringComparison.Ordinal));
                dataSet.UsedFields[field.FieldName] = field;
            }

            return field;
        }

        public PlannedField UseDataSetField(string key)
        {
            var field = string.IsNullOrEmpty(key) || !_plan.Fields.TryGetValue(key, out var candidate) || candidate.Kind != PlannedFieldKind.DataSetField
                ? null
                : candidate;

            return field is null ? null : Use(key);
        }

        public bool TryResolve(string key, out ExpressionFieldBinding binding)
        {
            var field = Use(key);

            if (field is null)
            {
                binding = null;

                return false;
            }

            binding = new ExpressionFieldBinding
            {
                Key = field.Key,
                DataType = field.DataType,
                Index = field.Slot,
                Aggregate = field.IsAggregate ? field.Expression : null,
                References = field.Expression?.References,
            };

            return true;
        }
    }
}
