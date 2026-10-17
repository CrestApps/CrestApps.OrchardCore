using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Queries;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Queries;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Queries;

public sealed class QueriesReportDataSourceTests
{
    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("u1", "Analyst");

    [Fact]
    public void Flatten_NestedObjectsBecomeDottedFields_AndListsAreJoined()
    {
        // Arrange
        var item = JsonNode.Parse("""
            {
              "ContentItemId": "abc",
              "TitlePart": { "Title": "Acme" },
              "Customer": { "Balance": { "Value": 12.5 }, "Tags": { "Values": ["a", "b"] } },
              "Ignored": [ { "Nested": true } ]
            }
            """);

        // Act
        var values = QueryResultSchema.Flatten(item).ToDictionary(pair => pair.Key, pair => pair.Value?.ToString());

        // Assert
        Assert.Equal("abc", values["ContentItemId"]);
        Assert.Equal("Acme", values["TitlePart.Title"]);
        Assert.Equal("12.5", values["Customer.Balance.Value"]);
        Assert.Equal("a, b", values["Customer.Tags.Values"]);
        Assert.False(values.ContainsKey("Ignored"));
    }

    // A query set to return content items hands back ContentItem objects rather than JSON rows; their parts and fields
    // must still become report fields.
    [Fact]
    public void Flatten_ContentItem_GivesItsPropertiesPartsAndFields()
    {
        // Arrange
        var contentItem = new ContentItem
        {
            ContentItemId = "c1",
            ContentType = "Customer",
            DisplayText = "Acme",
        };
        contentItem.Content.TitlePart = new JsonObject { ["Title"] = "Acme" };
        contentItem.Content.Customer = new JsonObject { ["Balance"] = new JsonObject { ["Value"] = 12.5 } };

        // Act
        var values = QueryResultSchema.Flatten(contentItem).ToDictionary(pair => pair.Key, pair => pair.Value?.ToString());

        // Assert
        Assert.Equal("c1", values["ContentItemId"]);
        Assert.Equal("Customer", values["ContentType"]);
        Assert.Equal("Acme", values["TitlePart.Title"]);
        Assert.Equal("12.5", values["Customer.Balance.Value"]);
    }

    [Fact]
    public void InferFields_ReadsTypesAcrossRows()
    {
        // Arrange
        var rows = new[]
        {
            QueryResultSchema.Flatten(JsonNode.Parse("""{ "CustomerId": "c1", "Total": 10, "Paid": true, "PlacedUtc": "2026-01-10T15:00:00Z", "Day": "2026-01-10", "Note": null }""")),
            QueryResultSchema.Flatten(JsonNode.Parse("""{ "CustomerId": "c2", "Total": 12.5, "Paid": false, "PlacedUtc": "2026-02-05T09:00:00Z", "Day": "2026-02-05", "Note": "late" }""")),
        };

        // Act
        var fields = QueryResultSchema.InferFields(rows).ToDictionary(field => field.Name);

        // Assert
        Assert.Equal(ReportDataType.Text, fields["CustomerId"].DataType);
        Assert.True(fields["CustomerId"].IsIdentifier);
        Assert.Equal(ReportDataType.Decimal, fields["Total"].DataType);
        Assert.Equal(ReportDataType.Boolean, fields["Paid"].DataType);
        Assert.Equal(ReportDataType.DateTime, fields["PlacedUtc"].DataType);
        Assert.Equal(ReportDataType.Date, fields["Day"].DataType);
        Assert.Equal(ReportDataType.Text, fields["Note"].DataType);
    }

    [Fact]
    public void ToValue_ReadsDateTimesAsUtc()
    {
        // Act
        var value = QueryResultSchema.ToValue(JsonValue.Create("2026-01-10T15:00:00"), ReportDataType.DateTime);

        // Assert
        var date = Assert.IsType<DateTime>(value);
        Assert.Equal(DateTimeKind.Utc, date.Kind);
        Assert.Equal(new DateTime(2026, 1, 10, 15, 0, 0), date);
    }

