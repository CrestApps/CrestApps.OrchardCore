using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.PersonalData;
using CrestApps.OrchardCore.Fundraising.Models;
using CrestApps.OrchardCore.Fundraising.Services;
using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Marketplace.Services;
using CrestApps.OrchardCore.Memberships.Models;
using CrestApps.OrchardCore.Memberships.Services;
using CrestApps.OrchardCore.Reviews;
using CrestApps.OrchardCore.Reviews.Indexes;
using CrestApps.OrchardCore.Reviews.Migrations;
using CrestApps.OrchardCore.Reviews.Models;
using CrestApps.OrchardCore.Reviews.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Moq;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// The commerce verticals take part in a person's data export and erasure: donations, memberships, vendors they run,
/// and their product reviews are exported and anonymized.
/// </summary>
public sealed class VerticalPersonalDataContributorTests : IAsyncLifetime
{
    private const string UserId = "user-1";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-pd-reviews-{Guid.NewGuid():N}.db");
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
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ADonorsDonations_AreExported_AndAnonymizedKeepingTheAmount()
    {
        var donation = new Donation { ItemId = "d1", DonorUserId = UserId, DonorName = "Ann Lee", DonorEmail = "ann@example.com", Amount = 25m, Tribute = "In memory of Ann Lee's father", ReceiptNumber = "R-1" };
        var store = new Mock<IDonationStore>();
        store.Setup(value => value.ListByDonorAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync([donation]);
        store.Setup(value => value.UpdateAsync(It.IsAny<Donation>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        var contributor = new DonationPersonalDataContributor(store.Object);
        var subject = Subject();

        await contributor.IdentifyAsync(subject, TestContext.Current.CancellationToken);

        var record = Assert.Single(await ExportAsync(contributor, subject));
        var result = await AnonymizeAsync(contributor, subject);

        Assert.Equal(("Donation", "ann@example.com"), (record.Kind, record.Values[nameof(Donation.DonorEmail)]));
        Assert.Equal(1, result.Records[DonationPersonalDataContributor.RecordKind]);
        Assert.Equal("[removed]", donation.DonorName);
        Assert.Null(donation.DonorEmail);
        Assert.DoesNotContain("Ann Lee", donation.Tribute, StringComparison.Ordinal);
        Assert.Equal(25m, donation.Amount);
    }

    [Fact]
    public async Task AMembersMemberships_AreExported_AndTheirUserNameIsAnonymized()
    {
        var membership = new Membership { ItemId = "m1", UserId = UserId, UserName = "ann", Title = "Gold", RoleNames = ["Gold"], Status = MembershipStatus.Active };
        var store = new Mock<IMembershipStore>();
        store.Setup(value => value.ListByUserAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync([membership]);
        store.Setup(value => value.UpdateAsync(It.IsAny<Membership>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        var contributor = new MembershipPersonalDataContributor(store.Object);
        var subject = Subject();

        var record = Assert.Single(await ExportAsync(contributor, subject));
        var result = await AnonymizeAsync(contributor, subject);

        Assert.Equal(("Membership", "ann", "Gold"), (record.Kind, record.Values[nameof(Membership.UserName)], record.Values[nameof(Membership.RoleNames)]));
        Assert.Equal(1, result.Records[MembershipPersonalDataContributor.RecordKind]);
        Assert.Equal("[removed]", membership.UserName);
    }

    [Fact]
    public async Task APersonWhoRunsAVendor_IsExported_AndTakenOffItWhenAnonymized()
    {
        var vendor = new Vendor { ItemId = "v1", Name = "Northwind Pottery", Email = "ann@example.com", UserIds = ["other", UserId], UserNames = ["bob", "ann"] };
        var store = new Mock<IVendorStore>();
        store.Setup(value => value.ListByUserAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync([vendor]);
        store.Setup(value => value.UpdateAsync(It.IsAny<Vendor>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        var contributor = new VendorPersonalDataContributor(store.Object);
        var subject = Subject();

        var record = Assert.Single(await ExportAsync(contributor, subject));
        var result = await AnonymizeAsync(contributor, subject);

        Assert.Equal(("Vendor", "ann"), (record.Kind, record.Values["UserName"]));
        Assert.Equal(1, result.Records[VendorPersonalDataContributor.RecordKind]);
        Assert.Equal(["other"], vendor.UserIds);
        Assert.Equal(["bob"], vendor.UserNames);
        Assert.Null(vendor.Email);
        Assert.Equal("Northwind Pottery", vendor.Name);
    }

    [Fact]
    public async Task AReviewersReviews_AreExported_AndAnonymizedKeepingTheRating()
    {
        await using (var session = _store.CreateSession())
        {
            await session.SaveAsync(new ProductReview { ItemId = "r1", ProductId = "watch", ProductTitle = "Watch", UserId = UserId, AuthorName = "Ann Lee", Rating = 4, Title = "Great", Body = "Ann Lee loves it", Status = ReviewStatus.Approved }, collection: ReviewsConstants.CollectionName, cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveAsync(new ProductReview { ItemId = "r2", ProductId = "watch", UserId = "someone-else", AuthorName = "Bob", Rating = 2 }, collection: ReviewsConstants.CollectionName, cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var subject = Subject();
        IReadOnlyList<CommercePersonalDataRecord> records;

        await using (var session = _store.CreateSession())
        {
            var contributor = new ReviewPersonalDataContributor(session);

            await contributor.IdentifyAsync(subject, TestContext.Current.CancellationToken);
            records = await ExportAsync(contributor, subject);

            var result = await AnonymizeAsync(contributor, subject);

            Assert.Equal(1, result.Records[ReviewPersonalDataContributor.RecordKind]);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            var review = await session.Query<ProductReview, ProductReviewIndex>(index => index.ItemId == "r1", collection: ReviewsConstants.CollectionName).FirstOrDefaultAsync(TestContext.Current.CancellationToken);

            Assert.Equal(("ProductReview", "Ann Lee"), (Assert.Single(records).Kind, records[0].Values[nameof(ProductReview.AuthorName)]));
            Assert.Equal("[removed]", review.AuthorName);
            Assert.DoesNotContain("Ann Lee", review.Body, StringComparison.Ordinal);
            Assert.Equal(4, review.Rating);
        }
    }

    private static CommercePersonalDataSubject Subject()
        => new(CustomerOwner.ForUser(UserId), "ann@example.com");

    private static async Task<IReadOnlyList<CommercePersonalDataRecord>> ExportAsync(ICommercePersonalDataContributor contributor, CommercePersonalDataSubject subject)
    {
        var export = new CommercePersonalDataExport();

        await contributor.ExportAsync(new CommercePersonalDataExportContext(subject, export), TestContext.Current.CancellationToken);

        return [.. export.Records];
    }

    private static async Task<CommercePersonalDataAnonymizeResult> AnonymizeAsync(ICommercePersonalDataContributor contributor, CommercePersonalDataSubject subject)
    {
        var result = new CommercePersonalDataAnonymizeResult();

        await contributor.AnonymizeAsync(new CommercePersonalDataAnonymizeContext(subject, "[removed]", result), TestContext.Current.CancellationToken);

        return result;
    }
}
