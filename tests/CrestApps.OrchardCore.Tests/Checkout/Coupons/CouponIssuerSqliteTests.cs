using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Migrations;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Locking;
using OrchardCore.Settings;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Checkout.Coupons;

/// <summary>
/// Codes issued one per person from a template coupon through a real YesSql store, each request in its own session as it
/// would be in its own web request: one code per idempotency key, even for two requests at once; an expiry that never
/// passes the template's end; and a code that works only for the address it is bound to.
/// </summary>
public sealed class CouponIssuerSqliteTests : IAsyncLifetime
{
    private static readonly DateTime _now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-coupon-issuer-{Guid.NewGuid():N}.db");
    private readonly LocalLock _lock = new(NullLogger<LocalLock>.Instance);
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
    public async Task IssuingTwiceWithTheSameKey_ReturnsTheSameCode_MarkedAlreadyIssued()
    {
        // Arrange
        await AddCouponAsync(new Coupon { Code = "WELCOME", OnlyGeneratedCodes = true });

        // Act
        var first = await IssueAsync(Request("WELCOME", "list-1:pat", "pat@example.test"));
        var second = await IssueAsync(Request("WELCOME", "list-1:pat", "other@example.test"));

        // Assert
        Assert.True(first.Succeeded);
        Assert.False(first.AlreadyIssued);
        Assert.Equal("WELCOME", first.Coupon.ItemId);
        Assert.Matches("^WELCOME-[A-HJ-NP-Z2-9]{8}$", first.Code.Code);
        Assert.Equal(1, first.Code.MaxRedemptions);
        Assert.Equal(_now, first.Code.CreatedUtc);
        Assert.Equal("pat@example.test", first.Code.CustomerEmail);
        Assert.Equal("MailingListMember:member-1", first.Code.Source);
        Assert.Equal("list-1:pat", first.Code.IdempotencyKey);

        Assert.True(second.Succeeded);
        Assert.True(second.AlreadyIssued);
        Assert.Equal(first.Code.ItemId, second.Code.ItemId);
        Assert.Equal(first.Code.Code, second.Code.Code);
        Assert.Equal("pat@example.test", second.Code.CustomerEmail);
        Assert.Equal(1, await CountCodesAsync("list-1:pat"));
    }

    [Fact]
    public async Task TwoRequestsWithTheSameKeyAtOnce_IssueOneCode()
    {
        // Arrange
        await AddCouponAsync(new Coupon { Code = "WELCOME" });

        // Act
        var results = await Task.WhenAll(
            IssueAsync(Request("WELCOME", "list-1:sam", "sam@example.test")),
            IssueAsync(Request("WELCOME", "list-1:sam", "sam@example.test")));

        // Assert
        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Single(results, result => result.AlreadyIssued);
        Assert.Equal(results[0].Code.Code, results[1].Code.Code);
        Assert.Equal(1, await CountCodesAsync("list-1:sam"));
    }

    [Fact]
    public async Task ADifferentKey_GetsItsOwnCode()
    {
        // Arrange
        await AddCouponAsync(new Coupon { Code = "WELCOME" });

        // Act
        var pat = await IssueAsync(Request("WELCOME", "list-1:pat", "pat@example.test"));
        var sam = await IssueAsync(Request("WELCOME", "list-1:sam", "sam@example.test"));

        // Assert
        Assert.False(pat.AlreadyIssued);
        Assert.False(sam.AlreadyIssued);
        Assert.NotEqual(pat.Code.Code, sam.Code.Code);
    }