    [Fact]
    public async Task Query_CanBeJoinedAndAggregatedLikeAnyDataSet()
    {
        // Arrange
        var (source, manager) = Source(executeAllowed: true);
        var engine = ReportDesignerTestServices.Engine(source);
        var query = new ReportQueryDefinition
        {
            DataSets = [new ReportDataSetReference { Alias = "q", Source = ReportsConstants.QueriesDataSource, DataSet = "Orders" }],
            Columns =
            [
                new ReportColumnDefinition { Id = "customer", Field = "q.CustomerId" },
                new ReportColumnDefinition { Id = "total", Field = "q.Total", Aggregate = ReportAggregate.Sum },
            ],
        };
        var context = ReportDesignerTestServices.Context();
        context.DataSourceContext.User = _user;

        // Act
        var result = await engine.ExecuteAsync(query, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["c1", 22.5m], result.Rows[0]);
        Assert.Equal(["c2", 5m], result.Rows[1]);
        manager.Verify(value => value.ExecuteQueryAsync(It.IsAny<Query>(), It.IsAny<IDictionary<string, object>>()), Times.Once());
    }

    [Fact]
    public async Task Query_TheUserMayNotExecute_IsHidden()
    {
        // Arrange
        var (source, manager) = Source(executeAllowed: false);
        var context = new ReportDataSourceContext { User = _user };

        // Act & Assert
        Assert.Empty(await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken));
        Assert.Null(await source.GetSchemaAsync("Orders", context, TestContext.Current.CancellationToken));
        Assert.Empty((await source.QueryAsync(new ReportDataSourceQuery { DataSet = "Orders", MaxRows = 10, Context = context }, TestContext.Current.CancellationToken)).Rows);
        manager.Verify(value => value.ExecuteQueryAsync(It.IsAny<Query>(), It.IsAny<IDictionary<string, object>>()), Times.Never());
    }

    [Fact]
    public async Task Query_ThatFails_ReportsWhichQueryFailed()
    {
        // Arrange
        var (source, manager) = Source(executeAllowed: true);
        manager
            .Setup(value => value.ExecuteQueryAsync(It.IsAny<Query>(), It.IsAny<IDictionary<string, object>>()))
            .ThrowsAsync(new InvalidOperationException("syntax error"));

        // Act
        var exception = await Assert.ThrowsAsync<ReportQueryException>(() => source.GetSchemaAsync("Orders", new ReportDataSourceContext { User = _user }, TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("The query 'Orders' failed: syntax error", exception.Message, StringComparison.Ordinal);
    }

    private static (QueriesReportDataSource Source, Mock<IQueryManager> Manager) Source(bool executeAllowed)
    {
        var query = new Query
        {
            Name = "Orders",
            Source = "Sql",
        };

        var results = new Mock<IQueryResults>();
        results.SetupGet(value => value.Items).Returns(
        [
            JsonNode.Parse("""{ "CustomerId": "c1", "Total": 10 }"""),
            JsonNode.Parse("""{ "CustomerId": "c1", "Total": 12.5 }"""),
            JsonNode.Parse("""{ "CustomerId": "c2", "Total": 5 }"""),
        ]);

        var manager = new Mock<IQueryManager>();
        manager.Setup(value => value.ListQueriesAsync(It.IsAny<QueryContext>())).ReturnsAsync([query]);
        manager.Setup(value => value.GetQueryAsync("Orders")).ReturnsAsync(query);
        manager.Setup(value => value.ExecuteQueryAsync(It.IsAny<Query>(), It.IsAny<IDictionary<string, object>>())).ReturnsAsync(results.Object);

        var authorization = executeAllowed
            ? new FakeAuthorizationService(QueryPermissions.CreatePermissionForQuery("Orders").Name)
            : new FakeAuthorizationService();

        var source = new QueriesReportDataSource(
            manager.Object,
            authorization,
            NullLogger<QueriesReportDataSource>.Instance,
            new PassThroughStringLocalizer<QueriesReportDataSource>());

        return (source, manager);
    }
}
