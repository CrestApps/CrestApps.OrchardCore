using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Tests.Subscriptions.Fakes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Abstractions.Setup;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking;
using OrchardCore.Modules;
using OrchardCore.Settings;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using OrchardCore.Recipes.Models;
using OrchardCore.Setup.Services;
using Xunit;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// Creating the site a customer paid for is the one part of a subscription that cannot be undone by hand
/// cheaply. These tests pin the two things that stopped it working at all.
/// </summary>
/// <remarks>
/// Both were found by selling a site on a running installation. Nothing gave the new tenant a database, so
/// setup refused every time with "DatabaseProvider setting is required"; and because setup registers the
/// tenant before it creates anything, the failed attempt left the name behind in the shell host, so every
/// retry then failed with a name collision the previous attempt had invented — a job that failed once could
/// never succeed.
/// </remarks>
public sealed class TenantProvisioningServiceTests
{
    private static readonly DateTime _now = new(2024, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Setup refuses without a database provider, and nobody is around to choose one for a self-service
    /// purchase, so an installation that has configured nothing still creates the site.
    /// </summary>
    [Fact]
    public async Task ProvisionAsync_WithNothingConfigured_CreatesTheSiteOnSqlite()
    {
        var (service, setup, _) = CreateService(new SubscriptionOnboardingSettings());

        await service.ProvisionAsync("job-1", TestContext.Current.CancellationToken);

        var settings = setup.Captured.ShellSettings;

        Assert.Equal("Sqlite", settings["DatabaseProvider"]);
        Assert.Equal("Sqlite", setup.Captured.Properties[SetupConstants.DatabaseProvider]);

        // A file-per-site provider needs no prefix, and giving it one only makes the table names odd.
        Assert.True(string.IsNullOrEmpty(settings["TablePrefix"]));
    }

    /// <summary>
    /// The operator's database is used when they have configured one, and sites that share it are kept
    /// apart by a prefix nobody has to invent.
    /// </summary>
    [Fact]
    public async Task ProvisionAsync_WithAConfiguredDatabase_UsesItAndDerivesATablePrefix()
    {
        var (service, setup, _) = CreateService(new SubscriptionOnboardingSettings
        {
            DatabaseProvider = "Postgres",
            ConnectionString = "Host=db;Database=saas",
            Schema = "public",
        });

        await service.ProvisionAsync("job-1", TestContext.Current.CancellationToken);

        var settings = setup.Captured.ShellSettings;

        Assert.Equal("Postgres", settings["DatabaseProvider"]);
        Assert.Equal("Host=db;Database=saas", settings["ConnectionString"]);
        Assert.Equal("public", settings["Schema"]);
        Assert.Equal("qasite1", settings["TablePrefix"]);
    }

    /// <summary>
    /// A name a real site already answers to cannot be taken, whatever the customer paid.
    /// </summary>
    [Fact]
    public async Task ProvisionAsync_WhenTheNameBelongsToARunningSite_Fails()
    {
        var running = new ShellSettings { Name = "qasite1" };
        running.AsRunning();

        var (service, setup, store) = CreateService(new SubscriptionOnboardingSettings(), existing: running);

        var status = await service.ProvisionAsync("job-1", TestContext.Current.CancellationToken);

        Assert.Equal(TenantProvisioningStatus.Failed, status);
        Assert.Contains("already in use", store.Jobs.First().LastError);
        Assert.Null(setup.Captured);
    }

    /// <summary>
    /// The residue of this job's own failed attempt is not a collision. Reading it as one is what made a
    /// job that failed once fail forever, on a site the customer had already paid for.
    /// </summary>
    [Fact]
    public async Task ProvisionAsync_WhenAnUninitializedShellOfTheSameNameExists_StillCreatesTheSite()
    {
        var leftOver = new ShellSettings { Name = "qasite1" };
        leftOver.AsUninitialized();

        var (service, setup, store) = CreateService(new SubscriptionOnboardingSettings(), existing: leftOver);

        var status = await service.ProvisionAsync("job-1", TestContext.Current.CancellationToken);

        Assert.Equal(TenantProvisioningStatus.Succeeded, status);
        Assert.NotNull(setup.Captured);
        Assert.Equal("/qasite1", store.Jobs.First().SiteUrl);
    }

    /// <summary>
    /// A setup that reports its failure as a message rather than an exception still has to leave something
    /// behind on the job for an operator to act on.
    /// </summary>
    [Fact]
    public async Task ProvisionAsync_WhenSetupReportsAnError_RecordsItOnTheJob()
    {
        var (service, setup, store) = CreateService(new SubscriptionOnboardingSettings());
        setup.Error = "The provided connection string is invalid.";

        var status = await service.ProvisionAsync("job-1", TestContext.Current.CancellationToken);

        Assert.Equal(TenantProvisioningStatus.Failed, status);
        Assert.Contains("connection string", store.Jobs.First().LastError);
    }

    private static (TenantProvisioningService Service, RecordingSetupService Setup, InMemoryTenantProvisioningJobStore Store) CreateService(
        SubscriptionOnboardingSettings onboarding,
        ShellSettings existing = null)
    {
        var protector = new Mock<IDataProtector>();
        protector.Setup(p => p.Unprotect(It.IsAny<byte[]>())).Returns(System.Text.Encoding.UTF8.GetBytes("Password!1"));

        var protectionProvider = new Mock<IDataProtectionProvider>();
        protectionProvider.Setup(p => p.CreateProtector(It.IsAny<string>())).Returns(protector.Object);

        var store = new InMemoryTenantProvisioningJobStore(new TenantProvisioningJob
        {
            ItemId = "job-1",
            TenantName = "qasite1",
            TenantTitle = "QA Customer Site",
            Prefix = "qasite1",
            AdminUsername = "siteadmin",
            AdminEmail = "siteadmin@example.com",
            ProtectedAdminPassword = "cHJvdGVjdGVk",
            RecipeName = "Blog",
            Status = TenantProvisioningStatus.Pending,
            CreatedUtc = _now,
        });

        var shellHost = new Mock<IShellHost>();
        var found = existing;
        shellHost.Setup(h => h.TryGetSettings(It.IsAny<string>(), out found)).Returns(existing is not null);

        var settingsManager = new Mock<IShellSettingsManager>();
        settingsManager.Setup(m => m.CreateDefaultSettings()).Returns(() => new ShellSettings());

        var setup = new RecordingSetupService();

        var service = new TenantProvisioningService(
            store,
            SiteServiceFactory.Create(onboarding),
            shellHost.Object,
            settingsManager.Object,
            setup,
            protectionProvider.Object,
            new LocalLock(NullLogger<LocalLock>.Instance),
            new ServiceCollection().BuildServiceProvider(),
            CreateSession(),
            new ProvisioningClock(_now),
            NullLogger<TenantProvisioningService>.Instance);

        return (service, setup, store);
    }

    // The shared test clock refuses every time-zone member, and setup asks for the system zone.
    private sealed class ProvisioningClock : IClock
    {
        private readonly Clock _inner = new();

        public ProvisioningClock(DateTime utcNow)
        {
            UtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        }

        public DateTime UtcNow { get; }

        public ITimeZone GetTimeZone(string timeZoneId) => _inner.GetTimeZone(timeZoneId);

        public ITimeZone[] GetTimeZones() => _inner.GetTimeZones();

        public ITimeZone GetSystemTimeZone() => _inner.GetSystemTimeZone();

        public DateTimeOffset ConvertToTimeZone(DateTimeOffset dateTimeOffset, ITimeZone timeZone)
            => _inner.ConvertToTimeZone(dateTimeOffset, timeZone);
    }

    private static ISession CreateSession()
    {
        var session = new Mock<ISession>();
        session.Setup(s => s.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        return session.Object;
    }

    private sealed class RecordingSetupService : ISetupService
    {
        public SetupContext Captured { get; private set; }

        public string Error { get; set; }

        public Task<IEnumerable<RecipeDescriptor>> GetSetupRecipesAsync()
            => Task.FromResult<IEnumerable<RecipeDescriptor>>([new RecipeDescriptor { Name = "Blog" }]);

        public Task<string> SetupAsync(SetupContext context)
        {
            Captured = context;

            if (Error is not null)
            {
                context.Errors["setup"] = Error;
            }

            return Task.FromResult(string.Empty);
        }
    }
}
