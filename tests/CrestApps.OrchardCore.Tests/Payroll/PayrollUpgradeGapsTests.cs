using CrestApps.Core.Services;
using CrestApps.OrchardCore.Approvals.Services;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Payroll.Core;
using CrestApps.OrchardCore.Payroll.Core.Indexes;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Taxation.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Workforce;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Localization;
using OrchardCore.Settings;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// Gaps found while moving an application onto Payroll: the tax id key made on first use while the scope holds a write
/// transaction on SQLite, the preparer-approval setting read when a run is submitted however it was written, and elections
/// asked only for jurisdictions with a tax in effect that reads one.
/// </summary>
public sealed class PayrollUpgradeGapsTests
{
    [Fact]
    public async Task TaxIdKey_IsMadeOnFirstUse_WhileTheScopeHoldsAWriteTransaction_OnSqlite()
    {
        await using var harness = await PayRunTests.CreateHarnessAsync();

        var hash = await harness.RunAsync(async services =>
        {
            // The scope writes first, as a recipe or an import does, so its session holds SQLite's write lock. A key made
            // in a session of its own was a second writer here, and SQLite refused it: "database is locked".
            var employers = services.GetRequiredService<IEmployerManager>();
            var employer = await employers.NewAsync();

            employer.Name = "Contoso";
            await employers.CreateAsync(employer);
            await services.GetRequiredService<global::YesSql.ISession>().FlushAsync();

            return await services.GetRequiredService<PayrollTaxIdHasher>().HashAsync("123-45-6789");
        });

        var again = await harness.RunAsync(async services => await services.GetRequiredService<PayrollTaxIdHasher>().HashAsync("123 45 6789"), commit: false);
        var keys = await harness.RunAsync(async services => await services.GetRequiredService<global::YesSql.ISession>().Query<PayrollKeys>(collection: PayrollConstants.CollectionName).ListAsync(), commit: false);

        Assert.False(string.IsNullOrEmpty(hash));
        Assert.Equal(hash, again);
        Assert.Single(keys);
    }

    [Fact]
    public async Task PreparerMayApprove_WhenTheSettingIsOnWithoutItsSettingsPage()
    {
        var site = Site(allowPreparerToApprove: false);

        await using var harness = await PayRunTests.CreateHarnessAsync(services => services.AddSingleton(SiteService(site)));
        var setup = await PayRunTests.SetUpAsync(harness);
        var preparer = CalendarTestHarness.User("preparer", PayrollPermissionTemplates.For(PayrollPermissions.PreparePayRuns, setup.EmployerId).Name, PayrollPermissionTemplates.For(PayrollPermissions.ApprovePayRuns, setup.EmployerId).Name);

        // The policy doesn't let the preparer approve; a recipe, a deployment or code turns the setting on, and nothing
        // copies it onto the policy.
        site.Put(new PayrollSettings { AllowPreparerToApprove = true });

        var preparerMay = await SubmitAndAskAsync(harness, setup, preparer);

        Assert.True(preparerMay);
    }

    [Fact]
    public async Task PreparerMayNotApprove_WhenTheSettingIsOff_EvenIfThePolicyStillSaysSo()
    {
        var site = Site(allowPreparerToApprove: true);

        await using var harness = await PayRunTests.CreateHarnessAsync(services => services.AddSingleton(SiteService(site)));
        var setup = await PayRunTests.SetUpAsync(harness);
        var preparer = CalendarTestHarness.User("preparer", PayrollPermissionTemplates.For(PayrollPermissions.PreparePayRuns, setup.EmployerId).Name, PayrollPermissionTemplates.For(PayrollPermissions.ApprovePayRuns, setup.EmployerId).Name);

        await harness.RunAsync(async services =>
        {
            var policies = services.GetRequiredService<INamedCatalogManager<CrestApps.OrchardCore.Approvals.Models.ApprovalPolicy>>();
            var policy = await policies.FindByNameAsync(PayrollConstants.PayRunApprovalPolicy);

            policy.AllowRequesterToApprove = true;
            policy.AllowSubjectToDecide = true;
            await policies.UpdateAsync(policy);
        });

        site.Put(new PayrollSettings { AllowPreparerToApprove = false });

        var preparerMay = await SubmitAndAskAsync(harness, setup, preparer);

        Assert.False(preparerMay);
    }