    [Fact]
    public async Task TheCodesExpiry_IsTheEarlierOfItsOwnTimeAndTheTemplatesEnd()
    {
        // Arrange
        await AddCouponAsync(new Coupon { Code = "ENDSSOON", EndsUtc = _now.AddDays(10) });
        await AddCouponAsync(new Coupon { Code = "ENDSLATE", EndsUtc = _now.AddDays(60) });
        await AddCouponAsync(new Coupon { Code = "NOEND" });

        // Act
        var cappedByTemplate = await IssueAsync(Request("ENDSSOON", "a", validFor: TimeSpan.FromDays(30)));
        var ownTime = await IssueAsync(Request("ENDSLATE", "b", validFor: TimeSpan.FromDays(30)));
        var templateEnd = await IssueAsync(Request("ENDSSOON", "c"));
        var noTemplateEnd = await IssueAsync(Request("NOEND", "d", validFor: TimeSpan.FromDays(7)));
        var neither = await IssueAsync(Request("NOEND", "e"));

        // Assert
        Assert.Equal(_now.AddDays(10), cappedByTemplate.Code.ExpiresUtc);
        Assert.Equal(_now.AddDays(30), ownTime.Code.ExpiresUtc);
        Assert.Equal(_now.AddDays(10), templateEnd.Code.ExpiresUtc);
        Assert.Equal(_now.AddDays(7), noTemplateEnd.Code.ExpiresUtc);
        Assert.Null(neither.Code.ExpiresUtc);
    }

