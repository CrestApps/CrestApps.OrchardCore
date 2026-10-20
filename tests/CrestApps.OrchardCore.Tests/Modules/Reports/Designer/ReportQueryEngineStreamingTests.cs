using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Reports.Designer;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Expressions;
using OrchardCore.Localization;
using static CrestApps.OrchardCore.Tests.Modules.Reports.Designer.ReportDesignerTestServices;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer;

/// <summary>
/// A run streams its rows: it joins, groups, regroups and sorts them with a bounded amount of memory, writing the rows
/// that don't fit to temporary files, and reads no more rows than its result needs.
/// </summary>
public sealed class ReportQueryEngineStreamingTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "crestapps-report-tests", Guid.NewGuid().ToString("n"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    // Far more rows than a megabyte holds, joined, grouped with a distinct count and an aggregate formula, regrouped for
    // totals and subtotals, and sorted: the run writes rows to temporary files, gives the result it gives in memory,
    // and deletes the files.
    [Fact]
    public async Task ExecuteAsync_RowsThatDontFitInMemory_GiveTheSameResultAndLeaveNoFiles()
    {
        // Arrange
        var data = ManyOrders(customers: 3_000, orders: 60_000);
        var query = CustomersWithOrders();
        query.CalculatedFields =
        [
            new ReportCalculatedField { Name = "AverageOrder", Expression = "SUM([o.Total]) / COUNT()" },
        ];
        query.Columns =
        [
            Column("region", "c.Region"),
            Column("customer", "c.Name"),
            Column("orders", "o.Id", DataAggregate.CountDistinct),
            Column("revenue", "o.Total", DataAggregate.Sum),
            Column("average", "AverageOrder"),
        ];
        query.Sorts = [new ReportSortDefinition { ColumnId = "revenue", Descending = true }];

        // Act
        var inMemory = await RunAsync(data, query, memoryLimitMegabytes: 1024);
        var spilled = await RunAsync(data, query, memoryLimitMegabytes: 1);

        // Assert
        Assert.Equal(3_000, inMemory.Rows.Count);
        Assert.Equal(inMemory.Rows, spilled.Rows);
        Assert.Equal(inMemory.GetTotals(), spilled.GetTotals());
        Assert.Equal(inMemory.Regroup(["region"]).Select(group => group.Values), spilled.Regroup(["region"]).Select(group => group.Values));
        Assert.Equal(60_000L, spilled.GetTotals()[2]);
        Assert.True(Directory.Exists(_directory), "The run should have written rows to temporary files.");
        Assert.Empty(Directory.GetFiles(_directory, "*", SearchOption.AllDirectories));
    }

    // A join reads the joined data set only for the keys of the rows before it, even when those rows don't fit in memory.
    [Fact]
    public async Task ExecuteAsync_JoinOverManyRows_ReadsTheJoinedDataSetForTheirKeys()
    {
        // Arrange
        var data = ManyOrders(customers: 3_000, orders: 60_000);
        data.ApplyConditions = true;
        var query = CustomersWithOrders();
        query.Filters = [new ReportFilterDefinition { Id = "region", Field = "c.Region", Operator = DataFilterOperator.Equals, Values = ["Region 1"] }];
        query.Columns = [Column("customer", "c.Name"), Column("revenue", "o.Total", DataAggregate.Sum)];

        // Act
        var result = await RunAsync(data, query, memoryLimitMegabytes: 1);

        // Assert
        Assert.Equal(300, result.Rows.Count);

        var orders = data.Queries.Where(sent => sent.DataSet == "Order").ToList();

        Assert.NotEmpty(orders);
        Assert.All(orders, sent => Assert.Contains(sent.Conditions, condition => condition.IsJoinKey));
    }

    // A result that is neither grouped nor sorted stops reading once it has its rows.
    [Fact]
    public async Task ExecuteAsync_DetailRowsWithALimit_StopReadingOnceTheResultIsComplete()
    {
        // Arrange
        var source = new BatchedDataSource(rows: 100_000, batchSize: 500);
        var query = new ReportQueryDefinition
        {
            DataSets = [new ReportDataSetReference { Alias = "n", Source = "Batches", DataSet = "Numbers" }],
            Columns = [Column("value", "n.Value")],
            Limit = 10,
        };

        // Act
        var result = await Engine(new TestDataSourceEntry("Batches", source)).ExecuteAsync(query, Context(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(Enumerable.Range(0, 10).Select(value => (long)value), result.Rows.Select(row => (long)row[0]));
        Assert.Equal(1, source.BatchesRead);
    }

    [Fact]
    public async Task Gate_BoundsTheRunsThatRunAtOnce_ButNotARunWithinARun()
    {
        // Arrange
        var gate = new ReportQueryGate();
        var first = Context();
        var second = Context();

        first.Limits.MaxConcurrentRuns = 1;
        second.Limits.MaxConcurrentRuns = 1;

        // Act
        var lease = await gate.EnterAsync(first, TestContext.Current.CancellationToken);
        var nested = gate.EnterAsync(first, TestContext.Current.CancellationToken);
        var waiting = gate.EnterAsync(second, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(nested.IsCompleted, "A run within a run, such as a view a report reads, doesn't wait for itself.");
        Assert.False(waiting.IsCompleted, "A second run waits for the first one to end.");

        await (await nested).DisposeAsync();
        await lease.DisposeAsync();
        await (await waiting.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).DisposeAsync();
    }

    // A report aggregates its rows as they arrive, so an aggregate function that needs all the values of a group at
    // once is refused rather than computed over rows held in memory.
    [Fact]
    public async Task PlanAsync_AggregateFunctionThatNeedsAllValues_IsRefused()
    {
        // Arrange
        var product = ExpressionFunction.CreateAggregate("PRODUCT", "PRODUCT(number)", "Multiplies the values.", true, _ => DataFieldType.Decimal, values => values.OfType<decimal>().Aggregate(1m, (total, value) => total * value));
        var planner = new ReportQueryPlanner(
            new TestDataSourceManager([new TestDataSourceEntry("Memory", SalesData())]),
            new ExpressionFunctionCatalog([new BuiltInExpressionFunctionProvider(), new FunctionProvider(product)]),
            new PassThroughStringLocalizerFactory(),
            new PassThroughStringLocalizer<ReportQueryPlanner>());
        var query = CustomersWithOrders();
        query.CalculatedFields = [new ReportCalculatedField { Name = "Product", Expression = "PRODUCT([o.Total])" }];
        query.Columns = [Column("region", "c.Region"), Column("product", "Product")];

        // Act
        var plan = await planner.PlanAsync(query, new DataSourceContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(plan.Errors, error => error.Contains("can't compute", StringComparison.Ordinal));
    }

    private async Task<ReportQueryResult> RunAsync(InMemoryReportDataSource data, ReportQueryDefinition query, int memoryLimitMegabytes)
    {
        var context = Context();

        context.Limits.MemoryLimitMegabytes = memoryLimitMegabytes;
        context.Limits.TemporaryDirectory = _directory;
        context.Limits.MaxRowsPerDataSet = 100_000;
        context.Limits.MaxJoinKeys = 100_000;
        context.Groupings.Add([]);
        context.Groupings.Add(["region"]);

        return await Engine(data).ExecuteAsync(query, context, TestContext.Current.CancellationToken);
    }

    // Customers in ten regions, and orders spread over them.
    private static InMemoryReportDataSource ManyOrders(int customers, int orders)
    {
        var customerRows = Enumerable.Range(0, customers)
            .Select(index => new object[] { "c" + index, "Customer " + index, "Region " + (index % 10), true })
            .ToArray();
        var orderRows = Enumerable.Range(0, orders)
            .Select(index => new object[] { "o" + index, "c" + (index % customers), (decimal)(index % 97) + 0.5m, (long)(index % 5), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(index) })
            .ToArray();
        var sales = SalesData();

        return new InMemoryReportDataSource()
            .Add("Customer", sales.Table("Customer").Schema.Fields.Select(field => KeyFilterable(field)), customerRows)
            .Add("Order", sales.Table("Order").Schema.Fields.Select(field => KeyFilterable(field)), orderRows);
    }

    private static DataField KeyFilterable(DataField field)
    {
        field.IsKeyFilterable = field.IsIdentifier;

        return field;
    }

    // A data set of numbers read in batches, which counts the batches it was asked for.
    private sealed class BatchedDataSource : IDataSource
    {
        private static readonly DataField[] _fields = [new("Value", "Value", DataFieldType.Integer)];

        private readonly int _rows;
        private readonly int _batchSize;

        public BatchedDataSource(int rows, int batchSize)
        {
            _rows = rows;
            _batchSize = batchSize;
        }

        public static string Name => "Batches";

        public static LocalizationSource DisplayName { get; } = LocalizationSource.Create("Batches");

        public static LocalizationSource Description { get; } = LocalizationSource.Create("Batches");

        public int BatchesRead { get; private set; }

        public Task<IReadOnlyList<DataSetDescriptor>> GetDataSetsAsync(DataSourceContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DataSetDescriptor>>([new DataSetDescriptor("Numbers", "Numbers")]);
        }

        public Task<DataSetSchema> GetSchemaAsync(string dataSet, DataSourceContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DataSetSchema
            {
                DataSet = new DataSetDescriptor("Numbers", "Numbers"),
                Fields = _fields,
            });
        }

        public async IAsyncEnumerable<DataBatch> ReadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (var start = 0; start < _rows; start += _batchSize)
            {
                await Task.Yield();

                BatchesRead++;

                yield return new DataBatch(_fields, [.. Enumerable.Range(start, Math.Min(_batchSize, _rows - start)).Select(value => new object[] { (long)value })]);
            }
        }
    }

    private sealed class FunctionProvider : IExpressionFunctionProvider
    {
        private readonly ExpressionFunction _function;

        public FunctionProvider(ExpressionFunction function)
        {
            _function = function;
        }

        public IEnumerable<ExpressionFunction> GetFunctions() => [_function];
    }
}
