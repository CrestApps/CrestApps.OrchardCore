using System.Globalization;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Contents.Models;
using CrestApps.OrchardCore.Reports.Contents.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Contents;

public sealed class ContentsReportDataSourceTests
{
    [Fact]
    public async Task GetDataSetsAsync_ListsOnlyTheContentTypesTheUserMayView()
    {
        // Arrange
        var menu = ContentReportTestHelpers.Type("MainMenu", "Main menu", "MenuItem");
        using var services = ContentReportTestHelpers.Services(
            ContentReportTestHelpers.Definitions(ContentReportTestHelpers.OrderType(), ContentReportTestHelpers.CustomerType(), menu).Object,
            ContentReportTestHelpers.AuthorizationFor("Customer", "MainMenu").Object);
        var dataSource = GetDataSource(services);

        // Act
        var dataSets = await dataSource.GetDataSetsAsync(Context(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ReportsConstants.ContentsDataSource, dataSource.Name);
        Assert.Equal("Content items", dataSource.DisplayName.Value);
        Assert.Equal(
            [("Customer", "Customer", "Content"), ("MainMenu", "Main menu", "MenuItem")],
            dataSets.Select(dataSet => (dataSet.Name, dataSet.DisplayName, dataSet.Group)));
    }

    [Fact]
    public async Task GetDataSetsAsync_WhenThereIsNoUser_ReturnsNothing()
    {
        // Arrange
        using var services = ContentReportTestHelpers.Services(
            ContentReportTestHelpers.Definitions(ContentReportTestHelpers.CustomerType()).Object,
            ContentReportTestHelpers.AuthorizationFor("Customer").Object);
        var dataSource = GetDataSource(services);

        // Act
        var dataSets = await dataSource.GetDataSetsAsync(new ReportDataSourceContext(), TestContext.Current.CancellationToken);
        var schema = await dataSource.GetSchemaAsync("Customer", new ReportDataSourceContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
    }

    [Theory]
    [InlineData("Order")]
    [InlineData("Missing")]
    [InlineData("customer")]
    [InlineData("")]
    public async Task GetSchemaAsync_WhenTheTypeIsUnknownOrNotViewable_ReturnsNull(string dataSet)
    {
        // Arrange
        using var services = ContentReportTestHelpers.Services(
            ContentReportTestHelpers.Definitions(ContentReportTestHelpers.CustomerType(), ContentReportTestHelpers.OrderType()).Object,
            ContentReportTestHelpers.AuthorizationFor("Customer").Object);
        var dataSource = GetDataSource(services);

        // Act
        var schema = await dataSource.GetSchemaAsync(dataSet, Context(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(schema);
    }

    [Fact]
    public async Task GetSchemaAsync_WhenTheTypeIsViewable_DescribesItsFields()
    {
        // Arrange
        using var services = ContentReportTestHelpers.Services(
            ContentReportTestHelpers.Definitions(ContentReportTestHelpers.CustomerType()).Object,
            ContentReportTestHelpers.AuthorizationFor("Customer").Object);
        var dataSource = GetDataSource(services);

        // Act
        var schema = await dataSource.GetSchemaAsync("Customer", Context(), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(schema);
        Assert.Equal("Customer", schema.DataSet.Name);
        Assert.True(schema.FindField("ContentItemId").IsIdentifier);
        Assert.Equal(ReportDataType.Decimal, schema.FindField("Customer.Balance").DataType);
    }

    [Fact]
    public async Task QueryAsync_WhenTheTypeIsNotViewable_ReadsNothing()
    {
        // Arrange
        var session = new Mock<ISession>(MockBehavior.Strict);
        using var services = ContentReportTestHelpers.Services(
            ContentReportTestHelpers.Definitions(ContentReportTestHelpers.CustomerType()).Object,
            ContentReportTestHelpers.AuthorizationFor().Object,
            session.Object);
        var dataSource = GetDataSource(services);

        // Act
        var table = await dataSource.QueryAsync(Query("Customer", 10), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(table.Fields);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public async Task QueryAsync_ReturnsOnlyRequestedFields_TypedAndOrdered_AndFlagsTruncation()
    {
        var databasePath = ContentReportTestHelpers.DatabasePath("query");
        var store = await ContentReportTestHelpers.CreateStoreAsync(databasePath);

        try
        {
            // Arrange
            await SeedAsync(
                store,
                Customer("customer-1", "Ada", 10.5m, new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc)),
                Customer("customer-2", "Grace", 20m, new DateTime(2026, 2, 5, 9, 0, 0, DateTimeKind.Utc)),
                Customer("customer-3", "Linus", 30.25m, new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc)),
                Draft(Customer("customer-4", "Draft", 40m, new DateTime(2026, 3, 6, 9, 0, 0, DateTimeKind.Utc))));

            await using var session = store.CreateSession();
            using var services = ContentReportTestHelpers.Services(
                ContentReportTestHelpers.Definitions(ContentReportTestHelpers.CustomerType()).Object,
                ContentReportTestHelpers.AuthorizationFor("Customer").Object,
                session);
            var dataSource = GetDataSource(services);

            // Act
            var all = await dataSource.QueryAsync(Query("Customer", 10, "CreatedUtc", "Customer.Balance", "ContentItemId", "Unknown"), TestContext.Current.CancellationToken);
            var limited = await dataSource.QueryAsync(Query("Customer", 2, "ContentItemId"), TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(["ContentItemId", "CreatedUtc", "Customer.Balance"], all.Fields.Select(field => field.Name));
            Assert.False(all.Truncated);
            Assert.Equal(3, all.Rows.Count);

            // Newest first, so a report that hits the row limit shows the latest items.
            Assert.Equal("customer-1", all.Rows[2][0]);
            Assert.Equal(new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc), all.Rows[2][1]);
            Assert.Equal(DateTimeKind.Utc, ((DateTime)all.Rows[2][1]).Kind);
            Assert.Equal(10.5m, all.Rows[2][2]);
            Assert.IsType<decimal>(all.Rows[0][2]);

            Assert.True(limited.Truncated);
            Assert.Equal(["customer-3", "customer-2"], limited.Rows.Select(row => (string)row[0]));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task QueryAsync_AppliesPushedDownDateConditions_AndIgnoresTheOthers()
    {
        var databasePath = ContentReportTestHelpers.DatabasePath("conditions");
        var store = await ContentReportTestHelpers.CreateStoreAsync(databasePath);

        try
        {
            // Arrange
            await SeedAsync(
                store,
                Customer("customer-1", "Ada", 10m, new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc)),
                Customer("customer-2", "Grace", 20m, new DateTime(2026, 2, 5, 9, 0, 0, DateTimeKind.Utc)),
                Customer("customer-3", "Linus", 30m, new DateTime(2026, 3, 5, 9, 0, 0, DateTimeKind.Utc)));

            await using var session = store.CreateSession();
            using var services = ContentReportTestHelpers.Services(
                ContentReportTestHelpers.Definitions(ContentReportTestHelpers.CustomerType()).Object,
                ContentReportTestHelpers.AuthorizationFor("Customer").Object,
                session);
            var dataSource = GetDataSource(services);
            var query = Query("Customer", 10, "ContentItemId");

            query.Conditions.Add(new ReportDataCondition
            {
                Field = "CreatedUtc",
                Operator = ReportFilterOperator.Between,
                Values = [new DateTime(2026, 2, 5, 9, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)],
            });
            query.Conditions.Add(new ReportDataCondition
            {
                Field = "DisplayText",
                Operator = ReportFilterOperator.In,
                Values = ["ADA"],
            });

            // Act
            var table = await dataSource.QueryAsync(query, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(["customer-2"], table.Rows.Select(row => (string)row[0]));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task QueryAsync_ResolvesPickedDisplayTexts_OnlyForViewableTypes()
    {
        var databasePath = ContentReportTestHelpers.DatabasePath("picker");
        var store = await ContentReportTestHelpers.CreateStoreAsync(databasePath);

        try
        {
            // Arrange
            await SeedAsync(
                store,
                Customer("customer-1", "Ada", 10m, null),
                Customer("customer-2", "Grace", 20m, null),
                ContentReportTestHelpers.Item("Secret", "secret-1", displayText: "Top secret"),
                Order("order-1", "customer-1", "customer-2"),
                Order("order-2", "secret-1"),
                Order("order-3"));

            await using var session = store.CreateSession();
            using var services = ContentReportTestHelpers.Services(
                ContentReportTestHelpers.Definitions(ContentReportTestHelpers.CustomerType(), ContentReportTestHelpers.OrderType()).Object,
                ContentReportTestHelpers.AuthorizationFor("Customer", "Order").Object,
                session);
            var dataSource = GetDataSource(services);

            // Act
            var table = await dataSource.QueryAsync(
                Query("Order", 10, "ContentItemId", "Order.Customer", "Order.Customer.DisplayText"),
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(
                [
                    ("order-3", null, null),
                    ("order-2", "secret-1", null),
                    ("order-1", "customer-1", "Ada,Grace"),
                ],
                table.Rows.Select(row => ((string)row[0], (string)row[1], (string)row[2])));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task QueryAsync_PreparesOnlyTheRequestedFields()
    {
        var databasePath = ContentReportTestHelpers.DatabasePath("prepare");
        var store = await ContentReportTestHelpers.CreateStoreAsync(databasePath);

        try
        {
            // Arrange
            await SeedAsync(store, ContentReportTestHelpers.Item("Audited", "audited-1"));

            var provider = new RecordingFieldProvider();
            var definition = ContentReportTestHelpers.Type(
                "Audited",
                "Audited",
                stereotype: null,
                ContentReportTestHelpers.Part("Audited", "Audited", "Audited", ContentReportTestHelpers.Field("Trail", "AuditField", "Trail")));

            await using var session = store.CreateSession();
            using var services = ContentReportTestHelpers.Services(
                ContentReportTestHelpers.Definitions(definition).Object,
                ContentReportTestHelpers.AuthorizationFor("Audited").Object,
                session,
                collection => collection.AddSingleton<IContentReportFieldProvider>(provider));
            var dataSource = GetDataSource(services);

            // Act
            await dataSource.QueryAsync(Query("Audited", 10, "ContentItemId"), TestContext.Current.CancellationToken);
            var preparedWithoutTheField = provider.PrepareCount;
            var table = await dataSource.QueryAsync(Query("Audited", 10, "Audited.Trail"), TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(0, preparedWithoutTheField);
            Assert.Equal(1, provider.PrepareCount);
            Assert.Equal("prepared:1", Assert.Single(table.Rows)[0]);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static ContentsReportDataSource GetDataSource(ServiceProvider services)
    {
        return Assert.IsType<ContentsReportDataSource>(services.GetRequiredService<IReportDataSource>());
    }

    private static ReportDataSourceContext Context()
    {
        return new ReportDataSourceContext
        {
            User = ContentReportTestHelpers.User,
        };
    }

    private static ReportDataSourceQuery Query(string dataSet, int maxRows, params string[] fields)
    {
        return new ReportDataSourceQuery
        {
            DataSet = dataSet,
            Fields = new HashSet<string>(fields, StringComparer.Ordinal),
            MaxRows = maxRows,
            Context = Context(),
        };
    }

    private static ContentItem Customer(string id, string name, decimal balance, DateTime? createdUtc)
    {
        return ContentReportTestHelpers.Item(
            "Customer",
            id,
            $$"""{ "Customer": { "Balance": { "Value": {{balance.ToString(CultureInfo.InvariantCulture)}} } } }""",
            createdUtc,
            name);
    }

    private static ContentItem Order(string id, params string[] customerIds)
    {
        var ids = string.Join(", ", customerIds.Select(customerId => $"\"{customerId}\""));

        return ContentReportTestHelpers.Item("Order", id, $$"""{ "Order": { "Customer": { "ContentItemIds": [ {{ids}} ] } } }""", null, id);
    }

    private static ContentItem Draft(ContentItem contentItem)
    {
        contentItem.Published = false;

        return contentItem;
    }

    private static async Task SeedAsync(IStore store, params ContentItem[] contentItems)
    {
        await using var session = store.CreateSession();

        foreach (var contentItem in contentItems)
        {
            await session.SaveAsync(contentItem, cancellationToken: TestContext.Current.CancellationToken);
        }

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private sealed class RecordingFieldProvider : IContentReportFieldProvider
    {
        public int PrepareCount { get; private set; }

        public string FieldType => "AuditField";

        public IEnumerable<ContentReportField> GetFields(ContentReportFieldContext context)
        {
            return [new RecordingField(context.CreateDescriptor(null, context.DisplayName, ReportDataType.Text), this)];
        }

        private sealed class RecordingField(ReportFieldDescriptor descriptor, RecordingFieldProvider provider) : ContentReportField(descriptor)
        {
            public override Task PrepareAsync(ContentReportQueryContext context, CancellationToken cancellationToken = default)
            {
                provider.PrepareCount++;

                return Task.CompletedTask;
            }

            public override object GetValue(ContentItem contentItem, ContentReportQueryContext context)
            {
                return $"prepared:{provider.PrepareCount}";
            }
        }
    }
}
