using System.Data.Common;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using Microsoft.Data.Sqlite;
using YesSql;
using YesSql.Provider.MySql;
using YesSql.Provider.PostgreSql;
using YesSql.Provider.Sqlite;
using YesSql.Provider.SqlServer;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterMigrationSqlTests
{
    public static TheoryData<string, string> DialectExpectations => new()
    {
        {
            nameof(SqliteDialect),
            "CREATE UNIQUE INDEX [UQ_Sample] ON [tp_Table] ([ColumnA], [ColumnB])"
        },
        {
            nameof(SqlServerDialect),
            "CREATE UNIQUE INDEX [UQ_Sample] ON [tp_Table] ([ColumnA], [ColumnB])"
        },
        {
            nameof(PostgreSqlDialect),
            "CREATE UNIQUE INDEX \"tp_UQ_Sample\" ON \"tp_Table\" (\"ColumnA\", \"ColumnB\")"
        },
        {
            nameof(MySqlDialect),
            "CREATE UNIQUE INDEX `UQ_Sample` ON `tp_Table` (`ColumnA`, `ColumnB`)"
        },
    };

    [Theory]
    [MemberData(nameof(DialectExpectations))]
    public void BuildCreateUniqueIndexStatement_EmitsEngineCorrectSqlForEverySupportedDialect(
        string dialectName,
        string expectedStatement)
    {
        // Arrange
        var dialect = CreateDialect(dialectName);
        var quotedTableName = dialect.QuoteForTableName("tp_Table", schema: null);

        // Act
        var statement = ContactCenterMigrationSql.BuildCreateUniqueIndexStatement(
            dialect,
            "tp_",
            quotedTableName,
            "UQ_Sample",
            "ColumnA",
            "ColumnB");

        // Assert
        Assert.Equal(expectedStatement, statement);
    }

    [Theory]
    [InlineData(nameof(SqliteDialect))]
    [InlineData(nameof(SqlServerDialect))]
    [InlineData(nameof(PostgreSqlDialect))]
    [InlineData(nameof(MySqlDialect))]
    public void BuildCreateUniqueIndexStatement_PrefixesTheIndexNameOnlyWhenTheEngineRequiresGloballyUniqueNames(
        string dialectName)
    {
        // Arrange
        var dialect = CreateDialect(dialectName);

        // Act
        var statement = ContactCenterMigrationSql.BuildCreateUniqueIndexStatement(
            dialect,
            "tp_",
            dialect.QuoteForTableName("tp_Table", schema: null),
            "UQ_Sample",
            "ColumnA");

        // Assert
        Assert.Equal(
            dialect.PrefixIndex,
            statement.Contains("tp_UQ_Sample", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BuildCreateUniqueIndexStatement_ProducesExecutableSqlThatRejectsDuplicateRows()
    {
        // Arrange
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var dialect = new SqliteDialect();
        var quotedTableName = dialect.QuoteForTableName("tp_Table", schema: null);
        await ExecuteAsync(connection, $"CREATE TABLE {quotedTableName} (\"ColumnA\" TEXT, \"ColumnB\" TEXT)");

        var statement = ContactCenterMigrationSql.BuildCreateUniqueIndexStatement(
            dialect,
            "tp_",
            quotedTableName,
            "UQ_Sample",
            "ColumnA",
            "ColumnB");

        // Act
        await ExecuteAsync(connection, statement);
        await ExecuteAsync(connection, $"INSERT INTO {quotedTableName} (\"ColumnA\", \"ColumnB\") VALUES ('a', 'b')");

        var duplicate = await Assert.ThrowsAsync<SqliteException>(
            () => ExecuteAsync(connection, $"INSERT INTO {quotedTableName} (\"ColumnA\", \"ColumnB\") VALUES ('a', 'b')"));

        await ExecuteAsync(connection, $"INSERT INTO {quotedTableName} (\"ColumnA\", \"ColumnB\") VALUES ('a', 'c')");

        // Assert
        Assert.Contains("UNIQUE constraint failed", duplicate.Message, StringComparison.Ordinal);
        Assert.Equal(2L, await ScalarAsync(connection, $"SELECT COUNT(*) FROM {quotedTableName}"));
    }

    [Fact]
    public void BuildCreateUniqueIndexStatement_WhenTheDialectIsMissing_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ContactCenterMigrationSql.BuildCreateUniqueIndexStatement(
            null,
            "tp_",
            "\"tp_Table\"",
            "UQ_Sample",
            "ColumnA"));
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

    private static async Task ExecuteAsync(DbConnection connection, string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<object> ScalarAsync(DbConnection connection, string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;

        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }
}
