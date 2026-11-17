using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.Core;
using CrestApps.Core.Handlers;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Banking.Checks.Core.Handlers;
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.Recipes;
using CrestApps.OrchardCore.Banking.DirectDeposit;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Recipes;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Recipes;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Recipes;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Core.Services;
using CrestApps.OrchardCore.Taxation.Core.UnitedStates;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Recipes;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Tests.Banking;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// The Payroll sample recipe: it builds on the shipped federal income tax recipe, every step runs without errors (twice,
/// without duplicates), and what it creates is ready to pay: the employer with its registrations and ACH funding account,
/// the check layout, the schedule, four staff members with their pay and pay profiles without blockers, the deposit
/// accounts, the 401(k) item and the child support order. A pay run of the current period then calculates everyone.
/// </summary>
public sealed class PayrollSampleRecipeTests
{
    private const string EmployerId = "4gn8cjc0e2ffzb2kyq1gjwaf3m";
    private const string FundingAccountId = "49rj58f0yqys0k5nvjtxz8e0m5";
    private const string RileyProfileId = "4nettp6gcxecw3avr24e27e4fv";

    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public void TheSampleRecipe_IsListedForPayroll_AndRunsTheShippedFederalRecipe()
    {
        var recipe = Load(RecipePath("CrestApps.OrchardCore.Payroll", "payroll-sample.recipe.json"));
        var steps = recipe["steps"]!.AsArray().OfType<JsonObject>().ToList();
        var features = steps.Single(step => step["name"]!.GetValue<string>() == "feature")["enable"]!.AsArray().Select(feature => feature!.GetValue<string>()).ToList();
        var nested = steps.Single(step => step["name"]!.GetValue<string>() == "recipes")["Values"]!.AsArray().Select(value => value!["name"]!.GetValue<string>()).ToList();

        Assert.Equal(("PayrollSample", "Payroll sample", false), (recipe["name"]!.GetValue<string>(), recipe["displayName"]!.GetValue<string>(), recipe["issetuprecipe"]!.GetValue<bool>()));
        Assert.Contains("payroll", recipe["categories"]!.AsArray().Select(category => category!.GetValue<string>()));
        Assert.Contains("sample", recipe["tags"]!.AsArray().Select(tag => tag!.GetValue<string>()));
        Assert.Contains(PayrollConstants.Features.Payroll, features);
        Assert.Contains(PayrollConstants.Features.UnitedStates, features);
        Assert.Contains(PayrollConstants.Features.Garnishments, features);
        Assert.Contains(PayrollConstants.Features.SelfService, features);
        Assert.Contains(CrestApps.OrchardCore.Banking.Disbursements.DisbursementsConstants.Features.DirectDeposit, features);
        Assert.Contains(CrestApps.OrchardCore.Banking.Checks.ChecksConstants.Features.Checks, features);

        // The federal rules come from the shipped recipe, run by name: it must still be named so.
        Assert.Equal(["IncomeTaxUnitedStatesFederal2026"], nested);
        Assert.Equal("IncomeTaxUnitedStatesFederal2026", Load(RecipePath("CrestApps.OrchardCore.Taxation", "income-tax-united-states-2026.recipe.json"))["name"]!.GetValue<string>());

        // Every id a step expects is a valid UniqueId, so it is kept and the records keep pointing at each other.
        foreach (var id in recipe.ToJsonString().Split('"').Where(token => token.Length == 26 && token.StartsWith('4') && token.All(char.IsAsciiLetterOrDigit)))
        {
            Assert.True(UniqueId.IsValid(id), $"'{id}' is not a valid UniqueId.");
        }
    }