    [Fact]
    public async Task AnAutomaticDisabledEndedOrMissingTemplate_IsRefused_AndIssuesNothing()
    {
        // Arrange
        await AddCouponAsync(new Coupon { Code = "AUTO", Method = CouponMethod.Automatic });
        await AddCouponAsync(new Coupon { Code = "OFF" }, isEnabled: false);
        await AddCouponAsync(new Coupon { Code = "ENDED", EndsUtc = _now.AddDays(-1) });
        await AddCouponAsync(new Coupon { Code = "NOTYET", StartsUtc = _now.AddDays(1) });

        // Act
        var results = new[]
        {
            await IssueAsync(Request("AUTO", "auto")),
            await IssueAsync(Request("OFF", "off")),
            await IssueAsync(Request("ENDED", "ended")),
            await IssueAsync(Request("NOTYET", "not-yet")),
            await IssueAsync(Request("MISSING", "missing")),
        };

        // Assert
        Assert.All(results, result =>
        {
            Assert.False(result.Succeeded);
            Assert.False(string.IsNullOrEmpty(result.Error));
            Assert.Null(result.Code);
        });

        await using var session = _store.CreateSession();
        var codes = await session
            .QueryIndex<CouponCodeIndex>(index => index.RedemptionCount >= 0, collection: CheckoutConstants.CouponCollectionName)
            .CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, codes);
    }

    [Fact]
    public async Task ARequestWithoutACouponOrKey_OrWithABadAddressPrefixOrTime_IsRefused()
    {
        // Arrange
        await AddCouponAsync(new Coupon { Code = "WELCOME" });

        // Act
        var results = new[]
        {
            await IssueAsync(Request(null, "key")),
            await IssueAsync(Request("WELCOME", " ")),
            await IssueAsync(Request("WELCOME", new string('k', CheckoutConstants.MaxCouponIdempotencyKeyLength + 1))),
            await IssueAsync(Request("WELCOME", "key", "not an address")),
            await IssueAsync(Request("WELCOME", "key", validFor: TimeSpan.Zero)),
            await IssueAsync(new CouponIssueRequest { CouponId = "WELCOME", IdempotencyKey = "key", Prefix = "WELCOME_OFFER!" }),
        };

        // Assert
        Assert.All(results, result =>
        {
            Assert.False(result.Succeeded);
            Assert.False(string.IsNullOrEmpty(result.Error));
        });

        Assert.Equal(0, await CountCodesAsync("key"));
    }

    [Fact]
    public async Task ARepeatedKey_ReturnsTheCode_EvenAfterTheTemplateWasTurnedOff()
    {
        // Arrange
        await AddCouponAsync(new Coupon { Code = "WELCOME" });

        var first = await IssueAsync(Request("WELCOME", "list-1:pat", "pat@example.test"));

        await using (var session = _store.CreateSession())
        {
            var store = new CouponStore(session, new TestClock(_now));
            var coupon = await store.FindByIdAsync("WELCOME", TestContext.Current.CancellationToken);
            coupon.IsEnabled = false;

            await store.UpdateAsync(coupon, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act
        var second = await IssueAsync(Request("WELCOME", "list-1:pat", "pat@example.test"));

        // Assert
        Assert.True(second.Succeeded);
        Assert.True(second.AlreadyIssued);
        Assert.Equal(first.Code.Code, second.Code.Code);
    }

    [Fact]
    public async Task AnIssuedCode_IsBoundToItsAddress_WhoeverTheTemplateWasFor()
    {
        // Arrange
        await AddCouponAsync(new Coupon { Code = "WELCOME", CustomerId = "user-1", CustomerEmail = "owner@example.test" });

        var issued = await IssueAsync(Request("WELCOME", "list-1:pat", " Pat@Example.TEST "));
        var evaluator = new CouponScopeEvaluator(new ServiceCollection().BuildServiceProvider());

        // Act
        var coupon = await GetCouponAsync(issued.Code.Code.ToLowerInvariant());

        // Assert
        Assert.Equal("pat@example.test", issued.Code.CustomerEmail);
        Assert.Equal(issued.Code.Code, coupon.Code);
        Assert.Equal("pat@example.test", coupon.CustomerEmail);
        Assert.Null(coupon.CustomerId);
        Assert.True(await evaluator.IsInScopeAsync(coupon, Flow("session-a", "PAT@example.test "), TestContext.Current.CancellationToken));
        Assert.False(await evaluator.IsInScopeAsync(coupon, Flow("session-b", "owner@example.test"), TestContext.Current.CancellationToken));
        Assert.False(await evaluator.IsInScopeAsync(coupon, Flow("session-c", "sam@example.test"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnIssuedCode_StopsWorkingAtItsExpiry()
    {
        // Arrange
        await AddCouponAsync(new Coupon { Code = "WELCOME", EndsUtc = _now.AddDays(90) });

        var issued = await IssueAsync(Request("WELCOME", "list-1:pat", "pat@example.test", TimeSpan.FromDays(30)));
        var beforeExpiry = _now.AddDays(29);
        var afterExpiry = _now.AddDays(31);

        // Act
        var coupon = await GetCouponAsync(issued.Code.Code);
        var availableBefore = await GetAvailabilityAsync(coupon, beforeExpiry);
        var availableAfter = await GetAvailabilityAsync(coupon, afterExpiry);

        // Assert
        Assert.Equal(_now.AddDays(30), coupon.EndsUtc);
        Assert.True(coupon.IsRedeemable(beforeExpiry));
        Assert.False(coupon.IsRedeemable(afterExpiry));
        Assert.Equal(CouponAvailability.Available, availableBefore);
        Assert.Equal(CouponAvailability.Unavailable, availableAfter);
    }

    [Fact]
    public async Task AGeneratedCodesOwnValues_LieOverItsCoupon_AndTheEarlierEndWins()
    {
        // Arrange
        await AddCouponAsync(new Coupon { Code = "PARENT", CustomerId = "user-1", CustomerEmail = "parent@example.test", EndsUtc = _now.AddDays(10) });
        await AddCodeAsync(new CouponCode { Code = "BOUND-EARLY", CustomerEmail = "Bound@Example.test", ExpiresUtc = _now.AddDays(5) });
        await AddCodeAsync(new CouponCode { Code = "LATE", ExpiresUtc = _now.AddDays(20) });
        await AddCodeAsync(new CouponCode { Code = "PLAIN" });

        // Act
        var boundEarly = await GetCouponAsync("BOUND-EARLY");
        var late = await GetCouponAsync("LATE");
        var plain = await GetCouponAsync("PLAIN");
        var parent = await GetCouponAsync("PARENT");

        // Assert
        Assert.Equal("bound@example.test", boundEarly.CustomerEmail);
        Assert.Null(boundEarly.CustomerId);
        Assert.Equal(_now.AddDays(5), boundEarly.EndsUtc);

        Assert.Equal("parent@example.test", late.CustomerEmail);
        Assert.Equal("user-1", late.CustomerId);
        Assert.Equal(_now.AddDays(10), late.EndsUtc);

        Assert.Equal("parent@example.test", plain.CustomerEmail);
        Assert.Equal(_now.AddDays(10), plain.EndsUtc);

        // The coupon itself keeps its own values.
        Assert.Equal("parent@example.test", parent.CustomerEmail);
        Assert.Equal("user-1", parent.CustomerId);
        Assert.Equal(_now.AddDays(10), parent.EndsUtc);
    }

    private static CouponIssueRequest Request(string couponId, string key, string email = null, TimeSpan? validFor = null)
        => new()
        {
            CouponId = couponId,
            IdempotencyKey = key,
            Prefix = "welcome-",
            CustomerEmail = email,
            ValidFor = validFor,
            Source = new RecordRef("MailingListMember", "member-1"),
        };

    private async Task<CouponIssueResult> IssueAsync(CouponIssueRequest request)
    {
        await using var session = _store.CreateSession();

        var clock = new TestClock(_now);
        var issuer = new DefaultCouponIssuer(
            new CouponStore(session, clock),
            session,
            _lock,
            clock,
            NullLogger<DefaultCouponIssuer>.Instance,
            new PassThroughStringLocalizer<DefaultCouponIssuer>());

        return await issuer.IssueAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task AddCouponAsync(Coupon coupon, bool isEnabled = true)
    {
        coupon.ItemId = coupon.Code;
        coupon.Kind = CouponKind.Percentage;
        coupon.Percentage = 10m;
        coupon.IsEnabled = isEnabled;

        await using var session = _store.CreateSession();
        await new CouponStore(session, new TestClock(_now)).CreateAsync(coupon, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task AddCodeAsync(CouponCode code)
    {
        code.ItemId = code.Code;
        code.CouponId = "PARENT";
        code.CreatedUtc = _now;

        await using var session = _store.CreateSession();
        await session.SaveAsync(code, collection: CheckoutConstants.CouponCollectionName, cancellationToken: TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Coupon> GetCouponAsync(string code)
    {
        await using var session = _store.CreateSession();

        return await new CouponStore(session, new TestClock(_now)).GetByCodeAsync(code, TestContext.Current.CancellationToken);
    }

    private async Task<int> CountCodesAsync(string key)
    {
        await using var session = _store.CreateSession();

        return await session
            .QueryIndex<CouponCodeIndex>(index => index.IdempotencyKey == key, collection: CheckoutConstants.CouponCollectionName)
            .CountAsync(TestContext.Current.CancellationToken);
    }

    private async Task<CouponAvailability> GetAvailabilityAsync(Coupon coupon, DateTime utcNow)
    {
        await using var session = _store.CreateSession();

        var clock = new TestClock(utcNow);
        var service = new CouponRedemptionService(
            new CouponStore(session, clock),
            session,
            _lock,
            Mock.Of<ISiteService>(),
            new ServiceCollection().BuildServiceProvider(),
            clock,
            NullLogger<CouponRedemptionService>.Instance);

        return await service.GetAvailabilityAsync(coupon, Flow("session-a", "pat@example.test"), TestContext.Current.CancellationToken);
    }

    private static CheckoutFlow Flow(string sessionId, string email)
    {
        var session = new CheckoutSession { SessionId = sessionId, ReferenceType = "Order", ReferenceId = "order-" + sessionId, Currency = "USD" };

        session.Put(new CheckoutContactInfo { Email = email });

        return new CheckoutFlow(session);
    }
}
