using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using CrestApps.OrchardCore.Reports.Contents.Models;
using CrestApps.OrchardCore.Reports.Contents.Services;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Contents;

public sealed class ContentReportPropertyDiscoveryTests
{
    private const string StoredJson = """
        {
          "Customer": { "Balance": { "Value": 10 }, "Region": "West" },
          "TitlePart": { "Title": "Ada" },
          "AutoroutePart": { "Path": "ada", "SetHomepage": false },
          "LoyaltyPart": {
            "Stars": 4,
            "JoinedUtc": "2026-01-05T09:00:00Z",
            "LeftUtc": null,
            "Settings": { "Mode": "auto" },
            "ApiToken": "secret-value",
            "Tags": [ "gold", "early" ],
            "History": [ { "Points": 1 } ]
          }
        }
        """;

    [Fact]
    public async Task GetSchemaAsync_DescribesEveryStoredPartProperty_ButNotFieldsDescribedPropertiesListsOfObjectsOrSecrets()
    {
        var databasePath = ContentReportTestHelpers.DatabasePath("discovery");
        var store = await ContentReportTestHelpers.CreateStoreAsync(databasePath);

        try
        {
            // Arrange
            await SeedAsync(store, ContentReportTestHelpers.Item("Customer", "customer-1", StoredJson));
            await using var session = store.CreateSession();
            using var services = Services(session);
            var dataSource = services.GetRequiredService<IReportDataSource>();

            // Act
            var schema = await dataSource.GetSchemaAsync("Customer", Context(), TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(ReportDataType.Integer, schema.FindField("LoyaltyPart.Stars").DataType);
            Assert.Equal(ReportDataType.DateTime, schema.FindField("LoyaltyPart.JoinedUtc").DataType);
            Assert.Equal(ReportDataType.DateTime, schema.FindField("LoyaltyPart.LeftUtc").DataType);
            Assert.Equal(ReportDataType.Text, schema.FindField("LoyaltyPart.Settings.Mode").DataType);
            Assert.Equal(ReportDataType.Text, schema.FindField("LoyaltyPart.Tags").DataType);
            Assert.Equal("Loyalty part (more)", schema.FindField("LoyaltyPart.Stars").Group);
            Assert.Equal("Settings › Mode", schema.FindField("LoyaltyPart.Settings.Mode").DisplayName);
            Assert.Equal(ReportDataType.Text, schema.FindField("Customer.Region").DataType);
            Assert.Equal(ReportDataType.Boolean, schema.FindField("AutoroutePart.SetHomepage").DataType);

            Assert.Null(schema.FindField("LoyaltyPart.ApiToken"));
            Assert.Null(schema.FindField("LoyaltyPart.History"));
            Assert.Null(schema.FindField("Customer.Balance.Value"));
            Assert.Single(schema.Fields, field => field.Name == "AutoroutePart.Path");
            Assert.Single(schema.Fields, field => field.Name == "TitlePart.Title");
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task QueryAsync_ReadsDiscoveredProperties_TypedAndWithListsJoined()
    {
        var databasePath = ContentReportTestHelpers.DatabasePath("discovery-query");
        var store = await ContentReportTestHelpers.CreateStoreAsync(databasePath);

        try
        {
            // Arrange
            await SeedAsync(store, ContentReportTestHelpers.Item("Customer", "customer-1", StoredJson));
            await using var session = store.CreateSession();
            using var services = Services(session);
            var dataSource = services.GetRequiredService<IReportDataSource>();

            // Act
            var table = await dataSource.QueryAsync(Query("ContentItemId", "LoyaltyPart.Stars", "LoyaltyPart.Tags", "LoyaltyPart.Settings.Mode", "LoyaltyPart.ApiToken"), TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(["ContentItemId", "LoyaltyPart.Stars", "LoyaltyPart.Settings.Mode", "LoyaltyPart.Tags"], table.Fields.Select(field => field.Name));
            Assert.Equal(["customer-1", 4L, "auto", "gold,early"], table.Rows.Single());
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task GetSchemaAsync_TypesPropertiesFromAPropertySource_EvenWhenNoItemStoresThem()
    {
        var databasePath = ContentReportTestHelpers.DatabasePath("discovery-source");
        var store = await ContentReportTestHelpers.CreateStoreAsync(databasePath);

        try
        {
            // Arrange
            await using var session = store.CreateSession();
            using var services = Services(session, collection =>
                collection.AddScoped<IContentReportPropertySource>(_ => new FixedPropertySource("AutoroutePart", new ContentReportProperty("Absolute", ReportDataType.Boolean))));
            var dataSource = services.GetRequiredService<IReportDataSource>();

            // Act
            var schema = await dataSource.GetSchemaAsync("Customer", Context(), TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(ReportDataType.Boolean, schema.FindField("AutoroutePart.Absolute").DataType);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task RecipeSchemaSource_DescribesThePropertiesOfAPartSchema()
    {
        // Arrange
        var source = new RecipeSchemaContentReportPropertySource([new AutoroutePartSchema()]);
        var typePart = ContentReportTestHelpers.Part("AutoroutePart", "AutoroutePart", "Autoroute");

        // Act
        var properties = await source.GetPropertiesAsync(typePart, TestContext.Current.CancellationToken);
        var unknown = await source.GetPropertiesAsync(ContentReportTestHelpers.Part("LoyaltyPart", "LoyaltyPart", "Loyalty"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(new ContentReportProperty("Path", ReportDataType.Text), properties);
        Assert.Contains(new ContentReportProperty("SetHomepage", ReportDataType.Boolean), properties);
        Assert.Empty(unknown);
    }

    private static ServiceProvider Services(ISession session, Action<IServiceCollection> configure = null)
    {
        return ContentReportTestHelpers.Services(
            ContentReportTestHelpers.Definitions(ContentReportTestHelpers.CustomerType()).Object,
            ContentReportTestHelpers.AuthorizationFor("Customer").Object,
            session,
            configure);
    }

    private static ReportDataSourceContext Context()
    {
        return new ReportDataSourceContext
        {
            User = ContentReportTestHelpers.User,
        };
    }

    private static ReportDataSourceQuery Query(params string[] fields)
    {
        return new ReportDataSourceQuery
        {
            DataSet = "Customer",
            Fields = new HashSet<string>(fields, StringComparer.Ordinal),
            MaxRows = 10,
            Context = Context(),
        };
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

    private sealed class FixedPropertySource : IContentReportPropertySource
    {
        private readonly string _partName;
        private readonly ContentReportProperty[] _properties;

        public FixedPropertySource(string partName, params ContentReportProperty[] properties)
        {
            _partName = partName;
            _properties = properties;
        }

        public ValueTask<IReadOnlyList<ContentReportProperty>> GetPropertiesAsync(ContentTypePartDefinition typePart, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IReadOnlyList<ContentReportProperty>>(typePart?.Name == _partName ? _properties : []);
        }
    }
}
