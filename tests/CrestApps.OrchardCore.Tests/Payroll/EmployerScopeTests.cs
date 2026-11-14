using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Payroll.Controllers;
using CrestApps.OrchardCore.Payroll.Core;
using CrestApps.OrchardCore.Payroll.Core.Indexes;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.DataSources;
using CrestApps.OrchardCore.Payroll.Drivers;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.ViewModels;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Workforce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.DataSources;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// Employers and the payroll scope: a payroll clerk of one employer opens only its pages and reads only its people's
/// data, while one with the global permission sees every employer.
/// </summary>
public sealed class EmployerScopeTests
{
    [Fact]
    public async Task Scope_IsEveryEmployer_WithTheGlobalPermission_AndTheirOwn_WithAPerEmployerOne()
    {
        await using var harness = await CreateHarnessAsync();
        var (contoso, fabrikam) = await AddEmployersAsync(harness);

        var admin = CalendarTestHarness.User("admin", PayrollPermissions.PreparePayRuns.Name);
        var clerk = CalendarTestHarness.User("clerk", PayrollPermissionTemplates.For(PayrollPermissions.PreparePayRuns, contoso.ItemId).Name);
        var nobody = CalendarTestHarness.User("nobody");

        var (adminScope, clerkScope, nobodyScope, clerkMayPrepareFabrikam, clerkMayViewContoso) = await harness.RunAsync(async services =>
        {
            var scope = services.GetRequiredService<IPayrollScopeService>();

            return (
                await scope.GetScopeAsync(admin, PayrollPermissions.PreparePayRuns),
                await scope.GetScopeAsync(clerk, PayrollPermissions.PreparePayRuns),
                await scope.GetScopeAsync(nobody, PayrollPermissions.PreparePayRuns),
                await scope.AuthorizeAsync(clerk, PayrollPermissions.PreparePayRuns, fabrikam.ItemId),
                await scope.AuthorizeAsync(clerk, PayrollPermissions.ViewPayRuns, contoso.ItemId));
        }, commit: false);

        Assert.True(adminScope.All);
        Assert.False(clerkScope.All);
        Assert.Equal([contoso.ItemId], clerkScope.EmployerIds);
        Assert.True(nobodyScope.IsEmpty);
        Assert.False(clerkMayPrepareFabrikam);

        // Preparing pay runs for an employer implies seeing them, for that employer only.
        Assert.True(clerkMayViewContoso);
    }

    [Fact]
    public async Task EmployerPages_AClerkOfOneEmployer_SeesOnlyIt_AndIsRefusedTheOther()
    {
        await using var harness = await CreateHarnessAsync();
        var (contoso, fabrikam) = await AddEmployersAsync(harness);
        var clerk = CalendarTestHarness.User("clerk", PayrollPermissionTemplates.For(PayrollPermissions.ManagePayrollSettings, contoso.ItemId).Name);
        var admin = CalendarTestHarness.User("admin", PayrollPermissions.ManagePayrollSettings.Name);

        var (clerkList, clerkOther, clerkOwn, clerkCreate, clerkDelete, adminList) = await harness.RunAsync(async services => (
            await Controller(services, clerk).Index(),
            await Controller(services, clerk).Edit(fabrikam.ItemId),
            await Controller(services, clerk).Edit(contoso.ItemId),
            await Controller(services, clerk).Create(),
            await Controller(services, clerk).Delete(contoso.ItemId),
            await Controller(services, admin).Index()), commit: false);

        var listed = Assert.IsType<EmployersIndexViewModel>(Assert.IsType<ViewResult>(clerkList).Model);

        Assert.Equal([contoso.ItemId], listed.Employers.Select(employer => employer.ItemId));
        Assert.False(listed.CanAdd);
        Assert.IsType<ForbidResult>(clerkOther);
        Assert.IsType<ViewResult>(clerkOwn);
        Assert.IsType<ForbidResult>(clerkCreate);
        Assert.IsType<ForbidResult>(clerkDelete);
        Assert.Equal(2, Assert.IsType<EmployersIndexViewModel>(Assert.IsType<ViewResult>(adminList).Model).Employers.Count);
    }

