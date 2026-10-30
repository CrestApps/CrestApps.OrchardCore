using System.Text.Json.Nodes;
using CrestApps.OrchardCore.DataConnectors;
using Microsoft.Data.Sqlite;

namespace CrestApps.OrchardCore.Tests.DataConnectors;

public sealed class SqlDataSetRowStoreTests
{
    [Fact]
    public async Task Migration_CreatesTheRowTable_UnderTheTenantTablePrefix()
    {
        // Arrange
        await using var fixture = await DataConnectorsSqliteStore.CreateAsync();

        await using var connection = new SqliteConnection(fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
        command.Parameters.AddWithValue("$name", DataConnectorsSqliteStore.TablePrefix + DataConnectorConstants.RowsTableName);

        // Act
        var count = Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ReadAsync_PagesThroughAVersion_InTheOrderItWasWritten()
    {
        // Arrange
        await using var fixture = await DataConnectorsSqliteStore.CreateAsync();
        var store = fixture.CreateRowStore();
        var rows = Enumerable.Range(1, 250).Select(index => new DataSetRow($"k{index}", new JsonObject { ["Id"] = index })).ToArray();

        await store.WriteAsync("ds", "v1", rows, TestContext.Current.CancellationToken);
        await store.WriteAsync("ds", "v2", [new DataSetRow(null, new JsonObject { ["Id"] = 999 })], TestContext.Current.CancellationToken);

        // Act
        var read = new List<StoredDataSetRow>();
        long after = 0;

        while (true)
        {
            var page = await store.ReadAsync("ds", "v1", after, 100, cancellationToken: TestContext.Current.CancellationToken);

            if (page.Count == 0)
            {
                break;
            }

            read.AddRange(page);
            after = page[^1].Id;
        }

        // Assert
        Assert.Equal(250, read.Count);
        Assert.Equal(Enumerable.Range(1, 250), read.Select(row => row.Data["Id"].GetValue<int>()));
        Assert.Equal(250, await store.CountAsync("ds", "v1", TestContext.Current.CancellationToken));
        Assert.Equal(1, await store.CountAsync("ds", "v2", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadAsync_ByKeys_ReturnsOnlyThoseRows()
    {
        // Arrange
        await using var fixture = await DataConnectorsSqliteStore.CreateAsync();
        var store = fixture.CreateRowStore();

        await store.WriteAsync("ds", "v1", [Row("a", 1), Row("b", 2), Row("c", 3)], TestContext.Current.CancellationToken);

        // Act
        var rows = await store.ReadAsync("ds", "v1", 0, 10, ["c", "a"], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["a", "c"], rows.Select(row => row.Key));
    }

    [Fact]
    public async Task CopyAsync_AndDeleteKeysAsync_ReplaceRowsInTheNewVersionOnly()
    {
        // Arrange
        await using var fixture = await DataConnectorsSqliteStore.CreateAsync();
        var store = fixture.CreateRowStore();

        await store.WriteAsync("ds", "v1", [Row("a", 1), Row("b", 2)], TestContext.Current.CancellationToken);

        // Act
        var copied = await store.CopyAsync("ds", "v1", "v2", TestContext.Current.CancellationToken);
        var deleted = await store.DeleteKeysAsync("ds", "v2", ["b", "missing"], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, copied);
        Assert.Equal(1, deleted);
        Assert.Equal(2, await store.CountAsync("ds", "v1", TestContext.Current.CancellationToken));
        Assert.Equal(1, await store.CountAsync("ds", "v2", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAllAsync_KeepsOnlyTheVersionsAskedFor_AndOtherDataSets()
    {
        // Arrange
        await using var fixture = await DataConnectorsSqliteStore.CreateAsync();
        var store = fixture.CreateRowStore();

        await store.WriteAsync("ds", "v1", [Row("a", 1)], TestContext.Current.CancellationToken);
        await store.WriteAsync("ds", "v2", [Row("a", 1)], TestContext.Current.CancellationToken);
        await store.WriteAsync("ds", "v3", [Row("a", 1)], TestContext.Current.CancellationToken);
        await store.WriteAsync("other", "v1", [Row("a", 1)], TestContext.Current.CancellationToken);

        // Act
        await store.DeleteAllAsync("ds", ["v2", "v3"], TestContext.Current.CancellationToken);
        await store.DeleteVersionAsync("ds", "v3", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, await store.CountAsync("ds", "v1", TestContext.Current.CancellationToken));
        Assert.Equal(1, await store.CountAsync("ds", "v2", TestContext.Current.CancellationToken));
        Assert.Equal(0, await store.CountAsync("ds", "v3", TestContext.Current.CancellationToken));
        Assert.Equal(1, await store.CountAsync("other", "v1", TestContext.Current.CancellationToken));
    }

    private static DataSetRow Row(string key, int id)
        => new(key, new JsonObject { ["Id"] = id });
}
