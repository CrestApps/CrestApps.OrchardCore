using OrchardCore.DataSources;
using OrchardCore.DataSources.Operations;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Lets a data source group and aggregate a simple report itself (see <see cref="IDataAggregateSource"/>). The
/// source returns, per group, the row count and the count, sum, smallest, and largest value of each measured field;
/// the engine turns each group into one row that stands for its rows, holding these partial aggregates, and Orchard
/// Core's aggregator merges them wherever it would have aggregated rows, so sorting, result filters, totals, charts,
/// and pivots give the same result as reading every row.
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

    // The groups the data source computed, each as a row that stands for its rows, with how many after the fields of
    // the plan; or null when the source can't group the report exactly.
    private static async Task<List<object[]>> TryAggregateAsync(QueryRun run, CancellationToken cancellationToken)
    {
        var plan = run.Plan;
        var context = run.Context;
        var today = run.Today;

        if (plan.DataSets.Count != 1 ||
            plan.Joins.Count > 0 ||
            plan.RowCalculations.Count > 0 ||
            !plan.IsAggregated ||
            plan.DataSets[0].Source is not IDataAggregateSource source)
        {
            return null;
        }

        var dataSet = plan.DataSets[0];
        var query = new DataAggregateQuery
        {
            DataSet = dataSet.Reference.DataSet,
            Context = context.DataSourceContext,
            MaxGroups = Math.Max(1, context.Limits.MaxRowsPerDataSet),
        };

        // Every row filter must be on a field of the data set and expressible exactly; filters that list options need
        // the rows themselves.
        foreach (var filter in run.Filters.Where(filter => filter.Filter.Definition.Stage == ReportFilterStage.Rows))
        {
            var field = filter.Filter.Field;

            if (field is null || field.Kind != PlannedFieldKind.DataSetField || (filter.Filter.Definition.Exposed && UsesOptions(filter.Filter.Definition, field.DataType)))
            {
                return null;
            }

            var conditions = DataFilterPredicates.BuildExactConditions(field.FieldName, filter.Filter.Definition.Operator, field.DataType, filter.Values, context.ToUtc, today);

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
        var measured = new Dictionary<PlannedField, HashSet<DataAggregate>>();

        foreach (var column in plan.Columns.Where(column => column.IsMeasure))
        {
            if (column.Field.Key == ReportQueryPlanner.RowCountField)
            {
                continue;
            }

            if (column.Field.Kind != PlannedFieldKind.DataSetField ||
                column.Definition.Transform != DataFieldTransform.None ||
                dimensions.ContainsKey(column.Field))
            {
                return null;
            }

            DataAggregate[] kinds = column.Definition.Aggregate switch
            {
                DataAggregate.Count => [DataAggregate.Count],
                DataAggregate.Sum => [DataAggregate.Count, DataAggregate.Sum],
                DataAggregate.Average => [DataAggregate.Count, DataAggregate.Sum],
                DataAggregate.Min => [DataAggregate.Count, DataAggregate.Min],
                DataAggregate.Max => [DataAggregate.Count, DataAggregate.Max],
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
                query.Groups.Add(new DataAggregateGroup { Field = field.FieldName });

                continue;
            }

            var boundaries = await BucketBoundariesAsync(source, query, field, size, context, cancellationToken);

            if (boundaries is null)
            {
                return null;
            }

            query.Groups.Add(new DataAggregateGroup { Field = field.FieldName, Boundaries = boundaries.Utc });
            bucketStarts[field] = boundaries.Local;
        }

        query.Measures.Add(new DataAggregateMeasure { Function = DataAggregate.Count });

        var measures = new List<(PlannedField Field, DataAggregate Kind)>();

        foreach (var (field, kinds) in measured)
        {
            foreach (var kind in kinds.Order())
            {
                query.Measures.Add(new DataAggregateMeasure { Field = field.FieldName, Function = kind });
                measures.Add((field, kind));
            }
        }

        var table = await source.AggregateAsync(query, cancellationToken);

        if (table is null || table.Any(row => row is null || row.Length != query.Groups.Count + query.Measures.Count))
        {
            return null;
        }

        var rows = new List<object[]>(table.Count);
        long total = 0;

        foreach (var sourceRow in table)
        {
            if (DataValues.Coerce(sourceRow[query.Groups.Count], DataFieldType.Integer) is not long weight || weight <= 0)
            {
                continue;
            }

            var row = new object[plan.Width + 1];

            for (var index = 0; index < groupFields.Length; index++)
            {
                var field = groupFields[index];
                var value = sourceRow[index];

                row[field.Slot] = bucketStarts.TryGetValue(field, out var starts)
                    ? (DataValues.Coerce(value, DataFieldType.Integer) is long bucket && bucket >= 0 && bucket < starts.Count ? starts[(int)bucket] : null)
                    : Normalize(value, field.DataType, context.ToLocal);
            }

            var parts = new Dictionary<PlannedField, DataPartialAggregate>();

            for (var index = 0; index < measures.Count; index++)
            {
                var (field, kind) = measures[index];
                var value = sourceRow[query.Groups.Count + 1 + index];

                if (!parts.TryGetValue(field, out var part))
                {
                    parts[field] = part = new DataPartialAggregate();
                    row[field.Slot] = part;
                }

                switch (kind)
                {
                    case DataAggregate.Count:
                        part.Count = DataValues.Coerce(value, DataFieldType.Integer) is long count ? count : 0;
                        break;

                    case DataAggregate.Sum:
                        part.Sum = DataValues.Coerce(value, field.DataType == DataFieldType.Integer ? DataFieldType.Integer : DataFieldType.Decimal);
                        break;

                    case DataAggregate.Min:
                        part.Min = Normalize(value, field.DataType, context.ToLocal);
                        break;

                    case DataAggregate.Max:
                        part.Max = Normalize(value, field.DataType, context.ToLocal);
                        break;
                }
            }

            row[plan.Width] = weight;
            total += weight;
            rows.Add(row);
        }

        run.SourceRows = total;
        run.FilteredRows = total;

        return rows;
    }

    // How a dimension groups a field: by its value (no transform), or by local hour, day, or month for a date transform;
    // false for transforms the source cannot group exactly (text and number transforms change the value).
    private static bool TryGetBucketSize(DataFieldTransform transform, DataFieldType dataType, out BucketSize? size)
    {
        size = transform switch
        {
            DataFieldTransform.Hour => BucketSize.Hour,
            DataFieldTransform.Day or DataFieldTransform.DayOfWeek or DataFieldTransform.Week => BucketSize.Day,
            DataFieldTransform.Month or DataFieldTransform.MonthOfYear or DataFieldTransform.Quarter or DataFieldTransform.Year => BucketSize.Month,
            _ => null,
        };

        if (transform == DataFieldTransform.None)
        {
            return true;
        }

        return size is not null && DataValues.IsTemporal(dataType) && (size != BucketSize.Hour || dataType == DataFieldType.DateTime);
    }

    // The bucket boundaries of a date dimension over the range the conditions allow, or the range of the data when they
    // leave it open: whole local hours, days, or months, so every row of a bucket gets the same transformed value. The
    // local starts give each bucket its value; the UTC boundaries are what the source compares. Null when there would
    // be too many buckets.
    private static async Task<BucketBoundaries> BucketBoundariesAsync(
        IDataAggregateSource source,
        DataAggregateQuery query,
        PlannedField field,
        BucketSize size,
        ReportQueryExecutionContext context,
        CancellationToken cancellationToken)
    {
        var isDateTime = field.DataType == DataFieldType.DateTime;
        DateTime? lower = null;
        DateTime? upper = null;

        foreach (var condition in query.Conditions.Where(condition => condition.Field == field.FieldName && condition.Values.Count > 0))
        {
            if (condition.Values[0] is not DateTime value)
            {
                continue;
            }

            if (condition.Operator is DataFilterOperator.GreaterThan or DataFilterOperator.GreaterThanOrEqual)
            {
                lower = lower is null || value > lower ? value : lower;
            }
            else if (condition.Operator is DataFilterOperator.LessThan or DataFilterOperator.LessThanOrEqual)
            {
                upper = upper is null || value < upper ? value : upper;
            }
        }

        if (lower is null || upper is null)
        {
            var range = new DataAggregateQuery
            {
                DataSet = query.DataSet,
                Context = query.Context,
                MaxGroups = 1,
            };

            foreach (var condition in query.Conditions)
            {
                range.Conditions.Add(condition);
            }

            range.Measures.Add(new DataAggregateMeasure { Field = field.FieldName, Function = DataAggregate.Min });
            range.Measures.Add(new DataAggregateMeasure { Field = field.FieldName, Function = DataAggregate.Max });

            var table = await source.AggregateAsync(range, cancellationToken);

            if (table is null)
            {
                return null;
            }

            var row = table.Count > 0 ? table[0] : null;

            lower ??= DataValues.Coerce(row?.ElementAtOrDefault(0), DataFieldType.DateTime) as DateTime?;
            upper ??= DataValues.Coerce(row?.ElementAtOrDefault(1), DataFieldType.DateTime) as DateTime?;

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
