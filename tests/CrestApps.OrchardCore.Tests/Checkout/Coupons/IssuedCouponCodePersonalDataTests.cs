using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Migrations;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.PersonalData;
using CrestApps.OrchardCore.Tests.Utilities;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Checkout.Coupons;

/// <summary>
/// The codes issued to one address are found by a privacy request for that address through a real YesSql store: they
/// are exported with their dates, source and uses, and erasing them removes the address, turns off a code that could
/// still be used, and keeps the code and its idempotency key so the address can't get a second one.
/// </summary>
public sealed class IssuedCouponCodePersonalDataTests : IAsyncLifetime
{
    private static readonly DateTime _now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-issued-codes-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new CouponIndexProvider(), new CouponCodeIndexProvider(), new CouponRedemptionIndexProvider()]);
        await _store.InitializeAsync(TestContext.Current.CancellationToken);
        await _store.InitializeCollectionAsync(CheckoutConstants.CouponCollectionName, TestContext.Current.CancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var migration = new CouponMigrations { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) };

        await migration.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task AnExport_ListsTheCodesIssuedToTheAddress_InAnyCase()
    {
        // Arrange
        await AddCodeAsync(new CouponCode
        {
            ItemId = "code-1",
            Code = "WELCOME-AAAA2222",
            CustomerEmail = "pat@example.test",
            ExpiresUtc = _now.AddDays(30),
            Source = "MailingListMember:member-1",
            IdempotencyKey = "mailing-list:list-1:hash",
        });
        await AddCodeAsync(new CouponCode { ItemId = "code-2", Code = "WELCOME-BBBB3333", CustomerEmail = "sam@example.test" });
        await AddCodeAsync(new CouponCode { ItemId = "code-3", Code = "BULK-CCCC4444" });

        var export = new CommercePersonalDataExport();

        // Act
        await using (var session = _store.CreateSession())
        {
            await new IssuedCouponCodePersonalDataContributor(session).ExportAsync(
                new CommercePersonalDataExportContext(new CommercePersonalDataSubject(null, " Pat@Example.TEST "), export),
                TestContext.Current.CancellationToken);
        }

        // Assert
        var record = Assert.Single(export.Records);
        Assert.Equal(IssuedCouponCodePersonalDataContributor.RecordKind, record.Kind);
        Assert.Equal("code-1", record.Id);
        Assert.Equal(_now, record.CreatedUtc);
        Assert.Equal("WELCOME-AAAA2222", record.Values[nameof(CouponCode.Code)]);
        Assert.Equal("pat@example.test", record.Values[nameof(CouponCode.CustomerEmail)]);
        Assert.Equal("2026-11-01T12:00:00.0000000Z", record.Values["IssuedUtc"]);
        Assert.Equal("2026-12-01T12:00:00.0000000Z", record.Values[nameof(CouponCode.ExpiresUtc)]);
        Assert.Equal("MailingListMember:member-1", record.Values[nameof(CouponCode.Source)]);
        Assert.Equal("0", record.Values[nameof(CouponCode.RedemptionCount)]);
    }

    [Fact]
    public async Task AnErasure_RemovesTheAddress_TurnsOffAnUnusedCode_AndKeepsTheKey()
    {
        // Arrange
        await AddCodeAsync(new CouponCode
        {
            ItemId = "unused",
            Code = "WELCOME-AAAA2222",
            CustomerEmail = "pat@example.test",
            IdempotencyKey = "mailing-list:list-1:hash",
        });
        await AddCodeAsync(new CouponCode
        {
            ItemId = "used",
            Code = "WELCOME-BBBB3333",
            CustomerEmail = "pat@example.test",
            RedemptionCount = 1,
            IdempotencyKey = "mailing-list:list-2:hash",
        });
        await AddCodeAsync(new CouponCode { ItemId = "other", Code = "WELCOME-CCCC4444", CustomerEmail = "sam@example.test" });

        var result = new CommercePersonalDataAnonymizeResult();

        // Act
        await using (var session = _store.CreateSession())
        {
            await new IssuedCouponCodePersonalDataContributor(session).AnonymizeAsync(
                new CommercePersonalDataAnonymizeContext(new CommercePersonalDataSubject(null, "pat@example.test"), null, result),
                TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Assert
        var unused = await GetCodeAsync("unused");
        var used = await GetCodeAsync("used");
        var other = await GetCodeAsync("other");

        Assert.Equal(2, result.Records[IssuedCouponCodePersonalDataContributor.RecordKind]);

        Assert.Null(unused.CustomerEmail);
        Assert.False(unused.IsEnabled);
        Assert.Equal("mailing-list:list-1:hash", unused.IdempotencyKey);
        Assert.Equal("WELCOME-AAAA2222", unused.Code);

        // A used-up code stays as it was used, without the address.
        Assert.Null(used.CustomerEmail);
        Assert.True(used.IsEnabled);
        Assert.Equal(1, used.RedemptionCount);

        Assert.Equal("sam@example.test", other.CustomerEmail);
        Assert.True(other.IsEnabled);

        // The address is gone from the index too, so a second request finds nothing.
        await using var check = _store.CreateSession();
        var found = await check
            .QueryIndex<CouponCodeIndex>(index => index.NormalizedEmail == "pat@example.test", collection: CheckoutConstants.CouponCollectionName)
            .CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, found);
    }

    [Fact]
    public async Task ARequestByOwnerOnly_FindsNoCodes()
    {
        // Arrange
        await AddCodeAsync(new CouponCode { ItemId = "code-1", Code = "WELCOME-AAAA2222", CustomerEmail = "pat@example.test" });

        var export = new CommercePersonalDataExport();
        var subject = new CommercePersonalDataSubject(CustomerOwner.ForUser("user-1"), null);

        // Act
        await using (var session = _store.CreateSession())
        {
            await new IssuedCouponCodePersonalDataContributor(session).ExportAsync(new CommercePersonalDataExportContext(subject, export), TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Empty(export.Records);
        Assert.False(new IssuedCouponCodePersonalDataContributor(null).TakesPartInRetentionExpiry);
    }

    private async Task AddCodeAsync(CouponCode code)
    {
        code.CouponId = "WELCOME";
        code.CreatedUtc = _now;

        await using var session = _store.CreateSession();
        await session.SaveAsync(code, collection: CheckoutConstants.CouponCollectionName, cancellationToken: TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<CouponCode> GetCodeAsync(string itemId)
    {
        await using var session = _store.CreateSession();

        return await session
            .Query<CouponCode, CouponCodeIndex>(index => index.ItemId == itemId, collection: CheckoutConstants.CouponCollectionName)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
    }
}
