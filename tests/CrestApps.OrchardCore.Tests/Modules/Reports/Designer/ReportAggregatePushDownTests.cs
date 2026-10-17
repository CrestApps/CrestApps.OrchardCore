using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer;
using Microsoft.Extensions.Localization;
using static CrestApps.OrchardCore.Tests.Modules.Reports.Designer.ReportDesignerTestServices;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer;

/// <summary>
/// When a data source groups and aggregates a report itself, the result, its totals, and its regrouped charts must be
/// exactly what reading every row gives. Each report runs twice over the same rows: once against a source that groups
/// (by the contract, like a database would) and once against one that only returns rows.
/// </summary>
public sealed class ReportAggregatePushDownTests
{
    // The tenant is five hours behind UTC, so local days and months differ from UTC ones.
    private static readonly Func<DateTime, DateTime> _toLocal = utc => DateTime.SpecifyKind(utc.AddHours(-5), DateTimeKind.Unspecified);
    private static readonly Func<DateTime, DateTime> _toUtc = local => DateTime.SpecifyKind(local.AddHours(5), DateTimeKind.Utc);

    public static TheoryData<string> Reports => new(Queries.Keys);

    private static Dictionary<string, Func<ReportQueryDefinition>> Queries { get; } = new(StringComparer.Ordinal)
    {
        ["Count by customer"] = () => Orders(Column("customer", "o.CustomerId"), Column("n", "$count")),
        ["Sum, average, min and max by customer"] = () => Orders(
            Column("customer", "o.CustomerId"),
            Column("total", "o.Total", ReportAggregate.Sum),
            Column("avg", "o.Total", ReportAggregate.Average),
            Column("qty", "o.Quantity", ReportAggregate.Sum),
            Column("min", "o.PlacedUtc", ReportAggregate.Min),
            Column("max", "o.Total", ReportAggregate.Max),
            Column("count", "o.Total", ReportAggregate.Count)),
        ["Grand totals only"] = () => Orders(Column("n", "$count"), Column("total", "o.Total", ReportAggregate.Sum)),
        ["By local month"] = () => Orders(Column("month", "o.PlacedUtc", transform: ReportFieldTransform.Month), Column("n", "$count"), Column("total", "o.Total", ReportAggregate.Sum)),
        ["By local day and weekday"] = () => Orders(
            Column("day", "o.PlacedUtc", transform: ReportFieldTransform.Day),
            Column("weekday", "o.PlacedUtc", transform: ReportFieldTransform.DayOfWeek),
            Column("n", "$count")),
        ["By year, filtered on a date range"] = () => WithFilter(
            Orders(Column("year", "o.PlacedUtc", transform: ReportFieldTransform.Year), Column("total", "o.Total", ReportAggregate.Sum)),
            new ReportFilterDefinition { Id = "placed", Field = "o.PlacedUtc", Stage = ReportFilterStage.Rows, Operator = ReportFilterOperator.Between, Values = ["2026-02-01", "2026-03-01"] }),
        ["Filtered on a number and an exact customer"] = () => WithFilter(
            WithFilter(
                Orders(Column("customer", "o.CustomerId"), Column("n", "$count")),
                new ReportFilterDefinition { Id = "big", Field = "o.Total", Stage = ReportFilterStage.Rows, Operator = ReportFilterOperator.GreaterThan, Values = ["40"] }),
            new ReportFilterDefinition { Id = "who", Field = "o.CustomerId", Stage = ReportFilterStage.Rows, Operator = ReportFilterOperator.NotEquals, Values = ["C2"] }),
        ["Result filter and sort on a measure"] = () =>
        {
            var query = WithFilter(
                Orders(Column("customer", "o.CustomerId"), Column("total", "o.Total", ReportAggregate.Sum)),
                new ReportFilterDefinition { Id = "result", Field = "total", Stage = ReportFilterStage.Result, Operator = ReportFilterOperator.GreaterThanOrEqual, Values = ["70"] });

            query.Sorts = [new ReportSortDefinition { ColumnId = "total", Descending = true }];

            return query;
        },
    };

