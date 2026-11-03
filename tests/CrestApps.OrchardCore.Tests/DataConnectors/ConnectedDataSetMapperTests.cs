using CrestApps.OrchardCore.DataConnectors.Services;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Tests.DataConnectors;

public sealed class ConnectedDataSetMapperTests
{
    [Fact]
    public async Task MapAsync_MapsSourceNamesToDataSetFields_AndCoercesTheValues()
    {
        // Arrange
        var mapper = ConnectedDataSetMapper.Create(DataConnectorTestData.DataSet(), ["Name", "Id"]);
        DataField[] source = [new("name", "name", DataFieldType.Text), new("id", "id", DataFieldType.Text)];

        // Act
        var batches = await ToListAsync(mapper.MapAsync(Batches(new DataBatch(source, [["Ada", "7"]])), 100, 0, TestContext.Current.CancellationToken));

        // Assert
        var batch = Assert.Single(batches);
        Assert.Equal(["Id", "Name"], batch.Fields.Select(field => field.Name));
        Assert.Equal(7L, batch.Rows[0][0]);
        Assert.Equal("Ada", batch.Rows[0][1]);
    }

    [Fact]
    public async Task MapAsync_ReBatchesRows_AndStopsAtTheMostRows()
    {
        // Arrange
        var mapper = ConnectedDataSetMapper.Create(DataConnectorTestData.DataSet(), null);
        DataField[] source = [new("id", "id", DataFieldType.Integer)];
        var rows = Enumerable.Range(1, 10).Select(index => new object[] { (long)index }).ToArray();

        // Act
        var batches = await ToListAsync(mapper.MapAsync(Batches(new DataBatch(source, rows[..4]), new DataBatch(source, rows[4..])), batchSize: 3, maxRows: 7, cancellationToken: TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal([3, 3, 1], batches.Select(batch => batch.Count));
        Assert.Equal(7L, batches[^1].Rows[0][0]);
    }

    [Fact]
    public async Task MapAsync_WithNoRows_YieldsOneEmptyBatchSoTheFieldsAreKnown()
    {
        // Arrange
        var mapper = ConnectedDataSetMapper.Create(DataConnectorTestData.DataSet(), null);

        // Act
        var batches = await ToListAsync(mapper.MapAsync(Batches(), 10, 0, TestContext.Current.CancellationToken));

        // Assert
        var batch = Assert.Single(batches);
        Assert.Equal(0, batch.Count);
        Assert.Equal(3, batch.Fields.Count);
    }

    [Fact]
    public async Task MapAsync_ASourceFieldTheBatchLacks_ReadsAsNull()
    {
        // Arrange
        var mapper = ConnectedDataSetMapper.Create(DataConnectorTestData.DataSet(), null);
        DataField[] source = [new("id", "id", DataFieldType.Integer)];

        // Act
        var batches = await ToListAsync(mapper.MapAsync(Batches(new DataBatch(source, [[1L]])), 10, 0, TestContext.Current.CancellationToken));

        // Assert
        Assert.Null(batches[0].Rows[0][1]);
    }

    [Fact]
    public void Coerce_NormalizesDatesToUtc_AndGuidsToText()
    {
        // Arrange
        var local = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Unspecified);
        var guid = Guid.NewGuid();

        // Act
        var date = (DateTime)ConnectedDataSetMapper.Coerce(local, DataFieldType.DateTime);
        var text = ConnectedDataSetMapper.Coerce(guid, DataFieldType.Text);

        // Assert
        Assert.Equal(DateTimeKind.Utc, date.Kind);
        Assert.Equal(guid.ToString(), text);
        Assert.Null(ConnectedDataSetMapper.Coerce(DBNull.Value, DataFieldType.Text));
    }

    /// <summary>
    /// A live read of a CSV file returned its dates with no kind ("2026-10-01") while the same file imported returned
    /// them as UTC ("2026-10-01T00:00:00Z"), so one data set read differently depending on how it was accessed.
    /// </summary>
    [Theory]
    [InlineData("2026-10-01", DataFieldType.Date)]
    [InlineData("2026-10-01 08:30:00", DataFieldType.DateTime)]
    public void Coerce_ADateParsedFromText_IsUtc_AsAnImportedDateIs(string text, DataFieldType type)
    {
        // Act
        var value = Assert.IsType<DateTime>(ConnectedDataSetMapper.Coerce(text, type));

        // Assert
        Assert.Equal(DateTimeKind.Utc, value.Kind);
        Assert.Equal(DateTime.Parse(text, System.Globalization.CultureInfo.InvariantCulture), new DateTime(value.Ticks, DateTimeKind.Unspecified));
    }

    [Fact]
    public void ToSourceConditions_KeepsOnlyConditionsOnFieldsThatReadTheSourceType()
    {
        // Arrange
        var dataSet = DataConnectorTestData.DataSet();
        dataSet.Fields[1].Type = DataFieldType.Integer;
        var mapper = ConnectedDataSetMapper.Create(dataSet, null);
        DataCondition[] conditions =
        [
            new() { Field = "Id", Operator = DataFilterOperator.GreaterThan, Values = [5L] },
            new() { Field = "Name", Operator = DataFilterOperator.Equals, Values = [1L] },
            new() { Field = "Missing", Operator = DataFilterOperator.Equals, Values = [1L] },
        ];

        // Act
        var translated = mapper.ToSourceConditions(conditions).ToList();

        // Assert
        var condition = Assert.Single(translated);
        Assert.Equal("id", condition.Field);
        Assert.Equal(DataFilterOperator.GreaterThan, condition.Operator);
    }

    [Fact]
    public void Create_WithAllFields_ReadsTheFieldsConsumersDoNotSee()
    {
        // Arrange
        var dataSet = DataConnectorTestData.DataSet();
        dataSet.Fields[2].IsIncluded = false;

        // Act
        var visible = ConnectedDataSetMapper.Create(dataSet, null);
        var all = ConnectedDataSetMapper.Create(dataSet, null, allFields: true);

        // Assert
        Assert.Equal(2, visible.Fields.Count);
        Assert.Equal(3, all.Fields.Count);
    }

    private static async IAsyncEnumerable<DataBatch> Batches(params DataBatch[] batches)
    {
        foreach (var batch in batches)
        {
            await Task.Yield();

            yield return batch;
        }
    }

    internal static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> items)
    {
        var list = new List<T>();

        await foreach (var item in items)
        {
            list.Add(item);
        }

        return list;
    }
}
