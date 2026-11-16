using CrestApps.Core.Services;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Payroll.Core.Calculation;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Core.UnitedStates;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Taxation.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// Contractors: backup withholding only when it applies, Form 1099-NEC above the year's threshold or when withheld, and
/// contractors kept out of the W-2, 941 and 940.
/// </summary>
public sealed class ContractorPaymentsTests
{
    [Fact]
    public async Task BackupWithholding_AppliesToAContractorOnlyWhenItShould_AndNeverToAnEmployee()
    {
        var rules = new[]
        {
            new IncomeTaxRule { ItemId = "bwh", TaxCode = Form1099Nec.BackupWithholdingCode, Name = "Backup withholding", Jurisdiction = "US", Category = IncomeTaxCategories.ContractorWithholding, Method = "Percentage", Rate = 24, EffectiveFrom = new DateOnly(2026, 1, 1) },
            new IncomeTaxRule { ItemId = "ss", TaxCode = "US-SS-EE", Name = "Social Security", Jurisdiction = "US", Category = IncomeTaxCategories.SocialInsurance, Method = "Percentage", Rate = 6.2m, EffectiveFrom = new DateOnly(2026, 1, 1) },
        };
        var manager = new Mock<IIncomeTaxRuleManager>();

        manager.Setup(candidate => candidate.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rules);

        var provider = new IncomeTaxRulesProvider(manager.Object, new DefaultTaxCalculationMethodProvider([new PercentageTaxCalculationMethod()]), new InMemoryNamedCatalog<TaxTable>(), [], new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<IncomeTaxRulesProvider>());

        IncomeTaxRequest Request(PayWorkerType type, bool withhold)
            => new()
            {
                RegisteredJurisdictions = ["US"],
                PayDate = new DateOnly(2026, 11, 20),
                PeriodsPerYear = 26,
                IncomeKind = type == PayWorkerType.Contractor ? IncomeKind.Contractor : IncomeKind.Employment,
                ContractorWithholding = withhold,
                Income = new Dictionary<string, decimal> { [IncomeTaxCategories.ContractorWithholding] = 1000, [IncomeTaxCategories.SocialInsurance] = 1000 },
            };

        var withheld = await provider.CalculateAsync(Request(PayWorkerType.Contractor, withhold: true), TestContext.Current.CancellationToken);
        var none = await provider.CalculateAsync(Request(PayWorkerType.Contractor, withhold: false), TestContext.Current.CancellationToken);
        var employee = await provider.CalculateAsync(Request(PayWorkerType.Employee, withhold: true), TestContext.Current.CancellationToken);

        // 24% of 1000, and no social insurance on a contractor.
        Assert.Equal((Form1099Nec.BackupWithholdingCode, 240m), (Assert.Single(withheld.Lines).TaxCode, withheld.Lines[0].Amount));
        Assert.Empty(none.Lines);
        Assert.Equal(["US-SS-EE"], employee.Lines.Select(line => line.TaxCode));
    }

    [Fact]
    public void AContractorWithNoTaxId_IsWithheldOn_AndOneWithATaxIdOnlyWhenTheProfileSaysSo()
    {
        var categories = new IncomeTaxCategoryOptions();

        categories.Categories.Add(new IncomeTaxCategory(IncomeTaxCategories.ContractorWithholding, global::OrchardCore.Localization.LocalizationSource.Create<ContractorPaymentsTests>("Backup withholding")));

        var step = new TaxesPayCalculationStep([], Options.Create(categories), new ServiceCollection().BuildServiceProvider(), new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<TaxesPayCalculationStep>());

        IncomeTaxRequest Request(EmployeePayProfile profile)
            => step.Request(new PayCalculationContext { Profile = profile, PayDate = new DateOnly(2026, 11, 20) });

        Assert.True(Request(new EmployeePayProfile { WorkerType = PayWorkerType.Contractor }).ContractorWithholding);
        Assert.False(Request(new EmployeePayProfile { WorkerType = PayWorkerType.Contractor, TaxId = new CrestApps.OrchardCore.SensitiveData.ProtectedValue { Cipher = "x", Hint = "1234" } }).ContractorWithholding);
        Assert.True(Request(new EmployeePayProfile { WorkerType = PayWorkerType.Contractor, BackupWithholding = true, TaxId = new CrestApps.OrchardCore.SensitiveData.ProtectedValue { Cipher = "x", Hint = "1234" } }).ContractorWithholding);
        Assert.False(Request(new EmployeePayProfile { WorkerType = PayWorkerType.Employee }).ContractorWithholding);
    }

