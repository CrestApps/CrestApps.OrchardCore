using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Payroll.Controllers;
using CrestApps.OrchardCore.Payroll.Core;
using CrestApps.OrchardCore.Payroll.Core.Calculation;
using CrestApps.OrchardCore.Payroll.Core.Indexes;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Recipes;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.ViewModels;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Workforce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// The engine around the calculation: accumulators read from final statements on SQLite, the tax rules' checks and
/// recipe, a preview from a real profile, and who may see the tax rule and preview pages.
/// </summary>
public sealed class PayEngineTests
{
    [Fact]
    public async Task Accumulators_SumFinalStatementsOnly_ByPeriodMonthQuarterYearAndEver_AndAVoidCancelsItsStatement()
    {
        await using var harness = await CreateHarnessAsync();

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<PayStatementStore>();

            await store.CreateAsync(Statement("s1", new DateOnly(2025, 12, 19), PayStatementStatus.Paid, 100.50m, 40));
            await store.CreateAsync(Statement("s2", new DateOnly(2026, 2, 20), PayStatementStatus.Final, 200m, 40));
            await store.CreateAsync(Statement("s3", new DateOnly(2026, 4, 17), PayStatementStatus.Voided, 300m, 40));
            await store.CreateAsync(Statement("s3v", new DateOnly(2026, 4, 17), PayStatementStatus.Final, -300m, -40));
            await store.CreateAsync(Statement("s4", new DateOnly(2026, 5, 15), PayStatementStatus.Final, 400.25m, 40, periodStart: new DateOnly(2026, 5, 3)));
            await store.CreateAsync(Statement("draft", new DateOnly(2026, 5, 15), PayStatementStatus.Draft, 999m, 40, periodStart: new DateOnly(2026, 5, 3)));
            await store.CreateAsync(Statement("later", new DateOnly(2026, 6, 19), PayStatementStatus.Final, 50m, 4));
        });

        var accumulators = await harness.RunAsync(
            services => services.GetRequiredService<IPayAccumulatorService>().GetAsync("lee", "contoso", new DateOnly(2026, 5, 29), new DateOnly(2026, 5, 3), new DateOnly(2026, 5, 16)),
            commit: false);
        var regular = accumulators.For(PayAccumulators.CodeKey("reg"));
        var tax = accumulators.For(PayAccumulators.TaxKey("US-SS"));

        Assert.Equal((100.50m + 200m + 400.25m, 600.25m, 400.25m, 400.25m, 400.25m), (regular.Lifetime, regular.Year, regular.Quarter, regular.Month, regular.Period));
        Assert.Equal(80m, regular.YearHours);
        Assert.Equal(600.25m, tax.YearTaxableWages);
        Assert.Equal(12.40m + 24.82m, tax.Year);
    }

    [Fact]
    public async Task FinalStatement_CannotBeDeleted_ADraftCan()
    {
        await using var harness = await CreateHarnessAsync();

        var (final, draft) = await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<PayStatementStore>();
            var finalStatement = Statement("f", new DateOnly(2026, 5, 15), PayStatementStatus.Final, 10m, 1);
            var draftStatement = Statement("d", new DateOnly(2026, 5, 15), PayStatementStatus.Draft, 10m, 1);

            await store.CreateAsync(finalStatement);
            await store.CreateAsync(draftStatement);

            return (await Record.ExceptionAsync(async () => await store.DeleteAsync(finalStatement, TestContext.Current.CancellationToken)), await store.DeleteAsync(draftStatement, TestContext.Current.CancellationToken));
        }, commit: false);

        Assert.IsType<InvalidOperationException>(final);
        Assert.True(draft);
    }

    [Fact]
    public async Task TaxRules_NeedWhatTheirMethodTakes_AndTwoVersionsOfATaxNeverOverlap()
    {
        await using var harness = await CreateHarnessAsync();

        await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IPayrollTaxRuleManager>();

            await manager.CreateAsync(Rule("US-SS", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 6.2m));
        });

        var (noRate, overlapping, nextYear, badRate) = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IPayrollTaxRuleManager>();

            return (
                (await manager.ValidateAsync(Rule("US-FIT", new DateOnly(2026, 1, 1), null, null))).Succeeded,
                (await manager.ValidateAsync(Rule("us-ss", new DateOnly(2026, 7, 1), null, 6.4m))).Succeeded,
                (await manager.ValidateAsync(Rule("US-SS", new DateOnly(2027, 1, 1), null, 6.4m))).Succeeded,
                (await manager.ValidateAsync(Rule("US-X", new DateOnly(2026, 1, 1), null, 150m))).Succeeded);
        }, commit: false);

        Assert.False(noRate);
        Assert.False(overlapping);
        Assert.True(nextYear);
        Assert.False(badRate);
    }

    [Fact]
    public async Task Recipe_ImportsTaxRules_AndMatchesAVersionByCodeAndFirstDay()
    {
        await using var harness = await CreateHarnessAsync();
        var step = JsonNode.Parse("""
            {
              "name": "PayrollTaxRules",
              "Rules": [
                { "Name": "Social insurance", "TaxCode": "US-SS", "Jurisdiction": "US", "Category": "SocialInsurance", "Payer": "Employee", "Method": "Percentage", "Rate": 6.2, "WageBase": 176100, "EffectiveFrom": "2026-01-01" }
              ]
            }
            """)!.AsObject();

        async Task<IReadOnlyList<string>> ImportAsync()
            => await harness.RunAsync(async services =>
            {
                var context = new RecipeExecutionContext { Name = PayrollConstants.TaxRulesRecipeStepName, Step = step.DeepClone().AsObject() };

                await ActivatorUtilities.CreateInstance<PayrollTaxRulesRecipeStep>(services).ExecuteAsync(context);

                return context.Errors.ToList();
            });

        var first = await ImportAsync();

        step["Rules"]![0]!["Rate"] = 6.3m;

        var second = await ImportAsync();
        var rules = await harness.RunAsync(services => services.GetRequiredService<IPayrollTaxRuleManager>().ListAsync(), commit: false);

        Assert.Empty(first);
        Assert.Empty(second);
        Assert.Equal(6.3m, Assert.Single(rules).Rate);
        Assert.Equal(176100m, rules[0].WageBase);
    }

    [Fact]
    public async Task Preview_CalculatesARealProfile_FromItsInputsRatesAndTaxRules()
    {
        var inputs = new FixedInputs();

        await using var harness = await CreateHarnessAsync(services => services.AddSingleton<IPayInputService>(inputs));
        var lee = await WorkforceTests.AddWorkerAsync(harness, "Lee Park", worker => worker.Put(new StaffPayRates { Rates = [new StaffPayRate { EffectiveFrom = new DateOnly(2020, 1, 1), HourlyRate = 20 }] }));
        var profile = await SetUpAsync(harness, lee.ItemId, inputs);

        var context = await harness.RunAsync(async services =>
        {
            var built = await services.GetRequiredService<PayCalculationContextFactory>().CreateAsync(profile, new PayCalculationRequest
            {
                PeriodStart = new DateOnly(2026, 11, 1),
                PeriodEnd = new DateOnly(2026, 11, 14),
                PayDate = new DateOnly(2026, 11, 20),
            });

            await services.GetRequiredService<IPayCalculator>().CalculateAsync(built);

            return built;
        }, commit: false);

        Assert.Equal(["Earning 1600.00", "Tax 99.20"], context.Lines.Select(line => $"{line.Type} {line.Amount:0.00}"));
        Assert.Equal(1500.80m, context.Net);
        Assert.Equal([1500.80m], context.Payments.Select(payment => payment.Amount));
        Assert.Equal(["US", "US-MA"], context.HomeJurisdictions);
    }

    [Fact]
    public async Task TaxRulesAndPreviewPages_NeedTheirPermission_AndAClerkOfOneEmployerIsRefusedAnothersPreview()
    {
        var inputs = new FixedInputs();

        await using var harness = await CreateHarnessAsync(services => services.AddSingleton<IPayInputService>(inputs));
        var lee = await WorkforceTests.AddWorkerAsync(harness, "Lee Park", worker => worker.Put(new StaffPayRates { Rates = [new StaffPayRate { EffectiveFrom = new DateOnly(2020, 1, 1), HourlyRate = 20 }] }));
        var profile = await SetUpAsync(harness, lee.ItemId, inputs);
        var fabrikamClerk = CalendarTestHarness.User("clerk", PayrollPermissionTemplates.For(PayrollPermissions.ViewPayProfiles, "fabrikam").Name);
        var contosoClerk = CalendarTestHarness.User("clerk2", PayrollPermissionTemplates.For(PayrollPermissions.ManagePayrollSettings, profile.EmployerId).Name, PayrollPermissionTemplates.For(PayrollPermissions.ViewPayProfiles, profile.EmployerId).Name);
        var admin = CalendarTestHarness.User("admin", PayrollPermissions.ManagePayrollSettings.Name, PayrollPermissions.ViewPayProfiles.Name);

        var (otherPreview, ownPreview, clerkRules, adminRules) = await harness.RunAsync(async services => (
            await Preview(services, fabrikamClerk).Index(profile.ItemId, new DateOnly(2026, 11, 5)),
            await Preview(services, contosoClerk).Index(profile.ItemId, new DateOnly(2026, 11, 5)),
            await Rules(services, contosoClerk).Index(),
            await Rules(services, admin).Index()), commit: false);

        Assert.IsType<ForbidResult>(otherPreview);
        Assert.Equal(1500.80m, Assert.IsType<PayPreviewViewModel>(Assert.IsType<ViewResult>(ownPreview).Model).Net);

        // Tax rules are the same for every employer: the settings of one employer are not enough.
        Assert.IsType<ForbidResult>(clerkRules);
        Assert.IsType<ViewResult>(adminRules);
    }

    [Fact]
    public async Task TaxTable_UsedByAPayrollTaxRule_IsKept_AndATableNotInEffectSkipsTheRuleWithAWarning()
    {
        await using var harness = await CreateHarnessAsync();

        await harness.RunAsync(async services =>
        {
            await services.GetRequiredService<INamedCatalog<CrestApps.OrchardCore.Taxation.Models.TaxTable>>().CreateAsync(new CrestApps.OrchardCore.Taxation.Models.TaxTable
            {
                ItemId = "brackets",
                Name = "Brackets",
                EffectiveFromUtc = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Rows = [new CrestApps.OrchardCore.Taxation.Models.TaxTableRow { Minimum = 0, Rate = 0.1m }],
            });

            var rule = Rule("US-FIT", new DateOnly(2026, 1, 1), null, null);

            rule.Method = "Progressive";
            rule.TaxTableId = "brackets";
            rule.Category = PayrollTaxCategories.IncomeTax;
            await services.GetRequiredService<IPayrollTaxRuleManager>().CreateAsync(rule);
        });

        var (uses, result) = await harness.RunAsync(async services =>
        {
            var table = await services.GetRequiredService<INamedCatalog<CrestApps.OrchardCore.Taxation.Models.TaxTable>>().FindByIdAsync("brackets");
            var usages = await services.GetRequiredService<PayrollTaxRuleTableUsage>().GetUsagesAsync(table);
            var taxes = await services.GetServices<IPayrollTaxProvider>().Single().CalculateAsync(new PayrollTaxRequest
            {
                Employer = new Employer { Registrations = [new EmployerTaxRegistration { Jurisdiction = "US" }] },
                PayDate = new DateOnly(2026, 11, 20),
                PeriodsPerYear = 26,
                Wages = new Dictionary<string, decimal> { [PayrollTaxCategories.IncomeTax] = 1000 },
            });

            return (usages, taxes);
        }, commit: false);

        Assert.Equal(["the payroll tax rule US-FIT"], uses);
        Assert.Empty(result.Lines);
        Assert.Contains("not in effect", Assert.Single(result.Warnings), StringComparison.Ordinal);
    }

    private static async Task<EmployeePayProfile> SetUpAsync(CalendarTestHarness harness, string staffMemberId, FixedInputs inputs)
    {
        await harness.RunAsync(services => services.GetRequiredService<PayCodeSeeder>().SeedAsync());

        inputs.PayCodeId = (await harness.RunAsync(services => services.GetRequiredService<IPayCodeManager>().FindByCodeAsync("REG"), commit: false)).ItemId;

        return await harness.RunAsync(async services =>
        {
            var employers = services.GetRequiredService<IEmployerManager>();
            var employer = await employers.NewAsync();

            employer.Name = "Contoso";
            employer.Currency = "USD";
            employer.Registrations = [new EmployerTaxRegistration { Jurisdiction = "US" }];
            await employers.CreateAsync(employer);

            var schedules = services.GetRequiredService<IPayScheduleManager>();
            var schedule = await schedules.NewAsync();

            schedule.Name = "Every two weeks";
            schedule.AnchorDate = new DateOnly(2026, 1, 4);
            await schedules.CreateAsync(schedule);

            await services.GetRequiredService<IPayrollTaxRuleManager>().CreateAsync(Rule("US-SS", new DateOnly(2026, 1, 1), null, 6.2m));

            var profiles = services.GetRequiredService<IPayProfileManager>();
            var profile = await profiles.NewAsync();

            profile.StaffMemberId = staffMemberId;
            profile.EmployerId = employer.ItemId;
            profile.PayScheduleId = schedule.ItemId;
            profile.HomeAddress = new CrestApps.OrchardCore.Addresses.Models.Address { Country = "us", Region = "ma" };
            profile.Distribution = [new NetPayRow { Method = NetPayMethod.Check, Split = NetPaySplit.Remainder }];
            await profiles.CreateAsync(profile);

            return profile;
        });
    }

    private static PayrollTaxRule Rule(string code, DateOnly from, DateOnly? to, decimal? rate)
        => new()
        {
            ItemId = Guid.NewGuid().ToString("n")[..26],
            Name = code,
            TaxCode = code,
            Jurisdiction = "US",
            Category = PayrollTaxCategories.SocialInsurance,
            Payer = PayLinePayer.Employee,
            Method = "Percentage",
            Rate = rate,
            EffectiveFrom = from,
            EffectiveTo = to,
        };

    private static PayStatement Statement(string id, DateOnly payDate, PayStatementStatus status, decimal amount, decimal hours, DateOnly? periodStart = null)
        => new()
        {
            ItemId = id,
            StaffMemberId = "lee",
            EmployerId = "contoso",
            PayDate = payDate,
            PeriodStart = periodStart ?? payDate.AddDays(-19),
            PeriodEnd = (periodStart ?? payDate.AddDays(-19)).AddDays(13),
            Currency = "USD",
            Status = status,
            Lines =
            [
                new PayStatementLine { Type = PayLineType.Earning, PayCodeId = "reg", Amount = amount, Hours = hours },
                new PayStatementLine { Type = PayLineType.Tax, TaxCode = "US-SS", Amount = Math.Round(amount * 0.062m, 2), TaxableWages = amount },
            ],
        };

    private static PayPreviewsController Preview(IServiceProvider services, ClaimsPrincipal user)
        => new(
            services.GetRequiredService<IPayProfileManager>(),
            services.GetRequiredService<IPayScheduleManager>(),
            services.GetRequiredService<IPayrollScopeService>(),
            services.GetRequiredService<PayCalculationContextFactory>(),
            services.GetRequiredService<IPayCalculator>(),
            services.GetRequiredService<PayrollNames>(),
            new FixedOffsetLocalClock(() => DateTime.UtcNow, TimeSpan.Zero))
        {
            ControllerContext = Context(user),
        };

    private static PayrollTaxRulesController Rules(IServiceProvider services, ClaimsPrincipal user)
        => new(
            services.GetRequiredService<IPayrollTaxRuleManager>(),
            services.GetRequiredService<IAuthorizationService>(),
            Mock.Of<IUpdateModelAccessor>(),
            Mock.Of<IDisplayManager<PayrollTaxRule>>(),
            Mock.Of<INotifier>(),
            new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughHtmlLocalizer<PayrollTaxRulesController>(),
            new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<PayrollTaxRulesController>())
        {
            ControllerContext = Context(user),
        };

    private static ControllerContext Context(ClaimsPrincipal user)
        => new()
        {
            HttpContext = new DefaultHttpContext
            {
                User = user,
                RequestServices = new ServiceCollection()
                    .AddSingleton(Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory>())
                    .AddSingleton<Microsoft.AspNetCore.Mvc.ModelBinding.IModelMetadataProvider, Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider>()
                    .BuildServiceProvider(),
            },
        };

    private static Task<CalendarTestHarness> CreateHarnessAsync(Action<IServiceCollection> configure = null)
        => WorkforceTests.CreateAsync(
            services =>
            {
                services.AddDataProtection();
                services.AddTaxTablesCore();
                services.AddPayrollCore();
                services.AddPayrollTaxTablesCore();
                services.AddScoped<PayrollTaxRuleTableUsage>();
                services.AddSingleton<INamedCatalog<CrestApps.OrchardCore.Taxation.Models.TaxTable>>(new CrestApps.OrchardCore.Tests.Taxation.Fakes.InMemoryNamedCatalog<CrestApps.OrchardCore.Taxation.Models.TaxTable>());
                services.AddScoped<PayrollNames>();
                services.AddSingleton<ILocalClock>(new FixedOffsetLocalClock(() => DateTime.UtcNow, TimeSpan.Zero));
                configure?.Invoke(services);
            },
            async schemaBuilder =>
            {
                await PayrollIndexSchema.CreateAsync(schemaBuilder);
                await PayrollTaxRuleIndexSchema.CreateAsync(schemaBuilder);
            },
            [.. PayrollIndexSchema.Providers(), new PayrollTaxRuleIndexProvider()]);

    private sealed class FixedInputs : IPayInputService
    {
        public string PayCodeId { get; set; }

        public Task<IReadOnlyList<PayInput>> GetInputsAsync(PayInputQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PayInput>>(
            [
                .. query.StaffMemberIds.Select(staff => new PayInput
                {
                    SourceKind = "Test",
                    SourceId = "t1",
                    StaffMemberId = staff,
                    PayCodeId = PayCodeId,
                    Hours = 80,
                    PeriodStart = query.PeriodStart ?? default,
                    PeriodEnd = query.PeriodEnd ?? default,
                }),
            ]);

        public Task MarkConsumedAsync(PayInputConsumption consumption, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ReleaseAsync(PayInputConsumption consumption, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
