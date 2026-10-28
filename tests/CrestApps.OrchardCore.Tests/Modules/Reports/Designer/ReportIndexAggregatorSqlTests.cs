using CrestApps.OrchardCore.Reports.Designer.DataSources;
using OrchardCore.DataSources;
using YesSql;
using YesSql.Indexes;
using YesSql.Provider.MySql;
using YesSql.Provider.PostgreSql;
using YesSql.Provider.Sqlite;
using YesSql.Provider.SqlServer;
using YesSqlConfiguration = YesSql.Configuration;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer;

/// <summary>
/// The grouping statement adds up whole-number columns without overflowing on every database, and leaves to the engine
/// whatever a database's collation would merge or order differently.
/// </summary>
public sealed class ReportIndexAggregatorSqlTests
{
    [Theory]
    [InlineData(nameof(SqlServerDialect), true)]
    [InlineData(nameof(SqliteDialect), false)]
    [InlineData(nameof(PostgreSqlDialect), false)]
    [InlineData(nameof(MySqlDialect), false)]
    public void Build_SumOfAnIntColumn_IsWidenedWhereTheDatabaseWouldOverflow(string dialectName, bool widened)
    {
        // Arrange
        var configuration = new YesSqlConfiguration
        {
            SqlDialect = CreateDialect(dialectName),
        };

        var query = new DataAggregateQuery
        {
            DataSet = "Usage",
            Groups = [new DataAggregateGroup { Field = "Region" }],
            Measures =
            [
                new DataAggregateMeasure { Function = DataAggregate.Sum, Field = "Tokens" },
                new DataAggregateMeasure { Function = DataAggregate.Sum, Field = "Cost" },
            ],
        };

        var columns = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Region"] = nameof(UsageIndex.Region),
            ["Tokens"] = nameof(UsageIndex.Tokens),
            ["Cost"] = nameof(UsageIndex.Cost),
        };

        // Act
        var statement = ReportIndexAggregator.Build<UsageIndex>(configuration, null, query, columns);

        // Assert
        Assert.NotNull(statement);
        Assert.Equal(widened, statement.Sql.Contains("CAST(", StringComparison.Ordinal));
        Assert.DoesNotContain("CAST(" + configuration.SqlDialect.QuoteForColumnName(nameof(UsageIndex.Cost)), statement.Sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(DataAggregate.Min)]
    [InlineData(DataAggregate.Max)]
    public void Build_TheSmallestOrLargestTextValue_Declines(DataAggregate function)
    {
        // Arrange: the engine orders text without regard to case; a database orders it by its collation.
        var query = new DataAggregateQuery
        {
            DataSet = "Usage",
            Groups = [new DataAggregateGroup { Field = "Region" }],
            Measures = [new DataAggregateMeasure { Function = function, Field = "Model" }],
        };

        // Act
        var statement = ReportIndexAggregator.Build<UsageIndex>(Configuration(), null, query, Columns());

        // Assert
        Assert.Null(statement);
    }

    [Theory]
    [InlineData(nameof(SqlServerDialect), true)]
    [InlineData(nameof(SqliteDialect), true)]
    [InlineData(nameof(PostgreSqlDialect), true)]
    [InlineData(nameof(MySqlDialect), false)]
    public void Build_AGroupByText_IsLeftToTheEngineOnlyOnMySql(string dialectName, bool inDatabase)
    {
        // Arrange: the engine groups the database's groups again ignoring case, so a database may keep values that differ
        // only in case apart; MySQL's default collation also ignores accents, merging values the engine keeps apart.
        var configuration = new YesSqlConfiguration
        {
            SqlDialect = CreateDialect(dialectName),
        };
        var query = new DataAggregateQuery
        {
            DataSet = "Usage",
            Groups = [new DataAggregateGroup { Field = "Model" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
        };

        // Act
        var statement = ReportIndexAggregator.Build<UsageIndex>(configuration, null, query, Columns());

        // Assert
        Assert.Equal(inDatabase, statement is not null);
    }

    [Fact]
    public void Build_GroupsByEnumsAndDates_AndCountsAndFiltersText_InTheDatabase()
    {
        // Arrange
        var start = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var query = new DataAggregateQuery
        {
            DataSet = "Usage",
            Groups =
            [
                new DataAggregateGroup { Field = "Region" },
                new DataAggregateGroup { Field = "CreatedUtc", Boundaries = [start, start.AddDays(1), start.AddDays(2)] },
            ],
            Measures =
            [
                new DataAggregateMeasure { Function = DataAggregate.Count, Field = "Model" },
                new DataAggregateMeasure { Function = DataAggregate.Max, Field = "Tokens" },
            ],
            Conditions = [new DataCondition { Field = "Model", Operator = DataFilterOperator.Equals, Values = ["gpt-4o"] }],
        };

        // Act
        var statement = ReportIndexAggregator.Build<UsageIndex>(Configuration(), null, query, Columns());

        // Assert
        Assert.NotNull(statement);
        Assert.Contains("GROUP BY", statement.Sql, StringComparison.Ordinal);
    }

    private static YesSqlConfiguration Configuration()
    {
        return new YesSqlConfiguration
        {
            SqlDialect = new SqliteDialect(),
        };
    }

    private static Dictionary<string, string> Columns()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Model"] = nameof(UsageIndex.Model),
            ["Region"] = nameof(UsageIndex.Region),
            ["Tokens"] = nameof(UsageIndex.Tokens),
            ["Cost"] = nameof(UsageIndex.Cost),
            ["CreatedUtc"] = nameof(UsageIndex.CreatedUtc),
        };
    }

    private static ISqlDialect CreateDialect(string dialectName)
    {
        return dialectName switch
        {
            nameof(SqliteDialect) => new SqliteDialect(),
            nameof(SqlServerDialect) => new SqlServerDialect(),
            nameof(PostgreSqlDialect) => new PostgreSqlDialect(),
            nameof(MySqlDialect) => new MySqlDialect(),
            _ => throw new ArgumentOutOfRangeException(nameof(dialectName), dialectName, "Unknown dialect."),
        };
    }

    private sealed class UsageIndex : MapIndex
    {
        public string Model { get; set; }

        public UsageRegion Region { get; set; }

        public int Tokens { get; set; }

        public decimal Cost { get; set; }

        public DateTime CreatedUtc { get; set; }
    }

    private enum UsageRegion
    {
        East,
        West,
    }
}
