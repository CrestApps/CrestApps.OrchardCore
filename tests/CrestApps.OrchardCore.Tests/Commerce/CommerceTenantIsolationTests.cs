using System.Text;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Migrations;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Downloads;
using CrestApps.OrchardCore.Downloads.Models;
using CrestApps.OrchardCore.Downloads.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// What one store tenant has, another never sees or spends: its digital files, its download links, its coupons' uses
/// and its throttling counts, even when the tenants share a cache.
/// </summary>
public sealed class CommerceTenantIsolationTests
{
    private static readonly DateTime _now = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task EachTenant_KeepsItsDigitalFilesInItsOwnFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"crestapps-tenants-{Guid.NewGuid():N}");

        try
        {
            var storeA = FileStoreFor(root, "storeA");
            var storeB = FileStoreFor(root, "storeB");

            var saved = await storeA.SaveAsync("guide.pdf", new MemoryStream(Encoding.UTF8.GetBytes("A")), TestContext.Current.CancellationToken);

            Assert.True(Directory.Exists(Path.Combine(root, "Sites", "storeA", DownloadsConstants.StoreFolder, saved.FileId)));
            Assert.Null(await storeB.FindAsync(saved.FileId, TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ADownloadLinkSignedForOneTenant_NamesNothingInAnother()
    {
        var entitlement = new DownloadEntitlement { Id = 1, ItemId = "entitlement-1", FileName = "guide.pdf", CreatedUtc = _now };

        var urlA = await DownloadServiceFor(new EphemeralDataProtectionProvider(), entitlement).CreateUrlAsync(entitlement);
        var token = Uri.UnescapeDataString(urlA[(urlA.LastIndexOf('/') + 1)..]);

        // The tenant's data protection keys sign its links; another tenant's keys can't read them.
        var (_, problem) = await DownloadServiceFor(new EphemeralDataProtectionProvider(), entitlement).ResolveAsync(token, TestContext.Current.CancellationToken);

        Assert.Equal(DownloadProblem.NotFound, problem);
    }

    [Fact]
    public async Task ACodeUsedUpInOneTenant_StillWorksInAnother()
    {
        var pathA = Path.Combine(Path.GetTempPath(), $"crestapps-coupons-a-{Guid.NewGuid():N}.db");
        var pathB = Path.Combine(Path.GetTempPath(), $"crestapps-coupons-b-{Guid.NewGuid():N}.db");
        var storeA = await CouponStoreAsync(pathA);
        var storeB = await CouponStoreAsync(pathB);

        try
        {
            await AddCouponAsync(storeA);
            await AddCouponAsync(storeB);

            Assert.Equal(CouponAvailability.Available, await HoldAsync(storeA, "session-a"));
            Assert.Equal(CouponAvailability.UsedUp, await HoldAsync(storeA, "session-a2"));
            Assert.Equal(CouponAvailability.Available, await HoldAsync(storeB, "session-b"));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(storeA, pathA);
            TemporarySqliteDatabase.DisposeAndDelete(storeB, pathB);
        }
    }

    [Fact]
    public async Task ThrottlingCounts_ArePerTenant_EvenInASharedCache()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var limiterA = LimiterFor(cache, "storeA");
        var limiterB = LimiterFor(cache, "storeB");

        Assert.True(await limiterA.AcquireAsync("coupon-code", "203.0.113.5"));
        Assert.True(await limiterA.AcquireAsync("coupon-code", "203.0.113.5"));
        Assert.False(await limiterA.AcquireAsync("coupon-code", "203.0.113.5"));

        Assert.True(await limiterB.AcquireAsync("coupon-code", "203.0.113.5"));
    }

    private static IDigitalFileStore FileStoreFor(string root, string tenant)
    {
        var services = new ServiceCollection();

        services.AddSingleton(new ShellSettings { Name = tenant });
        services.AddSingleton(Options.Create(new ShellOptions { ShellsApplicationDataPath = root, ShellsContainerName = "Sites" }));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        new CrestApps.OrchardCore.Downloads.Startup().ConfigureServices(services);

        return services.BuildServiceProvider().GetRequiredService<IDigitalFileStore>();
    }

    private static DownloadService DownloadServiceFor(IDataProtectionProvider protection, DownloadEntitlement entitlement)
    {
        var site = new Mock<ISite>();
        site.SetupGet(value => value.BaseUrl).Returns("https://store.test");
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);

        // A link another tenant signed can't be read, so nothing is looked up.
        var session = new Mock<global::YesSql.ISession>();

        return new DownloadService(session.Object, Mock.Of<IContentManager>(), siteService.Object, protection, new HttpContextAccessor(), Mock.Of<LinkGenerator>(), Mock.Of<IDistributedLock>(), clock.Object);
    }

    private static async Task<IStore> CouponStoreAsync(string path)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={path};Pooling=False"));
        store.RegisterIndexes([new CouponIndexProvider(), new CouponCodeIndexProvider(), new CouponRedemptionIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(CheckoutConstants.CouponCollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var migration = new CouponMigrations { SchemaBuilder = new SchemaBuilder(store.Configuration, transaction) };
        await migration.CreateAsync();
        await migration.UpdateFrom1Async();
        await migration.UpdateFrom2Async();
        await migration.UpdateFrom3Async();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private static async Task AddCouponAsync(IStore store)
    {
        await using var session = store.CreateSession();
        await new CouponStore(session, new TestClock(_now)).CreateAsync(new Coupon { ItemId = "c1", Code = "LAUNCH", Kind = CouponKind.Percentage, Percentage = 10m, MaxRedemptions = 1, IsEnabled = true }, TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<CouponAvailability> HoldAsync(IStore store, string sessionId)
    {
        await using var session = store.CreateSession();

        var site = new Mock<ISite>();
        site.Setup(value => value.GetOrCreate<CheckoutSettings>()).Returns(new CheckoutSettings());
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        var service = new CouponRedemptionService(
            new CouponStore(session, new TestClock(_now)),
            session,
            new LocalLock(NullLogger<LocalLock>.Instance),
            siteService.Object,
            new ServiceCollection().BuildServiceProvider(),
            new TestClock(_now),
            NullLogger<CouponRedemptionService>.Instance);

        var checkout = new CheckoutSession { SessionId = sessionId, Currency = "USD" };
        checkout.Put(new CheckoutContactInfo { Email = sessionId + "@example.test" });

        var availability = await service.HoldAsync(new CheckoutFlow(checkout), new DiscountLine { ProviderKey = CouponDiscountProvider.ProviderKey, Code = "LAUNCH", Amount = 5m }, TestContext.Current.CancellationToken);

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        return availability;
    }

    private static PaymentAttemptLimiter LimiterFor(IDistributedCache cache, string tenant)
    {
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((Mock.Of<ILocker>(), true));

        // The cache expires entries by the real clock, so the window has to start now: a fixed date in the past would
        // expire every count as soon as it was written.
        var now = DateTime.UtcNow;
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(now);

        return new PaymentAttemptLimiter(
            cache,
            distributedLock.Object,
            new ShellSettings { Name = tenant },
            clock.Object,
            Options.Create(new PaymentRateLimitOptions { PermitLimit = 2, Window = TimeSpan.FromMinutes(5) }));
    }
}