    [Fact]
    public async Task TheSampleRecipe_RunsCleanlyTwice_AndCreatesAPayrollReadyToRun()
    {
        var recipe = Load(RecipePath("CrestApps.OrchardCore.Payroll", "payroll-sample.recipe.json"));
        var banking = new BankingTestContext();

        await using var harness = await PayRunTests.CreateHarnessAsync(services =>
        {
            services.AddGarnishmentsCore();
            services.AddScoped<IncomeTaxRuleTableUsage>();
            services.AddElectionFieldSet(UnitedStatesElectionFields.Create());
            services.AddIncomeTaxWithholdingMethod<UnitedStatesFormW4WithholdingMethod>();
            services.AddSingleton<IBankAccountManager>(banking.Manager);
            services.AddSingleton<IBankAccountService>(banking.Service);
            services.AddSingleton<INamedCatalog<CheckLayout>>(new InMemoryNamedCatalog<CheckLayout>());
            services.AddScoped<ICatalogEntryHandler<CheckLayout>, CheckLayoutHandler>();
            services.AddScoped<INamedCatalogManager<CheckLayout>>(provider => new NamedCatalogManager<CheckLayout>(
                provider.GetRequiredService<INamedCatalog<CheckLayout>>(),
                provider.GetServices<ICatalogEntryHandler<CheckLayout>>(),
                provider.GetRequiredService<ILogger<NamedCatalogManager<CheckLayout>>>()));

            // The Staff step reads a user only for a user name, which the sample doesn't give.
            services.AddScoped(_ => new UserManager<IUser>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null));
        });

        // What the feature step's migrations seed on a site.
        await harness.RunAsync(async services =>
        {
            await services.GetRequiredService<PayCodeSeeder>().SeedAsync(TestContext.Current.CancellationToken);
            await services.GetRequiredService<PayrollPayCodeSeeder>().SeedAsync(TestContext.Current.CancellationToken);
            await services.GetRequiredService<GarnishmentPayCodeSeeder>().SeedAsync();
        });

