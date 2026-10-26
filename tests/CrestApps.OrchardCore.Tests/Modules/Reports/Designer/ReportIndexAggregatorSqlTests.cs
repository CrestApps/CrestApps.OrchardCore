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
/// The grouping statement adds up whole-number columns without overflowing on every database.
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
            Groups = [new DataAggregateGroup { Field = "Model" }],
            Measures =
            [
                new DataAggregateMeasure { Function = DataAggregate.Sum, Field = "Tokens" },
                new DataAggregateMeasure { Function = DataAggregate.Sum, Field = "Cost" },
            ],
        };

        var columns = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Model"] = nameof(UsageIndex.Model),
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

        public int Tokens { get; set; }

        public decimal Cost { get; set; }
    }
}
