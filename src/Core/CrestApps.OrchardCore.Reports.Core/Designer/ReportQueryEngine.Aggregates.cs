using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.Expressions;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Lets a data source group and aggregate a simple report itself (see <see cref="IReportAggregateDataSource"/>). The
/// source returns, per group, the row count and the count, sum, smallest, and largest value of each measured field;
/// the engine turns each group into one row that stands for its rows, and merges these partial aggregates wherever it
/// would have aggregated rows, so sorting, result filters, totals, charts, and pivots give the same result as reading
/// every row.
/// </summary>
public sealed partial class ReportQueryEngine
{
    // The most date buckets one dimension asks a source for.
    private const int MaxBuckets = 1_000;

    private enum BucketSize
    {
        Hour,
        Day,
        Month,
    }

    private async Task<ReportQueryResult> TryAggregateAsync(
        ReportQueryPlan plan,
        IReadOnlyList<ActiveFilter> filters,
        ReportQueryExecutionContext context,
        DateTime today,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        if (plan.DataSets.Count != 1 ||
            plan.Joins.Count > 0 ||
            plan.RowCalculations.Count > 0 ||
            !plan.IsAggregated ||
            plan.DataSets[0].Source is not IReportAggregateDataSource source)
        {
            return null;
        }

        var dataSet = plan.DataSets[0];
        var query = new ReportAggregateQuery
        {
            DataSet = dataSet.Reference.DataSet,
            Context = context.DataSourceContext,
            MaxGroups = Math.Max(1, context.Limits.MaxRowsPerDataSet),
        };

        // Every row filter must be on a field of the data set and expressible exactly; filters that list options need
        // the rows themselves.
        foreach (var filter in filters.Where(filter => filter.Filter.Definition.Stage == ReportFilterStage.Rows))
        {
            var field = filter.Filter.Field;

            if (field is null || field.Kind != PlannedFieldKind.DataSetField || (filter.Filter.Definition.Exposed && UsesOptions(filter.Filter.Definition, field.DataType)))
            {
                return null;
            }

            var conditions = ReportFilterPredicates.BuildExactConditions(field.FieldName, filter.Filter.Definition.Operator, field.DataType, filter.Values, context.ToUtc, today);

            if (conditions is null)
            {
                return null;
            }

            foreach (var condition in conditions)
            {
                query.Conditions.Add(condition);
            }
        }

        // Dimensions: data set fields, by value or, for dates, by local hour, day, or month.
        var dimensions = new Dictionary<PlannedField, BucketSize?>();

        foreach (var column in plan.Columns.Where(column => !column.IsMeasure))
        {
            if (column.Field.Kind != PlannedFieldKind.DataSetField)
            {
                return null;
            }

            if (!TryGetBucketSize(column.Definition.Transform, column.Field.DataType, out var size))
            {
                return null;
            }

            // A field grouped several ways is grouped by the finest of them; grouping by the value itself is finest.
            if (!dimensions.TryGetValue(column.Field, out var current))
            {
                dimensions[column.Field] = size;
            }
            else if (current is not null && (size is null || size < current))
            {
                dimensions[column.Field] = size;
            }
        }

        // Measures: the row count, and counts, sums, smallest and largest values of data set fields.
        var measured = new Dictionary<PlannedField, HashSet<ReportAggregateKind>>();

        foreach (var column in plan.Columns.Where(column => column.IsMeasure))
        {
            if (column.Field.Key == ReportQueryPlanner.RowCountField)
            {
                continue;
            }

            if (column.Field.Kind != PlannedFieldKind.DataSetField ||
                column.Definition.Transform != ReportFieldTransform.None ||
                dimensions.ContainsKey(column.Field))
            {
                return null;
            }

            ReportAggregateKind[] kinds = column.Definition.Aggregate switch
            {
                ReportAggregate.Count => [ReportAggregateKind.Count],
                ReportAggregate.Sum => [ReportAggregateKind.Count, ReportAggregateKind.Sum],
                ReportAggregate.Average => [ReportAggregateKind.Count, ReportAggregateKind.Sum],
                ReportAggregate.Min => [ReportAggregateKind.Count, ReportAggregateKind.Min],
                ReportAggregate.Max => [ReportAggregateKind.Count, ReportAggregateKind.Max],
                _ => null,
            };

            if (kinds is null)
            {
                return null;
            }

            if (!measured.TryGetValue(column.Field, out var set))
            {
                measured[column.Field] = set = [];
            }

            set.UnionWith(kinds);
        }

        var groupFields = dimensions.Keys.ToArray();
        var bucketStarts = new Dictionary<PlannedField, IReadOnlyList<DateTime>>();

        foreach (var field in groupFields)
        {
            if (dimensions[field] is not BucketSize size)
            {
                query.Groups.Add(new ReportAggregateGroup { Field = field.FieldName });

                continue;
            }

            var boundaries = await BucketBoundariesAsync(source, query, field, size, context, cancellationToken);

            if (boundaries is null)
            {
                return null;
            }

            query.Groups.Add(new ReportAggregateGroup { Field = field.FieldName, Boundaries = boundaries.Utc });
            bucketStarts[field] = boundaries.Local;
        }

        query.Measures.Add(new ReportAggregateMeasure { Kind = ReportAggregateKind.Count });

        var measures = new List<(PlannedField Field, ReportAggregateKind Kind)>();

        foreach (var (field, kinds) in measured)
        {
            foreach (var kind in kinds.Order())
            {
                query.Measures.Add(new ReportAggregateMeasure { Field = field.FieldName, Kind = kind });
                measures.Add((field, kind));
            }
        }

        var table = await source.AggregateAsync(query, cancellationToken);

        if (table?.Rows is null || table.Rows.Any(row => row is null || row.Length != query.Groups.Count + query.Measures.Count))
        {
            return null;
        }

        var rows = new List<object[]>(table.Rows.Count);
        long total = 0;

        foreach (var sourceRow in table.Rows)
        {
            if (ReportDataValues.Coerce(sourceRow[query.Groups.Count], ReportDataType.Integer) is not long weight || weight <= 0)
            {
                continue;
            }

            var row = new object[plan.Width + 1];

            for (var index = 0; index < groupFields.Length; index++)
            {
                var field = groupFields[index];
                var value = sourceRow[index];

                row[field.Slot] = bucketStarts.TryGetValue(field, out var starts)
                    ? (ReportDataValues.Coerce(value, ReportDataType.Integer) is long bucket && bucket >= 0 && bucket < starts.Count ? starts[(int)bucket] : null)
                    : Normalize(value, field.DataType, context.ToLocal);
            }

            var parts = new Dictionary<PlannedField, ReportPartialAggregate>();

            for (var index = 0; index < measures.Count; index++)
            {
                var (field, kind) = measures[index];
                var value = sourceRow[query.Groups.Count + 1 + index];

                if (!parts.TryGetValue(field, out var part))
                {
                    parts[field] = part = new ReportPartialAggregate();
                    row[field.Slot] = part;
                }

                switch (kind)
                {
                    case ReportAggregateKind.Count:
                        part.Count = ReportDataValues.Coerce(value, ReportDataType.Integer) is long count ? count : 0;
                        break;

                    case ReportAggregateKind.Sum:
                        part.Sum = ReportDataValues.Coerce(value, field.DataType == ReportDataType.Integer ? ReportDataType.Integer : ReportDataType.Decimal);
                        break;

                    case ReportAggregateKind.Min:
                        part.Min = Normalize(value, field.DataType, context.ToLocal);
                        break;

                    case ReportAggregateKind.Max:
                        part.Max = Normalize(value, field.DataType, context.ToLocal);
                        break;
                }
            }

            row[plan.Width] = weight;
            total += weight;
            rows.Add(row);
        }

        var expressionContext = new ExpressionContext
        {
            Now = today,
            WeightSlot = plan.Width,
        };

        var result = BuildResult(plan, rows, filters, today, expressionContext, context.Limits, warnings);

        result.SourceRowCount = (int)Math.Min(int.MaxValue, total);
        result.FilteredRowCount = result.SourceRowCount;
        result.GroupedBySource = true;

        foreach (var warning in warnings.Distinct(StringComparer.Ordinal))
        {
            result.Warnings.Add(warning);
        }

        return result;
    }

