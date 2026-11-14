using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Tests.Subscriptions.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Admin;
using OrchardCore.Data;
using OrchardCore.Environment.Extensions;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Builders;
using OrchardCore.Locking;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using OrchardCore.Setup.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// H1 (final security review): a site setup that fails must not leave Orchard's setup wizard answering for the store's
/// own root URL. These tests run Orchard Core's real <see cref="ShellHost"/>, <see cref="RunningShellTable"/> and
/// <see cref="SetupService"/>, because the defect lives in how they treat a tenant that was never saved: Orchard's
/// setup reloads the tenant from storage when it reports an error, and a tenant that was never saved reloads as an
/// uninitialized tenant with no host and no prefix, which then matches every request the store gets.
/// </summary>
public sealed class TenantProvisioningSetupFailureTests
{
    private static readonly DateTime _now = new(2024, 8, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly EphemeralDataProtectionProvider _protection = new();

    /// <summary>
    /// After a failed setup, the store's root URL is still the store's, and the sold site's leftover tenant answers only
    /// on its own prefix.
    /// </summary>
    [Fact]
    public async Task ProvisionAsync_WhenSetupFails_LeavesTheStoreRootOnTheDefaultTenant()
    {
        var harness = await Harness.CreateAsync();

        var status = await harness.Service.ProvisionAsync("job-1", TestContext.Current.CancellationToken);

        Assert.Equal(TenantProvisioningStatus.Failed, status);
        Assert.Equal(ShellSettings.DefaultShellName, harness.Table.Match(new HostString("store.example"), "/")?.Name);
        Assert.Equal(ShellSettings.DefaultShellName, harness.Table.Match(new HostString("store.example"), "/account")?.Name);
        Assert.Equal("qasite1", harness.Table.Match(new HostString("store.example"), "/qasite1")?.Name);
    }

    /// <summary>
    /// The leftover tenant is saved with a secret, so Orchard's setup wizard on its prefix asks for a token nobody but the
    /// host has, and with the plan's recipe.
    /// </summary>
    [Fact]
    public async Task ProvisionAsync_WhenSetupFails_LeavesASavedTenantWithASecretAndThePlansRecipe()
    {
        var harness = await Harness.CreateAsync();

        await harness.Service.ProvisionAsync("job-1", TestContext.Current.CancellationToken);

        Assert.True(harness.Settings.Saved.TryGetValue("qasite1", out var saved));
        Assert.True(saved.IsUninitialized());
        Assert.Equal("qasite1", saved.RequestUrlPrefix);
        Assert.False(string.IsNullOrWhiteSpace(saved["Secret"]));
        Assert.Equal("Blog", saved["RecipeName"]);
        Assert.Equal("sold-tenant", saved["FeatureProfile"]);
    }

    private sealed class Harness
    {
        public TenantProvisioningService Service { get; private init; }

        public RunningShellTable Table { get; private init; }

        public InMemoryShellSettingsManager Settings { get; private init; }

        public static async Task<Harness> CreateAsync()
        {
            var settingsManager = new InMemoryShellSettingsManager();
            var table = new RunningShellTable();
            var shellHost = new ShellHost(
                settingsManager,
                Mock.Of<IShellContextFactory>(),
                table,
                Mock.Of<IExtensionManager>(),
                NullLogger<ShellHost>.Instance);

            // The store: the Default tenant, running, with no host and no prefix, as most installations have it.
            var store = new ShellSettings { Name = ShellSettings.DefaultShellName };
            store.AsRunning();
            await settingsManager.SaveSettingsAsync(store);
            await shellHost.ReloadShellContextAsync(store, eventSource: false);

            var jobs = new InMemoryTenantProvisioningJobStore(new TenantProvisioningJob
            {
                ItemId = "job-1",
                TenantName = "qasite1",
                TenantTitle = "QA Customer Site",
                Prefix = "qasite1",
                AdminUsername = "siteadmin",
                AdminEmail = "siteadmin@example.com",
                ProtectedAdminPassword = _protection
                    .CreateProtector(SubscriptionConstants.ProtectorPurposes.TenantOnboardingStep)
                    .Protect("Password!1"),
                RecipeName = "Blog",
                FeatureProfile = "sold-tenant",
                CheckoutSessionId = "session-1",
                Status = TenantProvisioningStatus.Pending,
                CreatedUtc = _now,
            });

            var siteService = SiteServiceFactory.Create(new SubscriptionOnboardingSettings { AllowPrefixOnStoreHost = true });
            var httpContextAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };

            var service = new TenantProvisioningService(
                jobs,
                siteService,
                shellHost,
                settingsManager,
                CreateFailingSetupService(shellHost, httpContextAccessor),
                _protection,
                new LocalLock(NullLogger<LocalLock>.Instance),
                new ServiceCollection().BuildServiceProvider(),
                Mock.Of<ISession>(),
                new FixedClock(_now),
                NullLogger<TenantProvisioningService>.Instance,
                new TenantPurchaseRulesProvider(siteService, shellHost, httpContextAccessor, Options.Create(new AdminOptions()), new SoldTenantProfiles(), CreateSessionStore(), StoreRoutes.Create()),
                Mock.Of<ISubscriptionManager>());

            return new Harness { Service = service, Table = table, Settings = settingsManager };
        }

        // Orchard Core's own setup service, failing the way any setup error does: it reports the error and reloads the
        // tenant from storage.
        private static SetupService CreateFailingSetupService(IShellHost shellHost, IHttpContextAccessor httpContextAccessor)
        {
            var environment = new Mock<IHostEnvironment>();
            environment.SetupGet(e => e.ApplicationName).Returns("CrestApps.OrchardCore.Cms.Web");

            var idGenerator = new Mock<ISetupUserIdGenerator>();
            idGenerator.Setup(g => g.GenerateUniqueId()).Returns("4ADMINUSERID");

            var harvester = new Mock<IRecipeHarvester>();
            harvester.Setup(h => h.HarvestRecipesAsync()).ReturnsAsync([new RecipeDescriptor { Name = "Blog", IsSetupRecipe = true }]);

            var validator = new Mock<IDbConnectionValidator>();
            validator.Setup(v => v.ValidateAsync(It.IsAny<DbConnectionValidatorContext>())).ReturnsAsync(DbConnectionValidatorResult.InvalidConnection);

            return new SetupService(
                shellHost,
                environment.Object,
                Mock.Of<IShellContextFactory>(),
                idGenerator.Object,
                [harvester.Object],
                NullLogger<SetupService>.Instance,
                new KeyStringLocalizer<SetupService>(),
                Mock.Of<IHostApplicationLifetime>(),
                httpContextAccessor,
                validator.Object);
        }

        private static global::CrestApps.OrchardCore.Checkout.ICheckoutSessionStore CreateSessionStore()
        {
            var session = new global::CrestApps.OrchardCore.Checkout.CheckoutSession { SessionId = "session-1" };
            var step = new global::CrestApps.OrchardCore.Checkout.CheckoutFlowStep { Key = SubscriptionConstants.StepKey.TenantProvisioning };
            step.Data["RecipeName"] = "Blog";
            session.Steps.Add(step);

            var store = new Mock<global::CrestApps.OrchardCore.Checkout.ICheckoutSessionStore>();
            store.Setup(s => s.GetAsync("session-1", It.IsAny<CancellationToken>())).ReturnsAsync(session);

            return store.Object;
        }
    }

    private sealed class SoldTenantProfiles : ITenantFeatureProfileCatalog
    {
        public Task<IReadOnlyCollection<string>> GetProfileIdsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<string>>(["sold-tenant"]);
    }

    private sealed class FixedClock : IClock
    {
        private readonly Clock _inner = new();

        public FixedClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }

        public ITimeZone GetTimeZone(string timeZoneId) => _inner.GetTimeZone(timeZoneId);

        public ITimeZone[] GetTimeZones() => _inner.GetTimeZones();

        public ITimeZone GetSystemTimeZone() => _inner.GetSystemTimeZone();

        public DateTimeOffset ConvertToTimeZone(DateTimeOffset dateTimeOffset, ITimeZone timeZone)
            => _inner.ConvertToTimeZone(dateTimeOffset, timeZone);
    }

    private sealed class KeyStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
