using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Migrations;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Tests.Utilities;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// Review 11 C5: the upgrade step for M10 turned <c>AllowPrefixOnStoreHost</c> on for every store without a base domain,
/// which is every store, since the base domain is new. It now turns it on only where the store's completed sites show it
/// sold sites on a prefix of its own address. The jobs are read from a real database, as the upgrade reads them.
/// </summary>
public sealed class TenantOnboardingPrefixUpgradeTests : IAsyncLifetime
{
    private static readonly DateTime _now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-prefix-upgrade-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new TenantProvisioningJobIndexProvider()], SubscriptionConstants.TenantProvisioningCollectionName);
        await _store.InitializeAsync(TestContext.Current.CancellationToken);
        await _store.InitializeCollectionAsync(SubscriptionConstants.TenantProvisioningCollectionName, TestContext.Current.CancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await new TenantProvisioningJobMigrations { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// A store that sold a site on a prefix of its own address keeps selling them.
    /// </summary>
    [Fact]
    public async Task AStoreThatSoldPrefixSitesOnItsOwnAddress_KeepsSellingThem()
    {
        await SaveJobsAsync(
            new TenantProvisioningJob { ItemId = "job-1", TenantName = "Shop1", Prefix = "shop1", Status = TenantProvisioningStatus.Succeeded, CreatedUtc = _now });

        var settings = await UpgradeAsync(new SubscriptionOnboardingSettings());

        Assert.True(settings.AllowPrefixOnStoreHost);
    }

    /// <summary>
    /// A store that never sold one (no site yet, only sites on their own domains, or only failed attempts) stays off, and
    /// the onboarding settings and the Site provisioning page say how to sell sites.
    /// </summary>
    [Fact]
    public async Task AStoreThatNeverSoldAPrefixSiteOnItsOwnAddress_LeavesItOff()
    {
        await SaveJobsAsync(
            new TenantProvisioningJob { ItemId = "job-1", TenantName = "Shop1", Domains = ["shop1.example.net"], Status = TenantProvisioningStatus.Succeeded, CreatedUtc = _now },
            new TenantProvisioningJob { ItemId = "job-2", TenantName = "Shop2", Prefix = "shop2", Status = TenantProvisioningStatus.Abandoned, CreatedUtc = _now },
            new TenantProvisioningJob { ItemId = "job-3", TenantName = "Shop3", Prefix = "shop3", Status = TenantProvisioningStatus.AwaitingPayment, CreatedUtc = _now });

        var settings = await UpgradeAsync(new SubscriptionOnboardingSettings { AllowCustomDomains = true, ReservedHosts = ["operator.com"] });

        Assert.False(settings.AllowPrefixOnStoreHost);
        Assert.False(settings.GivesSoldSitesAnAddress());
    }

    [Fact]
    public async Task AStoreWithNoSites_LeavesItOff()
    {
        var settings = await UpgradeAsync(new SubscriptionOnboardingSettings());

        Assert.False(settings.AllowPrefixOnStoreHost);
    }

    private async Task SaveJobsAsync(params TenantProvisioningJob[] jobs)
    {
        await using var session = _store.CreateSession();
        var store = new TenantProvisioningJobStore(session, Mock.Of<IClock>(clock => clock.UtcNow == _now));

        foreach (var job in jobs)
        {
            await store.CreateAsync(job, TestContext.Current.CancellationToken);
        }

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // Runs the upgrade step's work against the database and a site whose settings are stored the way Orchard stores them.
    private async Task<SubscriptionOnboardingSettings> UpgradeAsync(SubscriptionOnboardingSettings initial)
    {
        var site = new SiteSettings();
        site.Put(initial);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.LoadSiteSettingsAsync()).ReturnsAsync(site);
        siteService.Setup(service => service.UpdateSiteSettingsAsync(It.IsAny<ISite>())).Returns(Task.CompletedTask);

        await using (var session = _store.CreateSession())
        {
            await SubscriptionOnboardingUpgrade.KeepPrefixSitesOnStoreHostAsync(session, siteService.Object);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return site.As<SubscriptionOnboardingSettings>();
    }
}
