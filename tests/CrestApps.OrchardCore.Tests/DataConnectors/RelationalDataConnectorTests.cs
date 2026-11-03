using System.Data.Common;
using CrestApps.Core;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Relational;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Data.Sqlite;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Tests.DataConnectors;

/// <summary>
/// Runs the relational connector base against a real SQLite database, through a dialect of a few lines, which is what
/// a PostgreSQL or MySQL connector would add.
/// </summary>
public sealed class RelationalDataConnectorTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-relational-{Guid.NewGuid():N}.db");
    private readonly SqliteDataConnector _connector;

    public RelationalDataConnectorTests()
    {
        _connector = new SqliteDataConnector($"Data Source={_path};Pooling=False");

        using var connection = new SqliteConnection($"Data Source={_path};Pooling=False");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE Customers (Id INTEGER PRIMARY KEY, Name TEXT, Balance REAL, Active INTEGER, Modified TEXT);
            INSERT INTO Customers VALUES (1, 'Ada', 10.5, 1, '2026-10-01 08:00:00');
            INSERT INTO Customers VALUES (2, 'grace', NULL, 0, '2026-10-02 08:00:00');
            INSERT INTO Customers VALUES (3, 'Linus', 99, 1, '2026-10-03 08:00:00');
            CREATE VIEW ActiveCustomers AS SELECT Id, Name FROM Customers WHERE Active = 1;
            """;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        TemporarySqliteDatabase.Delete(_path);
    }

    [Fact]
    public async Task DiscoverAsync_ListsTablesAndViews()
    {
        // Act
        var resources = await _connector.DiscoverAsync(Context(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(resources, resource => resource.Name == "ActiveCustomers" && resource.Kind == "View");
        Assert.Contains(resources, resource => resource.Name == "Customers" && resource.Kind == "Table");
    }

    [Fact]
    public async Task DescribeAsync_ReadsTheColumnsWithoutRows_AndTheirPrimaryKey()
    {
        // Act
        var fields = await _connector.DescribeAsync(Context(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["Id", "Name", "Balance", "Active", "Modified"], fields.Select(field => field.Name));
        Assert.True(fields[0].IsIdentifier);
        Assert.Equal(DataFieldType.Integer, fields[0].Type);
        Assert.Equal(DataFieldType.Text, fields[1].Type);
    }

    [Fact]
    public async Task ReadAsync_ReadsTheFieldsAskedFor_InBatches()
    {
        // Arrange
        var context = Context();
        context.Fields.Add("Name");
        context.BatchSize = 2;

        // Act
        var batches = await ReadAsync(context);

        // Assert
        Assert.Equal([2, 1], batches.Select(batch => batch.Count));
        Assert.Equal("Name", Assert.Single(batches[0].Fields).Name);
    }

    [Fact]
    public async Task ReadAsync_AppliesNumberRangesAtTheSource_ButNeverTextConditions()
    {
        // Arrange
        var context = Context();
        context.Conditions.Add(new DataCondition { Field = "Id", Operator = DataFilterOperator.GreaterThan, Values = [1L] });

        // A database's collation may compare text with case, the consumer doesn't, so this one must not drop 'grace'.
        context.Conditions.Add(new DataCondition { Field = "Name", Operator = DataFilterOperator.Equals, Values = ["GRACE"] });

        // Act
        var rows = (await ReadAsync(context)).SelectMany(batch => batch.Rows).ToList();

        // Assert
        Assert.Equal([2L, 3L], rows.Select(row => row[0]));
    }

    [Fact]
    public async Task ReadAsync_ReadsOnlyTheRowsAfterTheWatermark()
    {
        // Arrange
        var context = Context();
        context.WatermarkField = "Id";
        context.Watermark = 2L;

        // Act
        var rows = (await ReadAsync(context)).SelectMany(batch => batch.Rows).ToList();

        // Assert
        Assert.Equal(3L, Assert.Single(rows)[0]);
    }

    [Fact]
    public async Task ReadAsync_StopsAtTheMostRows()
    {
        // Arrange
        var context = Context();
        context.MaxRows = 1;

        // Act
        var rows = (await ReadAsync(context)).SelectMany(batch => batch.Rows).ToList();

        // Assert
        Assert.Single(rows);
    }

    [Fact]
    public async Task ReadAsync_WithACustomQuery_ReadsItAsASubquery_WithItsParameters()
    {
        // Arrange
        var dataSet = DataConnectorTestData.DataSet();
        dataSet.Resource = null;
        dataSet.Put(new RelationalDataSetSettings { Query = "SELECT Id, Name FROM Customers WHERE Balance > @Minimum;" });

        var context = new DataConnectorReadContext(DataConnectorTestData.Connection(), new FakeSecrets(), dataSet);
        context.Parameters["Minimum"] = 50;
        context.Conditions.Add(new DataCondition { Field = "Id", Operator = DataFilterOperator.LessThanOrEqual, Values = [3L] });

        // Act
        var rows = (await ReadAsync(context)).SelectMany(batch => batch.Rows).ToList();

        // Assert
        Assert.Equal("Linus", Assert.Single(rows)[1]);
    }

    [Fact]
    public async Task ReadAsync_NeverWritesThroughACustomQuery()
    {
        // Arrange
        var dataSet = DataConnectorTestData.DataSet();
        dataSet.Put(new RelationalDataSetSettings { Query = "SELECT 1); DELETE FROM Customers; SELECT (1" });
        var context = new DataConnectorReadContext(DataConnectorTestData.Connection(), new FakeSecrets(), dataSet);

        // Act
        await Assert.ThrowsAsync<DataConnectorException>(() => ReadAsync(context));

        // Assert
        Assert.Equal(3, (await ReadAsync(Context())).Sum(batch => batch.Count));
    }

    [Theory]
    [InlineData("SELECT * FROM t; DROP TABLE t")]
    [InlineData("SELECT 1) AS q; DELETE FROM t; SELECT * FROM (SELECT 1")]
    [InlineData("SELECT * FROM t -- comment")]
    [InlineData("WITH x AS (SELECT 1) SELECT * FROM x")]
    [InlineData("DELETE FROM t")]
    [InlineData("SELECT (1")]
    [InlineData("SELECT 'unclosed")]
    [InlineData("SELECTED")]
    public void ValidateQuery_RefusesAQueryThatCouldDoMoreThanRead(string query)
    {
        Assert.Throws<DataConnectorException>(() => RelationalDataConnector.ValidateQuery(query));
    }

    [Theory]
    [InlineData("SELECT * FROM t;", "SELECT * FROM t")]
    [InlineData("SELECT 'a;b)' AS x, [weird;)name] FROM t /* ; ) */", "SELECT 'a;b)' AS x, [weird;)name] FROM t /* ; ) */")]
    [InlineData("select it's = 'it''s' from t", null)]
    [InlineData("SELECT*FROM t", "SELECT*FROM t")]
    public void ValidateQuery_AcceptsASingleSelect_WithSeparatorsInsideQuotesAndComments(string query, string expected)
    {
        if (expected is null)
        {
            Assert.Throws<DataConnectorException>(() => RelationalDataConnector.ValidateQuery(query));

            return;
        }

        Assert.Equal(expected, RelationalDataConnector.ValidateQuery(query));
    }

    [Fact]
    public async Task TestAsync_ReportsTheServerVersion()
    {
        // Act
        var result = await _connector.TestAsync(new DataConnectorContext(DataConnectorTestData.Connection(), new FakeSecrets()), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.StartsWith("Connected.", result.Message, StringComparison.Ordinal);
    }

    private static DataConnectorReadContext Context()
    {
        var dataSet = DataConnectorTestData.DataSet();
        dataSet.Resource = "Customers";
        dataSet.Fields =
        [
            new ConnectedDataField { Name = "Id", SourceName = "Id", Type = DataFieldType.Integer, SourceType = DataFieldType.Integer, IsIdentifier = true },
            new ConnectedDataField { Name = "Name", SourceName = "Name", Type = DataFieldType.Text, SourceType = DataFieldType.Text },
        ];

        return new DataConnectorReadContext(DataConnectorTestData.Connection(), new FakeSecrets(), dataSet);
    }

    private async Task<List<DataBatch>> ReadAsync(DataConnectorReadContext context)
    {
        var batches = new List<DataBatch>();

        await foreach (var batch in _connector.ReadAsync(context, TestContext.Current.CancellationToken))
        {
            batches.Add(batch);
        }

        return batches;
    }

    private sealed class SqliteDataConnector : RelationalDataConnector
    {
        private readonly string _connectionString;

        public SqliteDataConnector(string connectionString)
        {
            _connectionString = connectionString;
        }

        protected override string ListResourcesSql =>
            "SELECT NULL, name, CASE type WHEN 'view' THEN 'View' ELSE 'Table' END FROM sqlite_master WHERE type IN ('table', 'view') ORDER BY name";

        protected override string VersionSql => "SELECT 'SQLite ' || sqlite_version()";

        protected override async Task<DbConnection> OpenConnectionAsync(DataConnectorContext context, CancellationToken cancellationToken)
        {
            var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            return connection;
        }

        protected override string QuoteIdentifier(string identifier)
            => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