    [Fact]
    public async Task Elections_AreAskedOnlyForJurisdictionsWhoseTaxTakesOne()
    {
        var provider = new FixedElectionsProvider(["US", "US-MI"]);

        await using var harness = await CreateProfileHarnessAsync(services => services.AddSingleton<IIncomeTaxProvider>(provider));
        var (missing, filled) = await ElectionBlockersAsync(harness);

        // Detroit's tax reads the state certificate, so the city asks for no election of its own.
        Assert.Equal(["Enter the tax election for US.", "Enter the tax election for US-MI."], missing);
        Assert.Empty(filled);
    }

    [Fact]
    public async Task Elections_AreAskedForEveryJurisdiction_WhenTheProviderCantTell()
    {
        var provider = new FixedElectionsProvider(null);

        await using var harness = await CreateProfileHarnessAsync(services => services.AddSingleton<IIncomeTaxProvider>(provider));
        var (missing, filled) = await ElectionBlockersAsync(harness);

        Assert.Equal(["Enter the tax election for US.", "Enter the tax election for US-MI.", "Enter the tax election for US-MI-DETROIT."], missing);
        Assert.Equal(["Enter the tax election for US-MI-DETROIT."], filled);
    }

    [Fact]
    public async Task IncomeTaxRules_TakeElections_OfTheirOwnOrTheNamedJurisdiction_WhileInEffect()
    {
        var rules = new[]
        {
            new IncomeTaxRule { TaxCode = "US-FIT", Jurisdiction = "US", UsesElections = true, EffectiveFrom = new DateOnly(2026, 1, 1) },
            new IncomeTaxRule { TaxCode = "US-SS", Jurisdiction = "US", EffectiveFrom = new DateOnly(2026, 1, 1) },
            new IncomeTaxRule { TaxCode = "US-MI-SIT", Jurisdiction = "US-MI", UsesElections = true, EffectiveFrom = new DateOnly(2026, 1, 1) },
            new IncomeTaxRule { TaxCode = "US-MI-DETROIT", Jurisdiction = "US-MI-DETROIT", ExemptionAllowance = 600, ElectionsFrom = "US-MI", EffectiveFrom = new DateOnly(2026, 1, 1) },
            new IncomeTaxRule { TaxCode = "US-OH-OLD", Jurisdiction = "US-OH", UsesElections = true, EffectiveFrom = new DateOnly(2020, 1, 1), EffectiveTo = new DateOnly(2025, 12, 31) },
            new IncomeTaxRule { TaxCode = "US-PA-OFF", Jurisdiction = "US-PA", UsesElections = true, EffectiveFrom = new DateOnly(2026, 1, 1), Enabled = false },
            new IncomeTaxRule { TaxCode = "US-KY-ERLANGER", Jurisdiction = "US-KY-ERLANGER", ExemptionAllowance = 100, EffectiveFrom = new DateOnly(2026, 1, 1) },
        };
        var manager = new Mock<IIncomeTaxRuleManager>();

        manager.Setup(candidate => candidate.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rules);

        var provider = new IncomeTaxRulesProvider(manager.Object, new DefaultTaxCalculationMethodProvider([new PercentageTaxCalculationMethod()]), new InMemoryNamedCatalog<TaxTable>(), [], new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<IncomeTaxRulesProvider>());

        var jurisdictions = await provider.GetElectionJurisdictionsAsync(new DateOnly(2026, 6, 1), TestContext.Current.CancellationToken);

        Assert.Equal(["US", "US-KY-ERLANGER", "US-MI"], jurisdictions.Order(StringComparer.Ordinal));
        Assert.Contains("us-mi", jurisdictions);
    }

    private static async Task<bool> SubmitAndAskAsync(CalendarTestHarness harness, PayRunTests.Setup setup, System.Security.Claims.ClaimsPrincipal preparer)
    {
        var submitted = await harness.RunAsync(async services =>
        {
            var service = services.GetRequiredService<IPayRunService>();
            var run = await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId);

            Assert.True((await service.CalculateAsync(run, preparer)).Succeeded);

            return await service.SubmitAsync(run, preparer);
        });

        Assert.True(submitted.Succeeded, string.Join(" ", submitted.Errors));