    [Fact]
    public async Task PayInputs_AClerkOfOneEmployer_ReadsOnlyItsPeople_AndOneOfNoneReadsNothing()
    {
        await using var harness = await CreateHarnessAsync(services =>
        {
            services.AddScoped<IPayInputService>(_ => new FixedInputs());
            services.AddScoped<IPayrollEmployeeDirectory>(_ => new FixedDirectory());
        });
        var (contoso, _) = await AddEmployersAsync(harness);

        FixedDirectory.Contoso = contoso.ItemId;

        var clerk = CalendarTestHarness.User("clerk", PayrollPermissionTemplates.For(PayrollPermissions.ExportPayrollData, contoso.ItemId).Name);
        var admin = CalendarTestHarness.User("admin", PayrollPermissions.ExportPayrollData.Name);
        var other = CalendarTestHarness.User("other", PayrollPermissions.PreparePayRuns.Name);

        var clerkRows = await ReadAsync(harness, clerk);
        var adminRows = await ReadAsync(harness, admin);
        var otherRows = await ReadAsync(harness, other);

        Assert.Equal(["ana"], clerkRows);
        Assert.Equal(["ana", "ben"], adminRows.Order(StringComparer.Ordinal));
        Assert.Empty(otherRows);
    }

    [Fact]
    public async Task Employer_NeedsAUniqueName_AThreeLetterCurrency_AndAJurisdictionPerRegistration()
    {
        await using var harness = await CreateHarnessAsync();

        await AddEmployersAsync(harness);

        var (duplicate, currency, registration, valid) = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IEmployerManager>();

            async Task<bool> ValidAsync(Action<Employer> configure)
            {
                var employer = await manager.NewAsync();

                configure(employer);

                return (await manager.ValidateAsync(employer)).Succeeded;
            }

            return (
                await ValidAsync(employer => employer.Name = "contoso"),
                await ValidAsync(employer => { employer.Name = "Northwind"; employer.Currency = "US"; }),
                await ValidAsync(employer => { employer.Name = "Northwind"; employer.Registrations = [new EmployerTaxRegistration { Agency = "IRS" }]; }),
                await ValidAsync(employer => { employer.Name = "Northwind"; employer.Currency = "usd"; employer.Registrations = [new EmployerTaxRegistration { Jurisdiction = "us", Rates = [new DatedRate { EffectiveFrom = new DateOnly(2026, 1, 1), Rate = 2.7m }] }]; }));
        }, commit: false);

        Assert.False(duplicate);
        Assert.False(currency);
        Assert.False(registration);
        Assert.True(valid);
    }

    [Fact]
    public void RegistrationRows_OfTheSameAccount_AreOneRegistration_WithItsDatedRates()
    {
        var driver = new EmployerDisplayDriver(null, null, null, RecipeRoundTrip.Localizer<EmployerDisplayDriver>());

        var registrations = driver.ReadRegistrations(
        [
            new EmployerRegistrationRow { Jurisdiction = "us-ca", Agency = "EDD", AccountId = "123", DepositFrequency = "Quarterly", Rate = "3.4", RateFrom = new DateOnly(2026, 1, 1) },
            new EmployerRegistrationRow { Jurisdiction = "US-CA", Agency = "EDD", AccountId = "123", Rate = "2.9", RateFrom = new DateOnly(2027, 1, 1) },
            new EmployerRegistrationRow { Jurisdiction = "US", Agency = "IRS", AccountId = "12-3456789", DepositFrequency = "Monthly" },
            new EmployerRegistrationRow(),
        ], null);

        Assert.Equal(["US-CA", "US"], registrations.Select(item => item.Jurisdiction));
        Assert.Equal(3.4m, registrations[0].RateOn(new DateOnly(2026, 6, 1)));
        Assert.Equal(2.9m, registrations[0].RateOn(new DateOnly(2027, 6, 1)));
        Assert.Null(registrations[0].RateOn(new DateOnly(2025, 6, 1)));
        Assert.Empty(registrations[1].Rates);
    }

    [Fact]
    public async Task PermissionProvider_ListsEachEmployersOwnPermissions()
    {
        await using var harness = await CreateHarnessAsync();
        var (contoso, _) = await AddEmployersAsync(harness);

        var names = await harness.RunAsync(async services =>
            (await ActivatorUtilities.CreateInstance<PayrollPermissionProvider>(services).GetPermissionsAsync()).Select(permission => permission.Name).ToList(), commit: false);
        var parsed = PayrollPermissionTemplates.Parse($"PreparePayRuns_{contoso.ItemId}");

        Assert.Contains(PayrollPermissions.PreparePayRuns.Name, names);
        Assert.Contains($"PreparePayRuns_{contoso.ItemId}", names);
        Assert.Contains($"ExportPayrollData_{contoso.ItemId}", names);
        Assert.Equal((PayrollPermissions.PreparePayRuns.Name, contoso.ItemId), (parsed.Value.Permission.Name, parsed.Value.EmployerId));
        Assert.DoesNotContain($"ViewOwnPay_{contoso.ItemId}", names);
    }

    [Fact]
    public void SampleRolesRecipe_GrantsOnlyPermissionsPayrollDeclares()
    {
        var path = Path.Combine(FindRepositoryRoot(), "src", "Modules", "CrestApps.OrchardCore.Payroll", "Recipes", "payroll-sample-roles.recipe.json");
        var recipe = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var known = PayrollPermissions.Payroll.Append(PayrollPermissions.ManagePaySetup).Select(permission => permission.Name).ToHashSet(StringComparer.Ordinal);
        var roles = recipe["steps"]!.AsArray().Single(step => step!["name"]!.GetValue<string>() == "Roles")!["Roles"]!.AsArray();
        var clerk = roles.Single(role => role!["Name"]!.GetValue<string>() == "Payroll clerk")!["Permissions"]!.AsArray().Select(node => node!.GetValue<string>());

        Assert.All(roles.SelectMany(role => role!["Permissions"]!.AsArray()), permission => Assert.Contains(permission!.GetValue<string>(), known));
        Assert.DoesNotContain(PayrollPermissions.ApprovePayRuns.Name, clerk);
    }

    private static Task<CalendarTestHarness> CreateHarnessAsync(Action<IServiceCollection> configure = null)
        => WorkforceTests.CreateAsync(
            services =>
            {
                services.AddPayrollCore();
                configure?.Invoke(services);
            },
            PayrollIndexSchema.CreateAsync,
            PayrollIndexSchema.Providers());

    private static async Task<(Employer Contoso, Employer Fabrikam)> AddEmployersAsync(CalendarTestHarness harness)
    {
        async Task<Employer> AddAsync(string name)
            => await harness.RunAsync(async services =>
            {
                var manager = services.GetRequiredService<IEmployerManager>();
                var employer = await manager.NewAsync();

                employer.Name = name;
                employer.Currency = "USD";
                await manager.CreateAsync(employer);

                return employer;
            });

        return (await AddAsync("Contoso"), await AddAsync("Fabrikam"));
    }

    private static EmployersController Controller(IServiceProvider services, ClaimsPrincipal user)
    {
        var displayManager = new Mock<IDisplayManager<Employer>>();

        displayManager
            .Setup(manager => manager.BuildEditorAsync(It.IsAny<Employer>(), It.IsAny<IUpdateModel>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Mock.Of<IShape>());

        return new EmployersController(
            services.GetRequiredService<IEmployerManager>(),
            services.GetRequiredService<EmployerStore>(),
            services.GetRequiredService<IPayrollScopeService>(),
            [],
            services.GetRequiredService<IAuthorizationService>(),
            Mock.Of<IUpdateModelAccessor>(),
            displayManager.Object,
            Mock.Of<INotifier>(),
            new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughHtmlLocalizer<EmployersController>(),
            new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<EmployersController>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user,
                    RequestServices = new ServiceCollection()
                        .AddSingleton(Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory>())
                        .AddSingleton<Microsoft.AspNetCore.Mvc.ModelBinding.IModelMetadataProvider, Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider>()
                        .BuildServiceProvider(),
                },
            },
        };
    }

    private static Task<string[]> ReadAsync(CalendarTestHarness harness, ClaimsPrincipal user)
        => harness.RunAsync(async services =>
        {
            var dataSet = ActivatorUtilities.CreateInstance<PayInputsDataSet>(services);
            var source = new PayrollDataSource([dataSet]);
            var table = await source.ReadRowsAsync(new DataSourceQuery
            {
                DataSet = PayrollConstants.PayInputsDataSetName,
                Fields = new HashSet<string>(StringComparer.Ordinal),
                MaxRows = 100,
                Context = new DataSourceContext { User = user },
            }, 100, TestContext.Current.CancellationToken);
            var staff = table.Fields.ToList().FindIndex(field => field.Name == "StaffMemberId");

            return table.Rows.Select(row => (string)row[staff]).ToArray();
        }, commit: false);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }

    private sealed class FixedInputs : IPayInputService
    {
        public Task<IReadOnlyList<PayInput>> GetInputsAsync(PayInputQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PayInput>>(
            [
                .. new[] { "ana", "ben" }
                    .Where(staff => query.StaffMemberIds is not { Count: > 0 } || query.StaffMemberIds.Contains(staff))
                    .Select(staff => new PayInput { SourceKind = "Timesheet", SourceId = staff, StaffMemberId = staff, Hours = 8 }),
            ]);

        public Task MarkConsumedAsync(PayInputConsumption consumption, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ReleaseAsync(PayInputConsumption consumption, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FixedDirectory : IPayrollEmployeeDirectory
    {
        public static string Contoso { get; set; }

        public Task<IReadOnlySet<string>> GetStaffMemberIdsAsync(IReadOnlyCollection<string> employerIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(employerIds.Contains(Contoso) ? ["ana"] : [], StringComparer.Ordinal));
    }
}
