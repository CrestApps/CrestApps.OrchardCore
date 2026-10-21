using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Migrations;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
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

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// Coupon uses held, redeemed, let go and given back through a real YesSql store, each buyer in their own session as
/// they would be in their own request.
/// </summary>
public sealed class CouponRedemptionSqliteTests : IAsyncLifetime
{
    private static readonly DateTime _now = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-coupons-{Guid.NewGuid():N}.db");
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
    public async Task TwoBuyersRacingForTheLastUse_OnePays_AndTheOtherIsToldItRanOut()
    {
        await AddCouponAsync(new Coupon { Code = "LAST1", Percentage = 10m, MaxRedemptions = 1 });

        var first = await HoldAsync("LAST1", "session-a", "a@example.test");
        var second = await HoldAsync("LAST1", "session-b", "b@example.test");

        Assert.Equal(CouponAvailability.Available, first);
        Assert.Equal(CouponAvailability.UsedUp, second);

        await RedeemAsync("LAST1", "session-a", "a@example.test");

        Assert.Equal(1, (await GetCouponAsync("LAST1")).RedemptionCount);
        Assert.Equal(CouponAvailability.UsedUp, await HoldAsync("LAST1", "session-b", "b@example.test"));
    }

    [Fact]
    public async Task ACanceledCheckout_LetsItsUseGo_SoTheNextBuyerGetsIt()
    {
        await AddCouponAsync(new Coupon { Code = "LAST1", Percentage = 10m, MaxRedemptions = 1 });

        Assert.Equal(CouponAvailability.Available, await HoldAsync("LAST1", "session-a", "a@example.test"));

        await UseServiceAsync(service => service.ReleaseAsync("session-a", TestContext.Current.CancellationToken));

        Assert.Equal(CouponAvailability.Available, await HoldAsync("LAST1", "session-b", "b@example.test"));
    }

    [Fact]
    public async Task ACanceledOrder_GivesItsUseBack()
    {
        await AddCouponAsync(new Coupon { Code = "LAST1", Percentage = 10m, MaxRedemptions = 1 });

        await HoldAsync("LAST1", "session-a", "a@example.test");
        await RedeemAsync("LAST1", "session-a", "a@example.test");

        var returned = 0;

        await UseServiceAsync(async service => returned = await service.ReturnAsync("session-a", TestContext.Current.CancellationToken));

        Assert.Equal(1, returned);
        Assert.Equal(0, (await GetCouponAsync("LAST1")).RedemptionCount);
        Assert.Equal(CouponAvailability.Available, await HoldAsync("LAST1", "session-b", "b@example.test"));
    }

    [Fact]
    public async Task ACustomer_CanUseACodeOnlyAsOftenAsItAllowsOneCustomer_WhateverTheirCheckout()
    {
        await AddCouponAsync(new Coupon { Code = "ONCEEACH", Percentage = 10m, MaxRedemptionsPerCustomer = 1 });

        await HoldAsync("ONCEEACH", "session-a", "pat@example.test");
        await RedeemAsync("ONCEEACH", "session-a", "pat@example.test");

        // The same email in other capitals is the same customer; someone else can still use it.
        Assert.Equal(CouponAvailability.CustomerLimitReached, await HoldAsync("ONCEEACH", "session-b", "PAT@example.test"));
        Assert.Equal(CouponAvailability.Available, await HoldAsync("ONCEEACH", "session-c", "sam@example.test"));
    }