    [Theory]
    [MemberData(nameof(Reports))]
    public async Task GroupingInTheSource_GivesTheSameResultTotalsAndRegroups(string report)
    {
        // Arrange
        var query = Queries[report];
        var rowsOnly = SalesData();
        var grouping = new GroupingDataSource(SalesData());

        // Act
        var expected = await Engine(rowsOnly).ExecuteAsync(query(), LocalContext(), TestContext.Current.CancellationToken);
        var actual = await Engine(grouping).ExecuteAsync(query(), LocalContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(actual.GroupedBySource, "The source should have grouped the report.");
        Assert.False(expected.GroupedBySource);
        Assert.Equal(expected.Rows, actual.Rows);
        Assert.Equal(expected.FilteredRowCount, actual.FilteredRowCount);

        // Totals and every regrouping a chart or pivot can ask for.
        var dimensions = expected.Columns.Where(column => !column.IsMeasure).Select(column => column.Id).ToArray();

        Assert.Equal(expected.Regroup([]).Select(group => group.Values), actual.Regroup([]).Select(group => group.Values));

        foreach (var dimension in dimensions)
        {
            Assert.Equal(expected.Regroup([dimension]).Select(group => group.Values), actual.Regroup([dimension]).Select(group => group.Values));
        }
    }

    [Fact]
    public async Task ReportsTheSourceCannotGroupExactly_ReadRows()
    {
        // Arrange: a median, a text "contains" filter, and an upper-case transform cannot be grouped exactly.
        var queries = new[]
        {
            Orders(Column("customer", "o.CustomerId"), Column("median", "o.Total", ReportAggregate.Median)),
            WithFilter(Orders(Column("customer", "o.CustomerId"), Column("n", "$count")), new ReportFilterDefinition { Id = "c", Field = "o.CustomerId", Stage = ReportFilterStage.Rows, Operator = ReportFilterOperator.Contains, Values = ["1"] }),
            Orders(Column("customer", "o.CustomerId", transform: ReportFieldTransform.Upper), Column("n", "$count")),
            Orders(Column("order", "o.Id"), Column("total", "o.Total")),
        };

        foreach (var query in queries)
        {
            var grouping = new GroupingDataSource(SalesData());

            // Act
            var result = await Engine(grouping).ExecuteAsync(query, LocalContext(), TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.GroupedBySource);
            Assert.Equal(0, grouping.AggregateCalls);
        }
    }

    [Fact]
    public async Task ASourceThatDeclines_IsReadRowByRow()
    {
        // Arrange
        var grouping = new GroupingDataSource(SalesData()) { Decline = true };

        // Act
        var result = await Engine(grouping).ExecuteAsync(Orders(Column("customer", "o.CustomerId"), Column("n", "$count")), LocalContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.GroupedBySource);
        Assert.Equal(1, grouping.AggregateCalls);
        Assert.Equal(4, result.Rows.Count);
    }

    private static ReportQueryExecutionContext LocalContext()
    {
        var context = Context(new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc));

        context.ToLocal = _toLocal;
        context.ToUtc = _toUtc;

        return context;
    }

    private static ReportQueryDefinition Orders(params ReportColumnDefinition[] columns)
    {
        return new ReportQueryDefinition
        {
            DataSets = [new ReportDataSetReference { Alias = "o", Source = "Memory", DataSet = "Order" }],
            Columns = columns,
        };
    }

    private static ReportQueryDefinition WithFilter(ReportQueryDefinition query, ReportFilterDefinition filter)
    {
        query.Filters.Add(filter);

        return query;
    }

    /// <summary>
    /// Groups the in-memory rows by the <see cref="IReportAggregateDataSource"/> contract, as a database would: exact
    /// conditions, buckets by UTC boundaries, and per-group counts, sums, smallest and largest values.
    /// </summary>
    private sealed class GroupingDataSource : IReportDataSource, IReportAggregateDataSource
    {
        private readonly InMemoryReportDataSource _inner;

        public GroupingDataSource(InMemoryReportDataSource inner)
        {
            _inner = inner;
        }

        public bool Decline { get; set; }

        public int AggregateCalls { get; private set; }

        public string Name => _inner.Name;

        public LocalizedString DisplayName => _inner.DisplayName;

        public LocalizedString Description => _inner.Description;

        public Task<IReadOnlyList<ReportDataSetDescriptor>> GetDataSetsAsync(ReportDataSourceContext context, CancellationToken cancellationToken = default)
            => _inner.GetDataSetsAsync(context, cancellationToken);

        public Task<ReportDataSetSchema> GetSchemaAsync(string dataSet, ReportDataSourceContext context, CancellationToken cancellationToken = default)
            => _inner.GetSchemaAsync(dataSet, context, cancellationToken);

