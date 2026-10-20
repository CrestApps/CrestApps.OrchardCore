using CrestApps.OrchardCore.Reports.Designer;
using OrchardCore.DataSources;
using static CrestApps.OrchardCore.Tests.Modules.Reports.Designer.ReportDesignerTestServices;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Designer;

/// <summary>
/// A joined data set whose join field the source can filter on is read only for the keys the rows before it hold, in
/// batches, and the result is the same as reading it in full.
/// </summary>
public sealed class ReportJoinKeyPushDownTests
{
    [Fact]
    public async Task Join_SendsTheKeysOfTheRowsBefore_AndGivesTheSameResult()
    {
        // Arrange
        var full = await ExecuteAsync(keyFilterable: false, DataJoinType.Inner);
        var (narrowed, source) = await ExecuteWithSourceAsync(keyFilterable: true, DataJoinType.Inner);

        // Assert
        Assert.Equal(full.Rows, narrowed.Rows);

        var orderQuery = Assert.Single(source.Queries, query => query.DataSet == "Order");
        var keys = Assert.Single(orderQuery.Conditions, condition => condition.IsJoinKey);
        Assert.Equal("CustomerId", keys.Field);
        Assert.Equal(DataFilterOperator.In, keys.Operator);
        Assert.Equal(["c1", "c2", "c3", "c4"], keys.Values.Cast<string>().Order());
    }

    [Fact]
    public async Task LeftJoin_IsNarrowedToo()
    {
        // Act
        var full = await ExecuteAsync(keyFilterable: false, DataJoinType.Left);
        var (narrowed, source) = await ExecuteWithSourceAsync(keyFilterable: true, DataJoinType.Left);

        // Assert
        Assert.Equal(full.Rows, narrowed.Rows);
        Assert.Contains(source.Queries.Single(query => query.DataSet == "Order").Conditions, condition => condition.IsJoinKey);
    }

    [Theory]
    [InlineData(DataJoinType.Right)]
    [InlineData(DataJoinType.Full)]
    public async Task JoinsThatKeepUnmatchedJoinedRows_ReadTheJoinedDataSetInFull(DataJoinType joinType)
    {
        // Act
        var (_, source) = await ExecuteWithSourceAsync(keyFilterable: true, joinType);

        // Assert
        Assert.DoesNotContain(source.Queries.Single(query => query.DataSet == "Order").Conditions, condition => condition.IsJoinKey);
    }

    [Fact]
    public async Task AFieldTheSourceCannotFilterOn_IsReadInFull()
    {
        // Act
        var (_, source) = await ExecuteWithSourceAsync(keyFilterable: false, DataJoinType.Inner);

        // Assert
        Assert.DoesNotContain(source.Queries.Single(query => query.DataSet == "Order").Conditions, condition => condition.IsJoinKey);
    }

    [Fact]
    public async Task Keys_AreSentInBatches()
    {
        // Act
        var (result, source) = await ExecuteWithSourceAsync(keyFilterable: true, DataJoinType.Inner, new ReportQueryLimits { JoinKeyBatchSize = 3 });

        // Assert
        var batches = source.Queries.Where(query => query.DataSet == "Order").ToList();
        Assert.Equal(2, batches.Count);
        Assert.Equal(4, batches.Sum(query => query.Conditions.Single(condition => condition.IsJoinKey).Values.Count));
        Assert.Equal((await ExecuteAsync(keyFilterable: false, DataJoinType.Inner)).Rows, result.Rows);
    }

    [Fact]
    public async Task MoreKeysThanTheLimit_ReadTheJoinedDataSetInFull()
    {
        // Act
        var (_, source) = await ExecuteWithSourceAsync(keyFilterable: true, DataJoinType.Inner, new ReportQueryLimits { MaxJoinKeys = 2 });

        // Assert
        Assert.DoesNotContain(source.Queries.Single(query => query.DataSet == "Order").Conditions, condition => condition.IsJoinKey);
    }

    [Fact]
    public async Task NoRowsBefore_SkipsReadingTheJoinedDataSet()
    {
        // Arrange
        var source = await SourceAsync(keyFilterable: true);
        var query = Query(DataJoinType.Inner);
        query.Filters.Add(new ReportFilterDefinition { Id = "none", Field = "c.Name", Stage = ReportFilterStage.Rows, Operator = DataFilterOperator.Equals, Values = ["Nobody"] });

        // Act
        var result = await Engine(source).ExecuteAsync(query, Context(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result.Rows);
        Assert.DoesNotContain(source.Queries, sourceQuery => sourceQuery.DataSet == "Order");
    }

    private static async Task<ReportQueryResult> ExecuteAsync(bool keyFilterable, DataJoinType joinType)
    {
        return (await ExecuteWithSourceAsync(keyFilterable, joinType)).Result;
    }

    private static async Task<(ReportQueryResult Result, InMemoryReportDataSource Source)> ExecuteWithSourceAsync(bool keyFilterable, DataJoinType joinType, ReportQueryLimits limits = null)
    {
        var source = await SourceAsync(keyFilterable);
        var context = Context();

        context.Limits = limits ?? new ReportQueryLimits();

        var result = await Engine(source).ExecuteAsync(Query(joinType), context, TestContext.Current.CancellationToken);

        return (result, source);
    }

    // The sales data, applying conditions like a database would, with the order's customer marked key-filterable or not.
    private static async Task<InMemoryReportDataSource> SourceAsync(bool keyFilterable)
    {
        var source = SalesData();

        source.ApplyConditions = true;
        (await source.GetSchemaAsync("Order", new DataSourceContext(), TestContext.Current.CancellationToken)).FindField("CustomerId").IsKeyFilterable = keyFilterable;

        return source;
    }

    private static ReportQueryDefinition Query(DataJoinType joinType)
    {
        var query = CustomersWithOrders(joinType);

        query.Columns =
        [
            Column("name", "c.Name"),
            Column("order", "o.Id"),
            Column("total", "o.Total"),
        ];
        query.Sorts = [new ReportSortDefinition { ColumnId = "name" }, new ReportSortDefinition { ColumnId = "order" }];

        return query;
    }
}