    [Fact]
    public async Task BeginningPaymentAgain_KeepsTheSameHold()
    {
        await AddCouponAsync(new Coupon { Code = "LAST1", Percentage = 10m, MaxRedemptions = 1 });

        Assert.Equal(CouponAvailability.Available, await HoldAsync("LAST1", "session-a", "a@example.test"));
        Assert.Equal(CouponAvailability.Available, await HoldAsync("LAST1", "session-a", "a@example.test"));

        await using var session = _store.CreateSession();
        var holds = await session.QueryIndex<CouponRedemptionIndex>(index => index.SessionId == "session-a", collection: CheckoutConstants.CouponCollectionName).CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, holds);
    }

    [Fact]
    public async Task RedeemingTwice_CountsTheUseOnce()
    {
        await AddCouponAsync(new Coupon { Code = "SAVE10", Percentage = 10m });

        await HoldAsync("SAVE10", "session-a", "a@example.test");
        await RedeemAsync("SAVE10", "session-a", "a@example.test");
        await RedeemAsync("SAVE10", "session-a", "a@example.test");

        Assert.Equal(1, (await GetCouponAsync("SAVE10")).RedemptionCount);
    }

    [Fact]
    public async Task AGeneratedCode_IsItsCoupon_WithItsOwnUses_AndTheCouponsOwnCodeCanBeTurnedOff()
    {
        await AddCouponAsync(new Coupon { Code = "SPRING", Percentage = 15m, OnlyGeneratedCodes = true, MaxRedemptions = 1 });

        IReadOnlyList<CouponCode> codes;

        await using (var session = _store.CreateSession())
        {
            var store = new CouponStore(session, new TestClock(_now));
            codes = await store.GenerateCodesAsync(await store.FindByIdAsync("SPRING", TestContext.Current.CancellationToken), 3, "sp-", usesPerCode: 1, TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(3, codes.Count);
        Assert.All(codes, code => Assert.Matches("^SP-[A-HJ-NP-Z2-9]{8}$", code.Code));

        var first = await GetCouponAsync(codes[0].Code.ToLowerInvariant());
        Assert.Equal(15m, first.Percentage);
        Assert.Equal(codes[0].Code, first.Code);
        Assert.True(first.CanBeTyped);
        Assert.False((await GetCouponAsync("SPRING")).CanBeTyped);

        // A use counts against the code it was made with, never the coupon or the other codes.
        Assert.Equal(CouponAvailability.Available, await HoldAsync(codes[0].Code, "session-a", "a@example.test"));
        await RedeemAsync(codes[0].Code, "session-a", "a@example.test");

        Assert.Equal(1, (await GetCouponAsync(codes[0].Code)).RedemptionCount);
        Assert.Equal(0, (await GetCouponAsync("SPRING")).RedemptionCount);
        Assert.Equal(CouponAvailability.UsedUp, await HoldAsync(codes[0].Code, "session-b", "b@example.test"));
        Assert.Equal(CouponAvailability.Available, await HoldAsync(codes[1].Code, "session-b", "b@example.test"));

        await using (var session = _store.CreateSession())
        {
            var store = new CouponStore(session, new TestClock(_now));
            Assert.Equal((3, 1), await store.CountGeneratedCodesAsync("SPRING", TestContext.Current.CancellationToken));

            await store.DeleteGeneratedCodesAsync("SPRING", TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Null(await GetCouponAsync(codes[2].Code));
    }

    private async Task AddCouponAsync(Coupon coupon)
    {
        coupon.ItemId = coupon.Code;
        coupon.Kind = CouponKind.Percentage;
        coupon.IsEnabled = true;

        await using var session = _store.CreateSession();
        await new CouponStore(session, new TestClock(_now)).CreateAsync(coupon, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Coupon> GetCouponAsync(string code)
    {
        await using var session = _store.CreateSession();

        return await new CouponStore(session, new TestClock(_now)).GetByCodeAsync(code, TestContext.Current.CancellationToken);
    }

    private async Task<CouponAvailability> HoldAsync(string code, string sessionId, string email)
    {
        var availability = CouponAvailability.Busy;

        await UseServiceAsync(async service => availability = await service.HoldAsync(Flow(sessionId, email), Discount(code), TestContext.Current.CancellationToken));

        return availability;
    }

    private Task RedeemAsync(string code, string sessionId, string email)
        => UseServiceAsync(service => service.RedeemAsync(Flow(sessionId, email), Discount(code), TestContext.Current.CancellationToken));

    private async Task UseServiceAsync(Func<CouponRedemptionService, Task> use)
    {
        await using var session = _store.CreateSession();

        var site = new Mock<ISite>();
        site.Setup(value => value.GetOrCreate<CheckoutSettings>()).Returns(new CheckoutSettings { SessionLifetimeHours = 24 });

        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        var service = new CouponRedemptionService(
            new CouponStore(session, new TestClock(_now)),
            session,
            _lock,
            siteService.Object,
            new ServiceCollection().BuildServiceProvider(),
            new TestClock(_now),
            NullLogger<CouponRedemptionService>.Instance);

        await use(service);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static CheckoutFlow Flow(string sessionId, string email)
    {
        var session = new CheckoutSession { SessionId = sessionId, ReferenceType = "Order", ReferenceId = "order-" + sessionId, Currency = "USD" };

        session.Put(new CheckoutContactInfo { Email = email });

        return new CheckoutFlow(session);
    }

    private static DiscountLine Discount(string code)
        => new() { ProviderKey = CouponDiscountProvider.ProviderKey, Code = code, Amount = 5m, Target = DiscountTarget.OneTime };
}
