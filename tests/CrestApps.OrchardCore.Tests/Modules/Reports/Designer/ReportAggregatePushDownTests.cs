using CrestApps.OrchardCore.Reports.Designer;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Operations;
using OrchardCore.Localization;
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
            Column("total", "o.Total", DataAggregate.Sum),
            Column("avg", "o.Total", DataAggregate.Average),
            Column("qty", "o.Quantity", DataAggregate.Sum),
            Column("min", "o.PlacedUtc", DataAggregate.Min),
            Column("max", "o.Total", DataAggregate.Max),
            Column("count", "o.Total", DataAggregate.Count)),
        ["Grand totals only"] = () => Orders(Column("n", "$count"), Column("total", "o.Total", DataAggregate.Sum)),
        ["By local month"] = () => Orders(Column("month", "o.PlacedUtc", transform: DataFieldTransform.Month), Column("n", "$count"), Column("total", "o.Total", DataAggregate.Sum)),
        ["By local day and weekday"] = () => Orders(
            Column("day", "o.PlacedUtc", transform: DataFieldTransform.Day),
            Column("weekday", "o.PlacedUtc", transform: DataFieldTransform.DayOfWeek),
            Column("n", "$count")),
        ["By year, filtered on a date range"] = () => WithFilter(
            Orders(Column("year", "o.PlacedUtc", transform: DataFieldTransform.Year), Column("total", "o.Total", DataAggregate.Sum)),
            new ReportFilterDefinition { Id = "placed", Field = "o.PlacedUtc", Stage = ReportFilterStage.Rows, Operator = DataFilterOperator.Between, Values = ["2026-02-01", "2026-03-01"] }),
        ["Filtered on a number and an exact customer"] = () => WithFilter(
            WithFilter(
                Orders(Column("customer", "o.CustomerId"), Column("n", "$count")),
                new ReportFilterDefinition { Id = "big", Field = "o.Total", Stage = ReportFilterStage.Rows, Operator = DataFilterOperator.GreaterThan, Values = ["40"] }),
            new ReportFilterDefinition { Id = "who", Field = "o.CustomerId", Stage = ReportFilterStage.Rows, Operator = DataFilterOperator.NotEquals, Values = ["C2"] }),
        ["Result filter and sort on a measure"] = () =>
        {
            var query = WithFilter(
                Orders(Column("customer", "o.CustomerId"), Column("total", "o.Total", DataAggregate.Sum)),
                new ReportFilterDefinition { Id = "result", Field = "total", Stage = ReportFilterStage.Result, Operator = DataFilterOperator.GreaterThanOrEqual, Values = ["70"] });

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
        var expected = await Engine(rowsOnly).ExecuteAsync(query(), RegroupedContext(query()), TestContext.Current.CancellationToken);
        var actual = await Engine(new TestDataSourceEntry("Memory", grouping)).ExecuteAsync(query(), RegroupedContext(query()), TestContext.Current.CancellationToken);

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
            Orders(Column("customer", "o.CustomerId"), Column("median", "o.Total", DataAggregate.Median)),
            WithFilter(Orders(Column("customer", "o.CustomerId"), Column("n", "$count")), new ReportFilterDefinition { Id = "c", Field = "o.CustomerId", Stage = ReportFilterStage.Rows, Operator = DataFilterOperator.Contains, Values = ["1"] }),
            Orders(Column("customer", "o.CustomerId", transform: DataFieldTransform.Upper), Column("n", "$count")),
            Orders(Column("order", "o.Id"), Column("total", "o.Total")),
        };

        foreach (var query in queries)
        {
            var grouping = new GroupingDataSource(SalesData());

            // Act
            var result = await Engine(new TestDataSourceEntry("Memory", grouping)).ExecuteAsync(query, LocalContext(), TestContext.Current.CancellationToken);

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
        var result = await Engine(new TestDataSourceEntry("Memory", grouping)).ExecuteAsync(Orders(Column("customer", "o.CustomerId"), Column("n", "$count")), LocalContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.GroupedBySource);
        Assert.Equal(1, grouping.AggregateCalls);
        Assert.Equal(4, result.Rows.Count);
    }

    // A context that regroups the result by nothing (the totals) and by each of its columns.
    private static ReportQueryExecutionContext RegroupedContext(ReportQueryDefinition query)
    {
        var context = LocalContext();

        context.Groupings.Add([]);

        foreach (var column in query.Columns)
        {
            context.Groupings.Add([column.Id]);
        }

        return context;
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
    /// Groups the in-memory rows by the <see cref="IDataAggregateSource"/> contract, as a database would: exact
    /// conditions, buckets by UTC boundaries, and per-group counts, sums, smallest and largest values.
    /// </summary>
    private sealed class GroupingDataSource : IDataSource, IDataAggregateSource
    {
        private readonly InMemoryReportDataSource _inner;

        public GroupingDataSource(InMemoryReportDataSource inner)
        {
            _inner = inner;
        }

        public bool Decline { get; set; }

        public int AggregateCalls { get; private set; }

        public static string Name => InMemoryReportDataSource.Name;

        public static LocalizationSource DisplayName => InMemoryReportDataSource.DisplayName;

        public static LocalizationSource Description => InMemoryReportDataSource.Description;

        public Task<IReadOnlyList<DataSetDescriptor>> GetDataSetsAsync(DataSourceContext context, CancellationToken cancellationToken = default)
            => _inner.GetDataSetsAsync(context, cancellationToken);

        public Task<DataSetSchema> GetSchemaAsync(string dataSet, DataSourceContext context, CancellationToken cancellationToken = default)
            => _inner.GetSchemaAsync(dataSet, context, cancellationToken);

        public IAsyncEnumerable<DataBatch> ReadAsync(DataSourceQuery query, CancellationToken cancellationToken = default)
            => _inner.ReadAsync(query, cancellationToken);

        public Task<IReadOnlyList<object[]>> AggregateAsync(DataAggregateQuery query, CancellationToken cancellationToken = default)
        {
            AggregateCalls++;

            if (Decline)
            {
                return Task.FromResult<IReadOnlyList<object[]>>(null);
            }

            var (schema, rows) = _inner.Table(query.DataSet);
            int Index(string field) => schema.Fields.IndexOf(schema.FindField(field));

            var kept = rows.Where(row => query.Conditions.All(condition => Matches(row[Index(condition.Field)], condition))).ToList();
            var groups = new Dictionary<string, (object[] Keys, List<object[]> Rows)>(StringComparer.Ordinal);

            foreach (var row in kept)
            {
                var keys = query.Groups.Select(group => Key(row[Index(group.Field)], group.Boundaries)).ToArray();
                var key = string.Join('\u001F', keys.Select(DataValues.ToKey));

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
                return Task.FromResult<IReadOnlyList<object[]>>(null);
            }

            IReadOnlyList<object[]> table = groups.Values.Select(group => group.Keys
                .Concat(query.Measures.Select(measure => Measure(group.Rows, measure.Field is null ? -1 : Index(measure.Field), measure.Function)))
                .ToArray())
                .ToList();

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

        private static object Measure(List<object[]> rows, int index, DataAggregate kind)
        {
            if (index < 0)
            {
                return (long)rows.Count;
            }

            var values = rows.Select(row => row[index]).Where(value => value is not null).ToList();

            return kind switch
            {
                DataAggregate.Count => (long)values.Count,
                DataAggregate.Sum => values.Count == 0 ? null : DataAggregations.Sum(values),
                DataAggregate.Min => values.Count == 0 ? null : values.Aggregate((left, right) => DataValues.Compare(left, right) <= 0 ? left : right),
                _ => values.Count == 0 ? null : values.Aggregate((left, right) => DataValues.Compare(left, right) >= 0 ? left : right),
            };
        }

        private static bool Matches(object value, DataCondition condition)
        {
            var first = condition.Values.FirstOrDefault();

            return condition.Operator switch
            {
                DataFilterOperator.IsEmpty => DataValues.IsEmpty(value),
                DataFilterOperator.IsNotEmpty => !DataValues.IsEmpty(value),
                DataFilterOperator.Equals => value is not null && DataValues.AreEqual(value, first),
                DataFilterOperator.NotEquals => value is null || !DataValues.AreEqual(value, first),
                DataFilterOperator.In => value is not null && condition.Values.Any(candidate => DataValues.AreEqual(value, candidate)),
                DataFilterOperator.NotIn => value is null || !condition.Values.Any(candidate => DataValues.AreEqual(value, candidate)),
                DataFilterOperator.GreaterThan => value is not null && DataValues.Compare(value, first) > 0,
                DataFilterOperator.GreaterThanOrEqual => value is not null && DataValues.Compare(value, first) >= 0,
                DataFilterOperator.LessThan => value is not null && DataValues.Compare(value, first) < 0,
                DataFilterOperator.LessThanOrEqual => value is not null && DataValues.Compare(value, first) <= 0,
                _ => throw new InvalidOperationException($"The engine sent a condition the contract does not allow: {condition.Operator}."),
            };
        }
    }
}