        var handlers = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            [PayrollConstants.PayCodesRecipeStepName] = typeof(PayCodesRecipeStep),
            [PayrollConstants.PaySchedulesRecipeStepName] = typeof(PaySchedulesRecipeStep),
            ["Staff"] = typeof(StaffRecipeStep),
            ["BankAccounts"] = typeof(BankAccountsRecipeStep),
            [PayrollConstants.EmployersRecipeStepName] = typeof(EmployersRecipeStep),
            ["CheckLayouts"] = typeof(CheckLayoutsRecipeStep),
            [PayrollConstants.PayProfilesRecipeStepName] = typeof(PayProfilesRecipeStep),
            [PayrollConstants.PayItemsRecipeStepName] = typeof(PayItemsRecipeStep),
            [PayrollConstants.GarnishmentsRecipeStepName] = typeof(GarnishmentsRecipeStep),
            [TaxationConstants.IncomeTaxRulesRecipeStepName] = typeof(IncomeTaxRulesRecipeStep),
        };
        var errors = new List<string>();

        for (var run = 0; run < 2; run++)
        {
            foreach (var step in recipe["steps"]!.AsArray().OfType<JsonObject>())
            {
                var name = step["name"]!.GetValue<string>();

                if (name == "feature")
                {
                    continue;
                }

                if (name == "recipes")
                {
                    foreach (var value in step["Values"]!.AsArray())
                    {
                        errors.AddRange(await ImportTaxRecipeAsync(harness, value!["name"]!.GetValue<string>()));
                    }

                    continue;
                }

                Assert.True(handlers.ContainsKey(name), $"The sample has a step no test handler runs: {name}.");

                errors.AddRange(await harness.RunAsync(async services =>
                {
                    var context = new RecipeExecutionContext { Name = name, Step = step.DeepClone().AsObject() };

                    await ((NamedRecipeStepHandler)ActivatorUtilities.CreateInstance(services, handlers[name])).ExecuteAsync(context);

                    return context.Errors;
                }));
            }
        }

        Assert.Empty(errors);

        // The funding account is the business's own, verified, with its Bank file (ACH) settings; people's accounts are theirs.
        Assert.Equal(4, banking.Store.Entries.Count);

        var funding = banking.Store.Entries.Single(account => account.ItemId == FundingAccountId);

        Assert.True(funding.Owner.IsEmpty);
        Assert.Equal((BankAccountStatus.Verified, "021000021"), (funding.Status, funding.RoutingNumber));
        Assert.True(funding.TryGet<NachaOriginSettings>(out var nacha));
        Assert.Equal((true, "1123456789", "CONTOSO INC", "PAYROLL"), (nacha.Enabled, nacha.CompanyIdentification, nacha.CompanyName, nacha.CompanyEntryDescription));
        Assert.All(banking.Store.Entries.Where(account => account.ItemId != FundingAccountId), account =>
        {
            Assert.Equal("Staff", account.Owner.Kind);
            Assert.Equal(BankAccountStatus.Verified, account.Status);
        });

        var cancellationToken = TestContext.Current.CancellationToken;

        var (employer, schedule, staff, profiles, blockers, items, order, codes) = await harness.RunAsync(async services =>
        {
            var employer = Assert.Single(await services.GetRequiredService<IEmployerManager>().GetAllAsync(cancellationToken));
            var schedule = Assert.Single(await services.GetRequiredService<IPayScheduleManager>().GetAllAsync(cancellationToken));
            var (_, staff) = await services.GetRequiredService<IStaffMemberManager>().SearchAsync(new StaffQuery(), 0, 50);
            var profiles = (await services.GetRequiredService<IPayProfileManager>().GetAllAsync(cancellationToken)).ToList();
            var service = services.GetRequiredService<IPayProfileService>();
            var blockers = new List<string>();

            foreach (var profile in profiles)
            {
                blockers.AddRange((await service.GetBlockersAsync(profile, cancellationToken)).Select(blocker => $"{profile.ItemId}: {blocker.Message}"));
            }

            var items = (await services.GetRequiredService<IPayItemManager>().GetAllAsync(cancellationToken)).ToList();
            var order = Assert.Single(await services.GetRequiredService<IGarnishmentOrderManager>().GetAllAsync(cancellationToken));
            var codes = (await services.GetRequiredService<IPayCodeManager>().GetAllAsync(cancellationToken)).ToDictionary(code => code.ItemId, code => code.Code);

            return (employer, schedule, staff.ToList(), profiles, blockers, items, order, codes);
        }, commit: false);

        Assert.Equal((EmployerId, "Contoso", "Contoso, Inc.", "USD", FundingAccountId), (employer.ItemId, employer.Name, employer.LegalName, employer.Currency, employer.FundingBankAccountId));
        Assert.Equal(["US", "US-TX"], employer.Registrations.Select(registration => registration.Jurisdiction));
        Assert.Equal(2.7m, employer.Registrations[1].RateOn(new DateOnly(2026, 11, 16)));
        Assert.Equal("TX", employer.Address.Region);

        Assert.Equal(("Every two weeks", PayFrequency.Biweekly), (schedule.Name, schedule.Frequency));

        var period = schedule.PeriodOn(new DateOnly(2026, 11, 16));
        var payDate = schedule.PayDateFor(period.End);

        Assert.Equal((new DateOnly(2026, 11, 8), new DateOnly(2026, 11, 21), new DateOnly(2026, 11, 27)), (period.Start, period.End, payDate));
        Assert.Equal(DayOfWeek.Friday, payDate.DayOfWeek);

        Assert.Equal(["Avery Lee", "Jordan Smith", "Riley Chen", "Sam Rivera"], staff.Select(member => member.DisplayName).Order());
        Assert.All(staff, member => Assert.True(member.TryGet<StaffPayRates>(out _)));
        Assert.Equal(78000m, PayOf(staff, "Avery Lee").SalaryOn(period.End));
        Assert.Equal((StaffPayBasis.Hourly, 24m), (PayOf(staff, "Jordan Smith").PayBasis, PayOf(staff, "Jordan Smith").RateOn(period.End)));

        Assert.Equal(4, profiles.Count);
        Assert.All(profiles, profile => Assert.Equal(EmployerId, profile.EmployerId));
        Assert.All(profiles, profile => Assert.Equal(schedule.ItemId, profile.PayScheduleId));
        Assert.All(profiles, profile => Assert.True(profile.TaxId is { HasValue: true }));
        Assert.Empty(blockers);
        Assert.Equal(3, profiles.Count(profile => profile.Distribution.Single().Method == NetPayMethod.BankAccount));
        Assert.Equal(NetPayMethod.Check, profiles.Single(profile => profile.Distribution.Single().Method == NetPayMethod.Check).Distribution.Single().Method);

        var item = Assert.Single(items);

        Assert.Equal((RileyProfileId, "401K", 5m, 50m), (item.ProfileId, codes[item.PayCodeId], item.Percent, item.EmployerMatchPercent));
        Assert.Equal((RileyProfileId, EmployerId, GarnishmentType.Support, 150m, "CSUP"), (order.ProfileId, order.EmployerId, order.Type, order.Amount, codes[order.PayCodeId]));
        Assert.False(string.IsNullOrEmpty(order.StaffMemberId));

        var layout = Assert.Single(await harness.RunAsync(async services => (await services.GetRequiredService<INamedCatalogManager<CheckLayout>>().GetAllAsync()).ToList(), commit: false));

        Assert.Equal(("Contoso paychecks", CheckStock.Blank, "Contoso, Inc."), (layout.Name, layout.Stock, layout.PayerName));

        // A pay run of the current period pays everyone, once Jordan's hours are entered.
        var preparer = CalendarTestHarness.User("preparer", PayrollPermissions.PreparePayRuns.Name);
        var jordanId = staff.Single(member => member.DisplayName == "Jordan Smith").ItemId;

        var runId = await harness.RunAsync(async services =>
        {
            var runs = services.GetRequiredService<IPayRunManager>();
            var run = await runs.NewAsync(cancellationToken: cancellationToken);

            run.EmployerId = EmployerId;
            run.PayScheduleId = schedule.ItemId;
            run.PeriodStart = period.Start;
            run.PeriodEnd = period.End;
            run.PayDate = payDate;

            var opened = await services.GetRequiredService<IPayRunService>().OpenAsync(run, preparer, cancellationToken);

            Assert.True(opened.Succeeded, string.Join(" ", opened.Errors));

            var inputs = services.GetRequiredService<IPayRunInputManager>();

            foreach (var (code, hours) in new[] { ("REG", 80m), ("OT", 4m) })
            {
                var input = await inputs.NewAsync(cancellationToken: cancellationToken);

                input.RunId = run.ItemId;
                input.StaffMemberId = jordanId;
                input.PayCodeId = codes.Single(entry => entry.Value == code).Key;
                input.Hours = hours;
                await inputs.CreateAsync(input, cancellationToken);
            }

            return run.ItemId;
        });

        var (calculated, statements) = await harness.RunAsync(async services =>
        {
            var service = services.GetRequiredService<IPayRunService>();
            var run = await services.GetRequiredService<IPayRunManager>().FindByIdAsync(runId, cancellationToken);
            var result = await service.CalculateAsync(run, preparer, cancellationToken: cancellationToken);

            return (run, result.Succeeded ? (await service.ListStatementsAsync(run, cancellationToken)).ToList() : throw new InvalidOperationException(string.Join(" ", result.Errors)));
        });

        Assert.Equal(PayRunStatus.Calculated, calculated.Status);
        Assert.DoesNotContain(calculated.Issues, issue => issue.IsBlocker);
        Assert.Equal(4, statements.Count);
        Assert.All(statements, statement => Assert.True(statement.Net > 0, $"{statement.StaffMemberId} is paid {statement.Net}."));
        Assert.All(statements, statement => Assert.Contains(statement.Lines, line => line.TaxCode == "US-SS-EE"));

        var jordan = statements.Single(statement => statement.StaffMemberId == jordanId);

        Assert.Equal(2064m, jordan.Gross);
        Assert.Equal(144m, jordan.Lines.Single(line => line.PayCodeId == codes.Single(entry => entry.Value == "OT").Key).Amount);

        var riley = statements.Single(statement => statement.ProfileId == RileyProfileId);

        Assert.Equal(2500m, riley.Gross);
        Assert.Equal(125m, riley.Lines.Single(line => line.Type == PayLineType.Deduction && CodeOf(line) == "401K").Amount);
        Assert.Equal(62.50m, riley.Lines.Single(line => line.Type == PayLineType.EmployerContribution && CodeOf(line) == "401K").Amount);
        Assert.Equal(150m, riley.Lines.Single(line => CodeOf(line) == "CSUP").Amount);

        // Texas unemployment at the employer's own 2.7% registration rate, under the year's 9,000 wage base.
        Assert.Equal(67.50m, riley.Lines.Single(line => line.TaxCode == "US-TX-SUI").Amount);

        Assert.Equal(NetPayMethod.Check, Assert.Single(statements.Single(statement => statement.Gross == 1600m).Payments).Method);
        Assert.Equal(3, statements.Count(statement => statement.Payments.All(payment => payment.Method == NetPayMethod.BankAccount)));

        // Tax lines have no pay code.
        string CodeOf(PayStatementLine line)
            => line.PayCodeId is { } id ? codes.GetValueOrDefault(id) : null;
    }

    // Runs the steps of a shipped income tax recipe the way a site does: its tax tables into the catalog and its rules
    // through the rules step. Its feature step is left to the sample's.
    private static async Task<IReadOnlyList<string>> ImportTaxRecipeAsync(CalendarTestHarness harness, string recipeName)
    {
        var directory = Path.Combine(RepositoryRoot(), "src", "Modules", "CrestApps.OrchardCore.Taxation", "Recipes");
        var recipe = Directory.EnumerateFiles(directory, "*.recipe.json")
            .Select(Load)
            .Single(candidate => candidate["name"]!.GetValue<string>() == recipeName);
        var steps = recipe["steps"]!.AsArray().OfType<JsonObject>().ToList();

        Assert.All(steps, step => Assert.Contains(step["name"]!.GetValue<string>(), new[] { "Feature", "TaxTable", TaxationConstants.IncomeTaxRulesRecipeStepName }));

        var tables = steps.Single(step => step["name"]!.GetValue<string>() == "TaxTable")["TaxTables"].Deserialize<List<TaxTable>>(_options);
        var rules = steps.Single(step => step["name"]!.GetValue<string>() == TaxationConstants.IncomeTaxRulesRecipeStepName);

        return await harness.RunAsync(async services =>
        {
            var catalog = services.GetRequiredService<INamedCatalog<TaxTable>>();

            foreach (var table in tables)
            {
                if (await catalog.FindByIdAsync(table.ItemId) is null)
                {
                    await catalog.CreateAsync(table);
                }
            }

            var context = new RecipeExecutionContext { Name = TaxationConstants.IncomeTaxRulesRecipeStepName, Step = rules.DeepClone().AsObject() };

            await ActivatorUtilities.CreateInstance<IncomeTaxRulesRecipeStep>(services).ExecuteAsync(context);

            return (IReadOnlyList<string>)[.. context.Errors];
        });
    }

    private static StaffPayRates PayOf(IEnumerable<StaffMember> staff, string name)
    {
        Assert.True(staff.Single(member => member.DisplayName == name).TryGet<StaffPayRates>(out var pay));

        return pay;
    }

    private static JsonObject Load(string path)
        => JsonNode.Parse(File.ReadAllText(path))!.AsObject();

    private static string RecipePath(string module, string fileName)
        => Path.Combine(RepositoryRoot(), "src", "Modules", module, "Recipes", fileName);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "src", "Modules", "CrestApps.OrchardCore.Payroll", "Recipes", "payroll-sample.recipe.json")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return directory.FullName;
    }
}
