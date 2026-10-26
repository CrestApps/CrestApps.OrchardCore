using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reviews;
using CrestApps.OrchardCore.Reviews.Indexes;
using CrestApps.OrchardCore.Reviews.Migrations;
using CrestApps.OrchardCore.Reviews.Models;
using CrestApps.OrchardCore.Reviews.Reports.DataSources;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.DataSources;
using OrchardCore.Security;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Reviews;

/// <summary>
/// The reviews data source reads real reviews from a SQLite store, so the index query, the fields it exposes, the
/// push-down of dates, join keys and statuses, the grouping in the database, and the permission it requires are all
/// exercised end to end.
/// </summary>
public sealed class ReviewReportDataSourceTests : IAsyncLifetime
{
    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("u1", "Manager");
    private static readonly DateTime _start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"review-reports-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new ProductReviewIndexProvider()], ReviewsConstants.CollectionName);

        await _store.InitializeAsync(cancellationToken);
        await _store.InitializeCollectionAsync(ReviewsConstants.CollectionName, cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

            await new ReviewMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();

            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            // Four reviews, one a day: two of the mug, two of the cap.
            await session.SaveAsync(Review(0, "product-mug", "Mug", "user-1", ReviewStatus.Approved, rating: 5, verified: true, orderId: "order-1"), false, ReviewsConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Review(1, "product-mug", "Mug", "user-2", ReviewStatus.Pending, rating: 3, verified: false, orderId: null), false, ReviewsConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Review(2, "product-cap", "Cap", "user-1", ReviewStatus.Rejected, rating: 1, verified: false, orderId: null), false, ReviewsConstants.CollectionName, cancellationToken);
            await session.SaveAsync(Review(3, "product-cap", "Cap", "user-3", ReviewStatus.Approved, rating: 4, verified: true, orderId: "order-3"), false, ReviewsConstants.CollectionName, cancellationToken);

            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task WithoutManageReviews_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, grants: [ReviewsPermissions.SubmitReviews.Name]);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.ReviewSource.Reviews, context, TestContext.Current.CancellationToken);
        var table = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.ReviewSource.Reviews), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public async Task WithoutAUser_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext();
        var query = Query(CommerceReportDataSets.ReviewSource.Reviews);
        query.Context = context;

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.ReviewSource.Reviews, context, TestContext.Current.CancellationToken);
        var table = await ReportDesignerTestServices.ReadAsync(source, query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public async Task Schema_DeclaresKeysAndReferences_AndNeverExposesTheReviewText()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.ReviewSource.Reviews, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([CommerceReportDataSets.ReviewSource.Reviews], dataSets.Select(dataSet => dataSet.Name));
        Assert.Equal(ProductReviewsReportDataSet.CreatedUtcField, schema.DataSet.DefaultDateField);
        Assert.True(schema.FindField(CommerceReportDataSets.ItemIdField).IsIdentifier);
        Assert.True(schema.FindField(CommerceReportDataSets.ItemIdField).IsKeyFilterable);
        Assert.True(schema.FindField("ProductId").IsKeyFilterable);
        Assert.True(schema.FindField("UserId").IsKeyFilterable);
        Assert.False(schema.FindField("OrderId").IsKeyFilterable);

        var product = Assert.Single(schema.FindField("ProductId").References);
        Assert.Equal(CommerceReportDataSets.ProductSource.Name, product.Source);
        Assert.Equal(CommerceReportDataSets.ProductSource.Products, product.DataSet);
        Assert.Equal(CommerceReportDataSets.ContentItemIdField, product.Field);

        var user = Assert.Single(schema.FindField("UserId").References);
        Assert.Equal(ReportsConstants.UsersDataSource, user.Source);
        Assert.Equal(ReportsConstants.UserIdField, user.Field);

        var order = Assert.Single(schema.FindField("OrderId").References);
        Assert.Equal(CommerceReportDataSets.OrderSource.Name, order.Source);
        Assert.Equal(CommerceReportDataSets.OrderSource.Orders, order.DataSet);

        Assert.Equal(DataFieldType.Integer, schema.FindField("Rating").Type);
        Assert.DoesNotContain(schema.Fields, field => field.Name is "Body" or "Photos" or "ModeratedBy");
    }

    [Fact]
    public async Task Query_ReturnsTypedValues_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);

        // Act
        var table = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.ReviewSource.Reviews), TestContext.Current.CancellationToken);
        var rows = Rows(table);

        // Assert
        Assert.Equal(["review-3", "review-2", "review-1", "review-0"], rows.Select(row => row[CommerceReportDataSets.ItemIdField]));

        var first = rows[3];
        Assert.Equal(5L, first["Rating"]);
        Assert.Equal("Great mug", first["Title"]);
        Assert.Equal("Approved", first["Status"]);
        Assert.Equal(true, first["IsApproved"]);
        Assert.Equal(true, first["VerifiedPurchase"]);
        Assert.Equal(1L, first["PhotoCount"]);
        Assert.Equal("product-mug", first["ProductId"]);
        Assert.Equal("Mug", first["ProductTitle"]);
        Assert.Equal("user-1", first["UserId"]);
        Assert.Equal("Ada", first["AuthorName"]);
        Assert.Equal("order-1", first["OrderId"]);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(first["CreatedUtc"]).Kind);
        Assert.Equal(_start, first["CreatedUtc"]);

        var pending = rows[2];
        Assert.Equal("Pending", pending["Status"]);
        Assert.Equal(false, pending["IsApproved"]);
        Assert.Null(pending["OrderId"]);
        Assert.Null(pending["ModeratedUtc"]);

        Assert.DoesNotContain(table.Rows.SelectMany(row => row), value => value is string text && text.Contains("private", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Query_PushDownNeverDropsAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var dates = Query(CommerceReportDataSets.ReviewSource.Reviews, CommerceReportDataSets.ItemIdField);
        dates.Conditions.Add(Condition(ProductReviewsReportDataSet.CreatedUtcField, DataFilterOperator.Between, _start.AddDays(1), _start.AddDays(2)));
        var keys = Query(CommerceReportDataSets.ReviewSource.Reviews, CommerceReportDataSets.ItemIdField);
        keys.Conditions.Add(new DataCondition { Field = "ProductId", Operator = DataFilterOperator.In, Values = ["product-mug"], IsJoinKey = true });
        var users = Query(CommerceReportDataSets.ReviewSource.Reviews, CommerceReportDataSets.ItemIdField);
        users.Conditions.Add(new DataCondition { Field = "UserId", Operator = DataFilterOperator.In, Values = ["user-1"], IsJoinKey = true });
        var status = Query(CommerceReportDataSets.ReviewSource.Reviews, CommerceReportDataSets.ItemIdField);
        status.Conditions.Add(Condition("Status", DataFilterOperator.In, "approved"));
        var none = Query(CommerceReportDataSets.ReviewSource.Reviews, CommerceReportDataSets.ItemIdField);
        none.Conditions.Add(new DataCondition { Field = CommerceReportDataSets.ItemIdField, Operator = DataFilterOperator.In, Values = [], IsJoinKey = true });

        // Act
        var dateRows = Rows(await ReportDesignerTestServices.ReadAsync(source, dates, TestContext.Current.CancellationToken));
        var keyRows = Rows(await ReportDesignerTestServices.ReadAsync(source, keys, TestContext.Current.CancellationToken));
        var userRows = Rows(await ReportDesignerTestServices.ReadAsync(source, users, TestContext.Current.CancellationToken));
        var statusRows = Rows(await ReportDesignerTestServices.ReadAsync(source, status, TestContext.Current.CancellationToken));
        var noRows = Rows(await ReportDesignerTestServices.ReadAsync(source, none, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["review-2", "review-1"], dateRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["review-1", "review-0"], keyRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["review-2", "review-0"], userRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Equal(["review-3", "review-0"], statusRows.Select(row => row[CommerceReportDataSets.ItemIdField]));
        Assert.Empty(noRows);
    }

    [Fact]
    public async Task Aggregate_CountsByStatus_AndSumsStarsByProduct_InTheDatabase()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session);
        var byStatus = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.ReviewSource.Reviews,
            Groups = [new DataAggregateGroup { Field = "Status" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };
        var byProduct = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.ReviewSource.Reviews,
            Groups = [new DataAggregateGroup { Field = "ProductId" }],
            Measures = [new DataAggregateMeasure { Field = "Rating", Function = DataAggregate.Sum }],
            Context = new DataSourceContext { User = _user },
        };

        // Act
        var statuses = await ((IDataAggregateSource)source).AggregateAsync(byStatus, TestContext.Current.CancellationToken);
        var products = await ((IDataAggregateSource)source).AggregateAsync(byProduct, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(statuses);
        var counts = statuses.ToDictionary(row => (string)row[0], row => Convert.ToInt64(row[1], CultureInfo.InvariantCulture));
        Assert.Equal(2L, counts["Approved"]);
        Assert.Equal(1L, counts["Pending"]);
        Assert.Equal(1L, counts["Rejected"]);

        Assert.NotNull(products);
        var stars = products.ToDictionary(row => (string)row[0], row => Convert.ToInt64(row[1], CultureInfo.InvariantCulture));
        Assert.Equal(8L, stars["product-mug"]);
        Assert.Equal(5L, stars["product-cap"]);
    }

    [Fact]
    public async Task ReportIndexMigration_CanRunAgain()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

        // Act
        var first = await new ReviewMigrations { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();
        var second = await new ReviewMigrations { SchemaBuilder = schemaBuilder }.UpdateFrom1Async();

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, first);
        Assert.Equal(2, second);
    }

    [Fact]
    public async Task DataSourcesStartup_RegistersTheDataSource()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var services = new ServiceCollection();

        services.AddSingleton(session);
        services.AddSingleton(Authorization([ReviewsPermissions.ManageReviews.Name]));
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));

        // Act
        new DataSourcesStartup().ConfigureServices(services);

        await using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredService<ReviewReportDataSource>();
        var dataSets = await source.GetDataSetsAsync(new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CommerceReportDataSets.ReviewSource.Name, ReviewReportDataSource.Name);
        Assert.Equal([CommerceReportDataSets.ReviewSource.Reviews], dataSets.Select(dataSet => dataSet.Name));
    }

    private static ProductReview Review(int day, string productId, string productTitle, string userId, ReviewStatus status, int rating, bool verified, string orderId)
    {
        var written = _start.AddDays(day);

        return new ProductReview
        {
            ItemId = $"review-{day}",
            ProductId = productId,
            ProductTitle = productTitle,
            UserId = userId,
            AuthorName = "Ada",
            Rating = rating,
            Title = day == 0 ? "Great mug" : null,
            Body = "Private words about the product",
            Status = status,
            VerifiedPurchase = verified,
            OrderId = orderId,
            Photos = day == 0 ? ["reviews/private/photo.jpg"] : [],
            CreatedUtc = written,
            ModifiedUtc = written,
            ModeratedUtc = status == ReviewStatus.Pending ? null : written.AddHours(1),
            ModeratedBy = status == ReviewStatus.Pending ? null : "moderator",
        };
    }

    private static ReviewReportDataSource Source(ISession session, string[] grants = null)
    {
        var authorizationService = Authorization(grants ?? [ReviewsPermissions.ManageReviews.Name]);

        return new ReviewReportDataSource(
        [
            new ProductReviewsReportDataSet(session, authorizationService, new PassThroughStringLocalizer<ProductReviewsReportDataSet>()),
        ]);
    }

    private static IAuthorizationService Authorization(string[] grants)
    {
        var authorizationService = new Mock<IAuthorizationService>();

        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                requirements.OfType<PermissionRequirement>().All(requirement => grants.Contains(requirement.Permission.Name))
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        return authorizationService.Object;
    }

    private static DataCondition Condition(string field, DataFilterOperator filterOperator, params object[] values)
    {
        return new DataCondition
        {
            Field = field,
            Operator = filterOperator,
            Values = values,
        };
    }

    private static DataSourceQuery Query(string dataSet, params string[] fields)
    {
        return new DataSourceQuery
        {
            DataSet = dataSet,
            Fields = new HashSet<string>(fields, StringComparer.Ordinal),
            MaxRows = 100,
            Context = new DataSourceContext { User = _user },
        };
    }

    private static List<Dictionary<string, object>> Rows(DataRowSet table)
    {
        return table.Rows
            .Select(row => table.Fields.Select((field, index) => (field.Name, Value: row[index])).ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal))
            .ToList();
    }
}