    // How a dimension groups a field: by its value (no transform), or by local hour, day, or month for a date transform;
    // false for transforms the source cannot group exactly (text and number transforms change the value).
    private static bool TryGetBucketSize(ReportFieldTransform transform, ReportDataType dataType, out BucketSize? size)
    {
        size = transform switch
        {
            ReportFieldTransform.Hour => BucketSize.Hour,
            ReportFieldTransform.Day or ReportFieldTransform.DayOfWeek or ReportFieldTransform.Week => BucketSize.Day,
            ReportFieldTransform.Month or ReportFieldTransform.MonthOfYear or ReportFieldTransform.Quarter or ReportFieldTransform.Year => BucketSize.Month,
            _ => null,
        };

        if (transform == ReportFieldTransform.None)
        {
            return true;
        }

        return size is not null && ReportDataValues.IsTemporal(dataType) && (size != BucketSize.Hour || dataType == ReportDataType.DateTime);
    }

    // The bucket boundaries of a date dimension over the range the conditions allow, or the range of the data when they
    // leave it open: whole local hours, days, or months, so every row of a bucket gets the same transformed value. The
    // local starts give each bucket its value; the UTC boundaries are what the source compares. Null when there would
    // be too many buckets.
    private static async Task<BucketBoundaries> BucketBoundariesAsync(
        IReportAggregateDataSource source,
        ReportAggregateQuery query,
        PlannedField field,
        BucketSize size,
        ReportQueryExecutionContext context,
        CancellationToken cancellationToken)
    {
        var isDateTime = field.DataType == ReportDataType.DateTime;
        DateTime? lower = null;
        DateTime? upper = null;

        foreach (var condition in query.Conditions.Where(condition => condition.Field == field.FieldName && condition.Values.Count > 0))
        {
            if (condition.Values[0] is not DateTime value)
            {
                continue;
            }

            if (condition.Operator is ReportFilterOperator.GreaterThan or ReportFilterOperator.GreaterThanOrEqual)
            {
                lower = lower is null || value > lower ? value : lower;
            }
            else if (condition.Operator is ReportFilterOperator.LessThan or ReportFilterOperator.LessThanOrEqual)
            {
                upper = upper is null || value < upper ? value : upper;
            }
        }

        if (lower is null || upper is null)
        {
            var range = new ReportAggregateQuery
            {
                DataSet = query.DataSet,
                Context = query.Context,
                MaxGroups = 1,
            };

            foreach (var condition in query.Conditions)
            {
                range.Conditions.Add(condition);
            }

            range.Measures.Add(new ReportAggregateMeasure { Field = field.FieldName, Kind = ReportAggregateKind.Min });
            range.Measures.Add(new ReportAggregateMeasure { Field = field.FieldName, Kind = ReportAggregateKind.Max });

            var table = await source.AggregateAsync(range, cancellationToken);

            if (table?.Rows is null)
            {
                return null;
            }

            var row = table.Rows.FirstOrDefault();

            lower ??= ReportDataValues.Coerce(row?.ElementAtOrDefault(0), ReportDataType.DateTime) as DateTime?;
            upper ??= ReportDataValues.Coerce(row?.ElementAtOrDefault(1), ReportDataType.DateTime) as DateTime?;

            // No dated rows: every row falls in the group without a date.
            if (lower is null || upper is null)
            {
                return new BucketBoundaries([], []);
            }
        }

        var localLower = isDateTime ? context.ToLocal(DateTime.SpecifyKind(lower.Value, DateTimeKind.Utc)) : lower.Value;
        var localUpper = isDateTime ? context.ToLocal(DateTime.SpecifyKind(upper.Value, DateTimeKind.Utc)) : upper.Value;
        var utc = new List<DateTime>();
        var local = new List<DateTime>();

        for (var start = Floor(localLower, size); ; start = Next(start, size))
        {
            var boundary = isDateTime ? DateTime.SpecifyKind(context.ToUtc(start), DateTimeKind.Utc) : start;

            // Clocks that go back repeat a local hour; keep the boundaries strictly ascending.
            if (utc.Count == 0 || boundary > utc[^1])
            {
                utc.Add(boundary);
                local.Add(start);
            }

            if (start > localUpper)
            {
                break;
            }

            if (utc.Count > MaxBuckets + 1)
            {
                return null;
            }
        }

        // The last boundary only closes the last bucket.
        local.RemoveAt(local.Count - 1);

        return new BucketBoundaries(utc, local);
    }

    private static DateTime Floor(DateTime value, BucketSize size)
    {
        return size switch
        {
            BucketSize.Hour => new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0, value.Kind),
            BucketSize.Day => value.Date,
            _ => new DateTime(value.Year, value.Month, 1, 0, 0, 0, value.Kind),
        };
    }

    private static DateTime Next(DateTime value, BucketSize size)
    {
        return size switch
        {
            BucketSize.Hour => value.AddHours(1),
            BucketSize.Day => value.AddDays(1),
            _ => value.AddMonths(1),
        };
    }

    private sealed record BucketBoundaries(IList<DateTime> Utc, IReadOnlyList<DateTime> Local);
}
