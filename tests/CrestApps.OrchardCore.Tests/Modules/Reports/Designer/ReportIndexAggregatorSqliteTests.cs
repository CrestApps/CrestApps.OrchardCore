using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Tests.Utilities;
using OrchardCore.DataSources;
using YesSql;
using YesSql.Indexes;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer;

/// <summary>
/// SQLite keeps a decimal column as whole and floating-point numbers, so its <c>SUM</c> of money comes back as a double
/// that can be off by a fraction of a cent. The grouping statement over a real SQLite store returns the exact decimal
/// sum, the same as adding up the records, or declines when it cannot tell it.
/// </summary>
public sealed class ReportIndexAggregatorSqliteTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-report-sums-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new MoneyRecordIndexProvider()]);
        await _store.InitializeAsync(TestContext.Current.CancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<MoneyRecordIndex>(table => table
            .Column<string>(nameof(MoneyRecordIndex.Account))
            .Column<decimal>(nameof(MoneyRecordIndex.Amount))
            .Column<decimal?>(nameof(MoneyRecordIndex.Tax), column => column.Nullable()));

        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task AggregateAsync_SumOfDecimals_IsTheExactDecimalSum()
    {
        // Arrange: 0.1 and 0.7 add up to 0.7999999999999999 as doubles, even summed without any rounding error, and the
        // cents of large amounts drift too.
        var records = new[] { new MoneyRecord { Account = "Sales", Amount = 0.1m, Tax = 0.01m }, new MoneyRecord { Account = "Sales", Amount = 0.7m, Tax = 0.07m } }
            .Concat(Enumerable.Range(0, 1_000).Select(number => new MoneyRecord
            {
                Account = "Expenses",
                Amount = 1234567.89m + (number * 0.07m),
                Tax = number % 2 == 0 ? null : 0.13m,
            }))
            .ToArray();

        await AddAsync(records);

        // Act
        var rows = await AggregateAsync(Sums());

        // Assert
        Assert.NotNull(rows);

        var byAccount = rows.ToDictionary(row => (string)row[0], StringComparer.Ordinal);

        foreach (var account in new[] { "Sales", "Expenses" })
        {
            var expected = records.Where(record => record.Account == account).ToArray();

            Assert.Equal(expected.Sum(record => record.Amount), Assert.IsType<decimal>(byAccount[account][2]));
            Assert.Equal(expected.Sum(record => record.Tax ?? 0m), Assert.IsType<decimal>(byAccount[account][3]));
        }

        Assert.Equal(0.8m, byAccount["Sales"][2]);
    }

    [Fact]
    public async Task AggregateAsync_SumOfWholeAmounts_IsReadAsItIs()
    {
        // Arrange
        await AddAsync([new MoneyRecord { Account = "Sales", Amount = 6200m }, new MoneyRecord { Account = "Sales", Amount = 1800m }]);

        // Act
        var rows = await AggregateAsync(Sums());

        // Assert
        var row = Assert.Single(rows);

        Assert.Equal(8000m, Convert.ToDecimal(row[2], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Null(row[3]);
    }

    [Fact]
    public async Task AggregateAsync_SumTooLargeToTellExactly_Declines()
    {
        // Arrange: at a hundred trillion, a cent is below what a double can tell apart, so the engine adds up the records.
        await AddAsync([new MoneyRecord { Account = "Sales", Amount = 100000000000000.01m }, new MoneyRecord { Account = "Sales", Amount = 0.01m }]);

        // Act
        var rows = await AggregateAsync(Sums());

        // Assert
        Assert.Null(rows);
    }

    [Fact]
    public void Build_SumOfADecimalOnSqlite_ReadsWhatMakesItExact()
    {
        // Act
        var statement = ReportIndexAggregator.Build<MoneyRecordIndex>(_store.Configuration, null, Sums(), Columns());

        // Assert
        Assert.NotNull(statement);
        Assert.Equal(4, statement.Columns.Count);
        Assert.Equal([new ReportIndexDecimalSum(2, 4), new ReportIndexDecimalSum(3, 7)], statement.DecimalSums);
        Assert.Contains("SUM(ABS(", statement.Sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0.30000000000000004d, 2, 0.3d, 2, "0.30")]
    [InlineData(-12.340000000000002d, 2, 20d, 3, "-12.34")]
    [InlineData(1234567890.1200001d, 2, 1234567890.12d, 1_000, "1234567890.12")]
    public void TryExactSum_RoundsToTheDecimalPlacesOfTheValues(double sum, long places, double magnitude, long count, string expected)
    {
        // Act
        var exact = ReportIndexAggregator.TryExactSum(sum, places, magnitude, count, out var value);

        // Assert
        Assert.True(exact);
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), value);
    }

    [Theory]
    [InlineData(1e14d, 2, 1e14d, 2)]
    [InlineData(1d, 29, 1d, 1)]
    [InlineData(double.NaN, 2, 1d, 1)]
    [InlineData(1d, 2, double.PositiveInfinity, 1)]
    public void TryExactSum_WhenTheErrorCouldReachTheLastPlace_Declines(double sum, long places, double magnitude, long count)
    {
        // Act
        var exact = ReportIndexAggregator.TryExactSum(sum, places, magnitude, count, out _);

        // Assert
        Assert.False(exact);
    }

    private static DataAggregateQuery Sums()
    {
        return new DataAggregateQuery
        {
            DataSet = "Money",
            Groups = [new DataAggregateGroup { Field = "Account" }],
            Measures =
            [
                new DataAggregateMeasure { Function = DataAggregate.Count },
                new DataAggregateMeasure { Function = DataAggregate.Sum, Field = "Amount" },
                new DataAggregateMeasure { Function = DataAggregate.Sum, Field = "Tax" },
            ],
        };
    }

    private static Dictionary<string, string> Columns()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Account"] = nameof(MoneyRecordIndex.Account),
            ["Amount"] = nameof(MoneyRecordIndex.Amount),
            ["Tax"] = nameof(MoneyRecordIndex.Tax),
        };
    }

    private async Task<IReadOnlyList<object[]>> AggregateAsync(DataAggregateQuery query)
    {
        await using var session = _store.CreateSession();

        return await ReportIndexAggregator.AggregateAsync<MoneyRecordIndex>(session, null, query, Columns(), TestContext.Current.CancellationToken);
    }

    private async Task AddAsync(IEnumerable<MoneyRecord> records)
    {
        await using var session = _store.CreateSession();

        foreach (var record in records)
        {
            await session.SaveAsync(record, cancellationToken: TestContext.Current.CancellationToken);
        }

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public sealed class MoneyRecord
    {
        public string Account { get; set; }

        public decimal Amount { get; set; }

        public decimal? Tax { get; set; }
    }

    public sealed class MoneyRecordIndex : MapIndex
    {
        public string Account { get; set; }

        public decimal Amount { get; set; }

        public decimal? Tax { get; set; }
    }

    private sealed class MoneyRecordIndexProvider : IndexProvider<MoneyRecord>
    {
        public override void Describe(DescribeContext<MoneyRecord> context)
        {
            context.For<MoneyRecordIndex>().Map(record => new MoneyRecordIndex
            {
                Account = record.Account,
                Amount = record.Amount,
                Tax = record.Tax,
            });
        }
    }
}