        return await harness.RunAsync(async services =>
        {
            var request = await services.GetRequiredService<IApprovalRequestManager>().FindByIdAsync((await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId)).ApprovalRequestId);

            return (await services.GetRequiredService<IApprovalService>().GetEligibilityAsync(request, preparer)).CanDecide;
        }, commit: false);
    }

    private static async Task<(List<string> Missing, List<string> Filled)> ElectionBlockersAsync(CalendarTestHarness harness)
    {
        var lee = await WorkforceTests.AddWorkerAsync(harness, "Lee Park");

        return await harness.RunAsync(async services =>
        {
            var employers = services.GetRequiredService<IEmployerManager>();
            var employer = await employers.NewAsync();

            // Registered with the IRS and Michigan, with people working in Detroit.
            employer.Name = "Contoso";
            employer.Currency = "USD";
            employer.Registrations = [new EmployerTaxRegistration { Jurisdiction = "US" }, new EmployerTaxRegistration { Jurisdiction = "US-MI" }];
            employer.WorkJurisdictions = ["US-MI-DETROIT"];
            await employers.CreateAsync(employer);
            await services.GetRequiredService<global::YesSql.ISession>().FlushAsync();

            var service = services.GetRequiredService<IPayProfileService>();
            var profile = await services.GetRequiredService<IPayProfileManager>().NewAsync();

            profile.StaffMemberId = lee.ItemId;
            profile.EmployerId = employer.ItemId;
            profile.HomeAddress = new CrestApps.OrchardCore.Addresses.Models.Address { Country = "US", Region = "MI" };

            var missing = await ElectionMessagesAsync(service, profile);

            profile.Elections =
            [
                new TaxElection { Jurisdiction = "US", EffectiveFrom = new DateOnly(2020, 1, 1) },
                new TaxElection { Jurisdiction = "US-MI", EffectiveFrom = new DateOnly(2020, 1, 1) },
            ];

            return (missing, await ElectionMessagesAsync(service, profile));
        }, commit: false);
    }

    private static async Task<List<string>> ElectionMessagesAsync(IPayProfileService service, EmployeePayProfile profile)
        => (await service.GetBlockersAsync(profile))
            .Where(blocker => blocker.Code == "NoElection")
            .Select(blocker => blocker.Message)
            .ToList();

    private static Task<CalendarTestHarness> CreateProfileHarnessAsync(Action<IServiceCollection> configure)
        => WorkforceTests.CreateAsync(
            services =>
            {
                services.AddDataProtection();
                services.AddPayrollCore();
                services.AddPayrollHandlers();
                services.AddScoped<PayrollNames>();
                services.AddSingleton<global::OrchardCore.Modules.ILocalClock>(new CrestApps.OrchardCore.Tests.Doubles.FixedOffsetLocalClock(() => DateTime.UtcNow, TimeSpan.Zero));
                configure(services);
            },
            PayrollIndexSchema.CreateAsync,
            PayrollIndexSchema.Providers());

    private static SiteSettings Site(bool allowPreparerToApprove)
    {
        var site = new SiteSettings { TimeZoneId = "America/New_York" };

        site.Put(new PayrollSettings { AllowPreparerToApprove = allowPreparerToApprove });

        return site;
    }

    private static ISiteService SiteService(SiteSettings site)
    {
        var siteService = new Mock<ISiteService>();

        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(() => site);

        return siteService.Object;
    }

    /// <summary>
    /// A tax provider that reads the elections of the jurisdictions it is given, or can't tell when given none.
    /// </summary>
    private sealed class FixedElectionsProvider : IIncomeTaxProvider
    {
        private readonly IReadOnlySet<string> _jurisdictions;

        public FixedElectionsProvider(string[] jurisdictions)
        {
            _jurisdictions = jurisdictions is null ? null : new HashSet<string>(jurisdictions, StringComparer.OrdinalIgnoreCase);
        }

        public string Name => "Fixed";

        public LocalizationSource DisplayName => new("Fixed");

        public Task<IncomeTaxResult> CalculateAsync(IncomeTaxRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new IncomeTaxResult());

        public Task<IReadOnlySet<string>> GetElectionJurisdictionsAsync(DateOnly date, CancellationToken cancellationToken = default)
            => Task.FromResult(_jurisdictions);
    }
}