    [Fact]
    public async Task Form1099Nec_ListsContractorsOverTheThresholdOrWithheldOn_AndTheW2AndReturnsLeaveThemOut()
    {
        await using var harness = await PayRunTests.CreateHarnessAsync(services =>
        {
            services.AddPayrollFilingsCore();
            services.AddPayrollFilingForm<Form941>();
            services.AddPayrollFilingForm<FormW2>();
            services.AddPayrollFilingForm<Form1099Nec>();
        });
        var setup = await PayRunTests.SetUpAsync(harness);
        var filer = CalendarTestHarness.User("filer", PayrollPermissions.ManagePayrollFilings.Name);
        var members = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var name in new[] { "Alex Design LLC", "Blake Small", "Casey Notin" })
        {
            members[name] = (await CrestApps.OrchardCore.Tests.Workforce.WorkforceTests.AddWorkerAsync(harness, name)).ItemId;
        }

        var (forms1099, w2, q4) = await harness.RunAsync(async services =>
        {
            var profiles = services.GetRequiredService<IPayProfileManager>();
            var codes = services.GetRequiredService<IPayCodeManager>();
            var regular = await codes.FindByCodeAsync("REG");
            var store = services.GetRequiredService<PayStatementStore>();
            var number = 0;

            async Task<EmployeePayProfile> ContractorAsync(string name)
            {
                var profile = await profiles.NewAsync();

                profile.StaffMemberId = members[name];
                profile.EmployerId = setup.EmployerId;
                profile.WorkerType = PayWorkerType.Contractor;
                profile.LegalName = name;
                profile.Distribution = [new NetPayRow { Method = NetPayMethod.Check, Split = NetPaySplit.Remainder }];
                await profiles.CreateAsync(profile);

                // A contractor is paid the amounts of invoices: no pay rate is asked for.
                Assert.DoesNotContain(await services.GetRequiredService<IPayProfileService>().GetBlockersAsync(profile), blocker => blocker.Code == "NoPayRate");

                return profile;
            }

            async Task StatementAsync(EmployeePayProfile profile, decimal paid, decimal withheld)
            {
                number++;

                var lines = new List<PayStatementLine> { new() { Type = PayLineType.Earning, PayCodeId = regular.ItemId, Amount = paid } };

                if (withheld > 0)
                {
                    lines.Add(new PayStatementLine { Type = PayLineType.Tax, TaxCode = Form1099Nec.BackupWithholdingCode, TaxCategory = IncomeTaxCategories.ContractorWithholding, Jurisdiction = "US", Payer = PayLinePayer.Employee, TaxableWages = paid, Amount = withheld });
                }

                await store.CreateAsync(new PayStatement
                {
                    ItemId = $"contractorstatement0000000{number}",
                    RunId = setup.RunId,
                    Number = $"C{number}",
                    EmployerId = setup.EmployerId,
                    StaffMemberId = profile.StaffMemberId,
                    ProfileId = profile.ItemId,
                    PeriodStart = new DateOnly(2026, 11, 1),
                    PeriodEnd = new DateOnly(2026, 11, 14),
                    PayDate = new DateOnly(2026, 11, 20),
                    Status = PayStatementStatus.Paid,
                    Currency = "USD",
                    Gross = paid,
                    Snapshot = new PayStatementSnapshot { EmployeeName = profile.LegalName },
                    Lines = lines,
                });
            }

            // Over the 2026 threshold of 2000; under it; under it but withheld on.
            await StatementAsync(await ContractorAsync("Alex Design LLC"), 2500, 0);
            await StatementAsync(await ContractorAsync("Blake Small"), 1500, 0);
            await StatementAsync(await ContractorAsync("Casey Notin"), 1500, 360);

            var service = services.GetRequiredService<IPayrollFilingService>();
            var employer = await services.GetRequiredService<IEmployerManager>().FindByIdAsync(setup.EmployerId);

            return (
                (await service.PrepareAsync(employer, Form1099Nec.FormName, 2026, 0, filer)).Filing,
                (await service.PrepareAsync(employer, FormW2.FormName, 2026, 0, filer)).Filing,
                (await service.PrepareAsync(employer, Form941.FormName, 2026, 4, filer)).Filing);
        });

        Assert.Equal(["Alex Design LLC", "Casey Notin"], forms1099.People.Select(person => person.Name).Order(StringComparer.Ordinal));
        Assert.Equal((2500m, 0m), (forms1099.People.Single(person => person.Name == "Alex Design LLC").Boxes["1"], forms1099.People.Single(person => person.Name == "Alex Design LLC").Boxes["4"]));
        Assert.Equal((2m, 4000m, 360m), (forms1099.Boxes["3"], forms1099.Boxes["5"], forms1099.Boxes["4"]));
        Assert.Contains(forms1099.Notes, note => note.Contains("no tax id", StringComparison.Ordinal));

        // No contractor on a wage statement or in the quarter's wages.
        Assert.Empty(w2.People);
        Assert.Equal(0m, q4.Boxes["2"]);

        var pdf = Form1099NecDocument.Write(forms1099, forms1099.People[0], new Employer { Name = "Contoso", TaxId = "12-3456789" }, ["1 Main St"], new Form1099NecTexts());

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.Equal(2000m, Form1099Nec.ThresholdFor(2026));
        Assert.Equal(600m, Form1099Nec.ThresholdFor(2025));
    }
}