        public Task<ReportDataTable> QueryAsync(ReportDataSourceQuery query, CancellationToken cancellationToken = default)
            => _inner.QueryAsync(query, cancellationToken);

        public Task<ReportAggregateTable> AggregateAsync(ReportAggregateQuery query, CancellationToken cancellationToken = default)
        {
            AggregateCalls++;

            if (Decline)
            {
                return Task.FromResult<ReportAggregateTable>(null);
            }

            var (schema, rows) = _inner.Table(query.DataSet);
            int Index(string field) => schema.Fields.IndexOf(schema.FindField(field));

            var kept = rows.Where(row => query.Conditions.All(condition => Matches(row[Index(condition.Field)], condition))).ToList();
            var groups = new Dictionary<string, (object[] Keys, List<object[]> Rows)>(StringComparer.Ordinal);

            foreach (var row in kept)
            {
                var keys = query.Groups.Select(group => Key(row[Index(group.Field)], group.Boundaries)).ToArray();
                var key = string.Join('\u001F', keys.Select(ReportDataValues.ToKey));

                if (!groups.TryGetValue(key, out var group))
                {
                    groups[key] = group = (keys, []);
                }

                group.Rows.Add(row);
            }

            if (query.Groups.Count == 0 && groups.Count == 0)
            {
                groups[string.Empty] = ([], []);
            }

            if (groups.Count > query.MaxGroups)
            {
                return Task.FromResult<ReportAggregateTable>(null);
            }

            var table = new ReportAggregateTable
            {
                Rows = groups.Values.Select(group => group.Keys
                    .Concat(query.Measures.Select(measure => Measure(group.Rows, measure.Field is null ? -1 : Index(measure.Field), measure.Kind)))
                    .ToArray())
                    .ToList(),
            };

            return Task.FromResult(table);
        }

        private static object Key(object value, IList<DateTime> boundaries)
        {
            if (boundaries is null)
            {
                return value;
            }

            if (value is not DateTime date)
            {
                return null;
            }

            for (var index = 0; index < boundaries.Count - 1; index++)
            {
                if (date >= boundaries[index] && date < boundaries[index + 1])
                {
                    return (long)index;
                }
            }

            return null;
        }

        private static object Measure(List<object[]> rows, int index, ReportAggregateKind kind)
        {
            if (index < 0)
            {
                return (long)rows.Count;
            }

            var values = rows.Select(row => row[index]).Where(value => value is not null).ToList();

            return kind switch
            {
                ReportAggregateKind.Count => (long)values.Count,
                ReportAggregateKind.Sum => values.Count == 0 ? null : ReportAggregations.Sum(values),
                ReportAggregateKind.Min => values.Count == 0 ? null : values.Aggregate((left, right) => ReportDataValues.Compare(left, right) <= 0 ? left : right),
                _ => values.Count == 0 ? null : values.Aggregate((left, right) => ReportDataValues.Compare(left, right) >= 0 ? left : right),
            };
        }

        private static bool Matches(object value, ReportDataCondition condition)
        {
            var first = condition.Values.FirstOrDefault();

            return condition.Operator switch
            {
                ReportFilterOperator.IsEmpty => ReportDataValues.IsEmpty(value),
                ReportFilterOperator.IsNotEmpty => !ReportDataValues.IsEmpty(value),
                ReportFilterOperator.Equals => value is not null && ReportDataValues.AreEqual(value, first),
                ReportFilterOperator.NotEquals => value is null || !ReportDataValues.AreEqual(value, first),
                ReportFilterOperator.In => value is not null && condition.Values.Any(candidate => ReportDataValues.AreEqual(value, candidate)),
                ReportFilterOperator.NotIn => value is null || !condition.Values.Any(candidate => ReportDataValues.AreEqual(value, candidate)),
                ReportFilterOperator.GreaterThan => value is not null && ReportDataValues.Compare(value, first) > 0,
                ReportFilterOperator.GreaterThanOrEqual => value is not null && ReportDataValues.Compare(value, first) >= 0,
                ReportFilterOperator.LessThan => value is not null && ReportDataValues.Compare(value, first) < 0,
                ReportFilterOperator.LessThanOrEqual => value is not null && ReportDataValues.Compare(value, first) <= 0,
                _ => throw new InvalidOperationException($"The engine sent a condition the contract does not allow: {condition.Operator}."),
            };
        }
    }
}
