using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Reviews;
using CrestApps.OrchardCore.Reviews.Indexes;
using CrestApps.OrchardCore.Reviews.Migrations;
using CrestApps.OrchardCore.Reviews.Models;
using CrestApps.OrchardCore.Reviews.Services;
using CrestApps.OrchardCore.Tests.Customers;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Products;

/// <summary>
/// A buyer reviews a product; once approved, its rating counts and goes into the product's search record, where
/// listings filter and sort by it.
/// </summary>
public sealed class ProductReviewsTests : IAsyncLifetime
{
    private static readonly DateTime _now = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-reviews-{Guid.NewGuid():N}.db");
    private readonly InMemoryOrderStore _orders = new();
    private readonly List<string> _refreshed = [];
    private ReviewSettings _settings = new();
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new ProductReviewIndexProvider()], ReviewsConstants.CollectionName);
        await _store.InitializeAsync(TestContext.Current.CancellationToken);
        await _store.InitializeCollectionAsync(ReviewsConstants.CollectionName, TestContext.Current.CancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await new ReviewMigrations { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        _orders.Orders.Add(new Order
        {
            ItemId = "order-1",
            UserId = "buyer",
            Status = OrderStatus.Paid,
            CreatedUtc = _now,
            Lines = [new OrderLine { LineId = "l1", ContentItemId = "watch", Title = "Watch", Quantity = 1 }],
        });
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ABuyersReview_CountsOnceApproved_AndRefreshesTheProductsListingRecord()
    {
        var review = await InSessionAsync(service => service.SubmitAsync(Submission("buyer", 4), TestContext.Current.CancellationToken));

        Assert.Equal(ReviewStatus.Pending, review.Status);
        Assert.True(review.VerifiedPurchase);
        Assert.Equal("order-1", review.OrderId);
        Assert.Equal(ProductRating.None, await InSessionAsync(service => service.GetRatingAsync("watch", TestContext.Current.CancellationToken)));
        Assert.Empty(_refreshed);

        await InSessionAsync(async service => { await service.ModerateAsync(await service.FindAsync(review.ItemId, TestContext.Current.CancellationToken), ReviewStatus.Approved, "admin", TestContext.Current.CancellationToken); return 0; });

        Assert.Equal(new ProductRating(1, 4m), await InSessionAsync(service => service.GetRatingAsync("watch", TestContext.Current.CancellationToken)));
        Assert.Equal(["watch"], _refreshed);
    }

    [Fact]
    public async Task OnlyBuyers_CanReview_UnlessTheSiteLetsAnyone()
    {
        Assert.Null(await InSessionAsync(service => service.SubmitAsync(Submission("stranger", 5), TestContext.Current.CancellationToken)));

        _settings = new ReviewSettings { BuyersOnly = false, RequireApproval = false };

        var review = await InSessionAsync(service => service.SubmitAsync(Submission("stranger", 5), TestContext.Current.CancellationToken));

        Assert.Equal(ReviewStatus.Approved, review.Status);
        Assert.False(review.VerifiedPurchase);
    }

    [Fact]
    public async Task ReviewingAgain_ChangesTheSameReview_AndSendsItForApprovalAgain()
    {
        var first = await InSessionAsync(service => service.SubmitAsync(Submission("buyer", 2), TestContext.Current.CancellationToken));
        await InSessionAsync(async service => { await service.ModerateAsync(await service.FindAsync(first.ItemId, TestContext.Current.CancellationToken), ReviewStatus.Approved, "admin", TestContext.Current.CancellationToken); return 0; });

        var second = await InSessionAsync(service => service.SubmitAsync(Submission("buyer", 5), TestContext.Current.CancellationToken));

        Assert.Equal(first.ItemId, second.ItemId);
        Assert.Equal(ReviewStatus.Pending, second.Status);
        Assert.Equal(1, (await InSessionAsync(service => service.PageAsync(null, "watch", 0, 10, TestContext.Current.CancellationToken))).Count);
        Assert.Equal(ProductRating.None, await InSessionAsync(service => service.GetRatingAsync("watch", TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task TheRating_GoesIntoTheSearchRecord_AndListingsOfferItAsAFilterAndASort()
    {
        _settings = new ReviewSettings { BuyersOnly = false, RequireApproval = false };

        await InSessionAsync(service => service.SubmitAsync(Submission("a", 5), TestContext.Current.CancellationToken));
        await InSessionAsync(service => service.SubmitAsync(Submission("b", 4), TestContext.Current.CancellationToken));

        var document = new ProductSearchDocument();
        await using (var session = _store.CreateSession())
        {
            await new ReviewSearchDocumentContributor(new ProductRatingReader(session)).ContributeAsync(new ContentItem { ContentItemId = "watch" }, document);
        }

        Assert.Equal(4.5m, document.Attributes.Single(value => value.Key == ProductAttributeKeys.Rating).Number);
        Assert.Equal(2m, document.Attributes.Single(value => value.Key == ProductAttributeKeys.ReviewCount).Number);

        var entries = new ReviewListingEntryProvider(new PassThroughLocalizer<ReviewListingEntryProvider>());
        var facet = Assert.Single(entries.GetFacets());
        var sort = Assert.Single(entries.GetSorts());

        Assert.Equal(ProductAttributeKeys.Rating, facet.AttributeKey);
        Assert.Equal(ProductFacetKind.Range, facet.Kind);
        Assert.Equal(4m, facet.Ranges[0].Min);
        Assert.True(sort.Descending);
        Assert.Equal(ProductAttributeKeys.Rating, sort.AttributeKey);
    }

    private async Task<T> InSessionAsync<T>(Func<ReviewService, Task<T>> work)
    {
        await using var session = _store.CreateSession();

        var site = new Mock<ISite>();
        site.Setup(value => value.GetOrCreate<ReviewSettings>()).Returns(() => _settings);
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        var refresher = new Mock<IProductSearchIndexRefresher>();
        refresher
            .Setup(value => value.RefreshAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<string>, CancellationToken>((ids, _) => _refreshed.AddRange(ids))
            .Returns(Task.CompletedTask);

        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);

        var result = await work(new ReviewService(session, _orders, siteService.Object, [refresher.Object], clock.Object));

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return result;
    }

    private static ReviewSubmission Submission(string userId, int rating)
        => new() { ProductId = "watch", ProductTitle = "Watch", UserId = userId, AuthorName = userId, Rating = rating, Title = "Nice", Body = "Keeps time." };

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
