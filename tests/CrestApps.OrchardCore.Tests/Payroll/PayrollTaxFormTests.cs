using CrestApps.Core;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Payroll.Core.Canada;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Core.TaxForms;
using CrestApps.OrchardCore.Taxation.Core.TaxForms;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Payroll.Core.UnitedStates;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.DisplayManagement.Handlers;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// Tax forms made on the Tax Forms page (or by a recipe) rather than in code: the boxes their formulas work out from the
/// statements, with pre-tax deductions, voids, yearly values, totals, conditions, statements per jurisdiction, pay code
/// boxes and checks; what the editor refuses; and a form made through the manager prepared by the filing service.
/// </summary>
public sealed class PayrollTaxFormTests
{
    private static readonly PayCode _regular = Code("reg", PayCodeClass.Earning);

    // A 401(k): taken before income tax, after Social Security and Medicare; it reports to the form's box D.
    private static readonly PayCode _retirement = Code("k401", PayCodeClass.Deduction, settings =>
    {
        settings.TaxTreatment[IncomeTaxCategories.IncomeTax] = PayCodeTaxTreatment.Exempt;
        settings.TaxTreatment[IncomeTaxCategories.SocialInsurance] = PayCodeTaxTreatment.Taxed;
        settings.FormBoxes = ["TEST-1:D"];
    });

    private static readonly PayCode _tips = Code("tips", PayCodeClass.Earning, kind: PayCodeKinds.Tips);

    private static readonly Dictionary<string, PayCode> _codes = new(StringComparer.Ordinal)
    {
        [_regular.ItemId] = _regular,
        [_retirement.ItemId] = _retirement,
        [_tips.ItemId] = _tips,
    };

    [Fact]
    public void AFormMadeOnThePage_WorksOutWagesAfterPreTaxDeductions_TaxesWithheld_VoidsAndTheFilingTotals()
    {
        var form = WageForm();
        var statements = WageStatements();

        var filing = PayrollTaxFormRecipes.Prepare(form, statements, _codes, 2026, deposits:
        [
            new TaxDeposit { Jurisdiction = "US", Group = TaxLiabilityGroups.Withholding, Amount = 650 },
            new TaxDeposit { Jurisdiction = "US", Group = TaxLiabilityGroups.Unemployment, Amount = 50 },
            new TaxDeposit { Jurisdiction = "CA", Group = TaxLiabilityGroups.Withholding, Amount = 999 },
        ]);

        // Kim, then Lee, by name. Lee: 5,000 + 3,000 of pay, the 500 401(k) off the income tax wages only; tax 400 + 200.
        Assert.Equal(["Kim Ross", "Lee Park"], filing.People.Select(person => person.Name));

        var lee = filing.People[1];

        Assert.Equal((7500m, 8000m, 600m, 500m, 1500m), (lee.Boxes["W"], lee.Boxes["SS"], lee.Boxes["T"], lee.Boxes["D"], lee.Boxes["X"]));

        // Kim: 2,000, and a 500 statement voided by its reversal, which cancels it; no pay code reported to box D, so the
        // statement has no box D at all, while a formula box is kept at zero.
        var kim = filing.People[0];

        Assert.Equal((2000m, 2000m, 100m, 400m), (kim.Boxes["W"], kim.Boxes["SS"], kim.Boxes["T"], kim.Boxes["X"]));
        Assert.False(kim.Boxes.ContainsKey("D"));

        // The filing: the people's totals of the boxes added up on it (X is not), and filing formulas reading them, with
        // the period's US withholding deposits only.
        Assert.Equal((9500m, 10000m, 700m, 500m), (filing.Boxes["W"], filing.Boxes["SS"], filing.Boxes["T"], filing.Boxes["D"]));
        Assert.False(filing.Boxes.ContainsKey("X"));
        Assert.Equal((2m, 4750m, 650m, 50m, 0m), (filing.Boxes["N"], filing.Boxes["AVG"], filing.Boxes["DEP"], filing.Boxes["BAL"], filing.Boxes["OVER"]));

        // No profile, so no tax id on file: each reported person is noted once.
        Assert.Equal(["Kim Ross: no tax id.", "Lee Park: no tax id."], filing.Notes);
    }

    [Fact]
    public async Task AFormPostedFromTheEditor_KeepsItsRowsInOrder_SkipsEmptyRows_AndWorksOutTheFiling()
    {
        var driver = Driver();
        var model = new CrestApps.OrchardCore.Taxation.ViewModels.TaxFormViewModel
        {
            Name = " test-ui ",
            DisplayName = "Made on the page",
            Jurisdiction = "US",
            Period = "Quarter",
            Source = PayrollConstants.TaxFormSource,
            HasRecipientStatements = true,
            Enabled = true,
            RecipientCondition = "[W] > 0",
            Fields =
            [
                new() { Number = "W", Label = "Wages", Level = "Recipient", Formula = "WAGES('IncomeTax')", AddToFiling = true, Print = "Always" },
                new() { Number = "D", Label = "Deferrals", Level = "Recipient", AcceptsReportedAmounts = true, AddToFiling = true, Print = "WhenNotZero" },
                new(),
                new() { Number = "N", Label = "People", Level = "Filing", Formula = "RECIPIENTS()" },
                new() { Number = "TAX", Label = "Tax at the rate", Level = "Filing", Formula = "ROUND([W] * VALUE('Rate'), 2)" },
            ],
            Values = [new() { Name = "Rate", Year = 2026, Amount = 0.05m }, new()],
            Checks = [new() { Condition = "[N] > 5", Note = "Fewer than six people." }],
        };
        var updater = new FakeUpdateModel(model);
        var form = new TaxForm();

        await driver.UpdateAsync(form, new UpdateEditorContext(null, string.Empty, true, string.Empty, null, null, updater));

        Assert.Equal(0, updater.ModelState.ErrorCount);
        Assert.Equal(("TEST-UI", TaxFormPeriod.Quarter, "Payroll"), (form.Name, form.Period, form.Source));
        Assert.Equal(["W", "D", "N", "TAX"], form.Fields.Select(box => box.Number));
        Assert.Equal((TaxFormFieldLevel.Filing, TaxFormFieldPrint.WhenNotZero), (form.Fields[2].Level, form.Fields[1].Print));
        Assert.Equal(0.05m, Assert.Single(form.Values).Amount);

        // The pay codes report to the form's name and the box number.
        var retirement = Code("k401ui", PayCodeClass.Deduction, settings =>
        {
            settings.TaxTreatment[IncomeTaxCategories.IncomeTax] = PayCodeTaxTreatment.Exempt;
            settings.FormBoxes = ["TEST-UI:D"];
        });
        var codes = new Dictionary<string, PayCode>(_codes, StringComparer.Ordinal) { [retirement.ItemId] = retirement };
        var filing = PayrollTaxFormRecipes.Prepare(form, [Statement("lee", "Lee Park", new DateOnly(2026, 11, 20), Earn(5000), Deduct(300, "k401ui"))], codes, 2026, quarter: 4);

        Assert.Equal((4700m, 300m, 1m, 235m), (filing.Boxes["W"], filing.Boxes["D"], filing.Boxes["N"], filing.Boxes["TAX"]));
        Assert.Equal(["Fewer than six people."], filing.Notes);
    }

    [Fact]
    public async Task TheEditor_LeavesTheRulesToTheHandler_SoTheManagerRefusesWhatARecipeWouldBeRefused()
    {
        var driver = Driver();
        var updater = new FakeUpdateModel(new CrestApps.OrchardCore.Taxation.ViewModels.TaxFormViewModel
        {
            Name = "TEST-RULES",
            DisplayName = "Rules",
            Source = PayrollConstants.TaxFormSource,
            Jurisdiction = "US",
            Fields = [new() { Number = "A", Level = "Filing", Formula = "VALUE('Rate') + [A]" }],
            Values = [new() { Name = null, Year = 2026, Amount = 5 }],
        });
        var form = new TaxForm { ItemId = "rulesform0000000000000001" };

        await driver.UpdateAsync(form, new UpdateEditorContext(null, string.Empty, true, string.Empty, null, null, updater));

        // The driver adds no error of its own: the value without a name and the field that reads itself are the handler's.
        Assert.Equal(0, updater.ModelState.ErrorCount);

        await using var harness = await PayRunTests.CreateHarnessAsync(services => services.AddPayrollFilingsCore());

        var errors = await harness.RunAsync(async services => (await services.GetRequiredService<ITaxFormManager>().ValidateAsync(form)).Errors.Select(error => error.ErrorMessage).ToList());

        Assert.Contains(errors, error => error.Contains("yearly value", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("reads itself", StringComparison.Ordinal));
    }

    [Fact]
    public void AYearlyValue_IsTheLatestYearAtOrBeforeTheFilingsYear()
    {
        var form = WageForm();
        var lee = WageStatements().Where(statement => statement.StaffMemberId == "lee").ToList();

        // Rate is 10% from 2025 and 20% from 2026; before 2025 it has none, and the missing value works out to zero.
        Assert.Equal(750m, PayrollTaxFormRecipes.PersonFields(form, lee, _codes, 2025)["X"]);
        Assert.Equal(1500m, PayrollTaxFormRecipes.PersonFields(form, lee, _codes, 2026)["X"]);
        Assert.Equal(1500m, PayrollTaxFormRecipes.PersonFields(form, lee, _codes, 2030)["X"]);
        Assert.Equal(0m, PayrollTaxFormRecipes.PersonFields(form, lee, _codes, 2024)["X"]);
    }

    [Fact]
    public void TheCondition_TheirJurisdiction_AndAStatementPerJurisdiction_SayWhoIsReportedAndOnHowManyStatements()
    {
        var form = new TaxForm
        {
            Name = "TEST-2",
            DisplayName = "Provincial slips",
            Jurisdiction = "CA",
            Source = PayrollConstants.TaxFormSource,
            HasRecipientStatements = true,
            StatementPerJurisdiction = true,
            RecipientCondition = "[INC] >= VALUE('Minimum')",
            Values = [new TaxFormValue { Name = "Minimum", Year = 2000, Amount = 1000 }],
            Fields =
            [
                Person("INC", "WAGES('IncomeTax')", addToFiling: true),
                Person("PROV", "TAX('CA-*-PIT') - TAX('CA-QC-PIT')", addToFiling: true),
                Filing("SLIPS", "RECIPIENTS()"),
                Filing("PROV.CHECK", "TOTAL('PROV')"),
            ],
        };

        var statements = new[]
        {
            // Ana: Ontario in March, Alberta in June, and a June pay with no provincial line, which goes with Alberta.
            Statement("ana", "Ana Silva", new DateOnly(2026, 3, 13), Earn(3000), Tax("CA-ON-PIT", "CA-ON", 3000, 300)),
            Statement("ana", "Ana Silva", new DateOnly(2026, 6, 12), Earn(2000), Tax("CA-AB-PIT", "CA-AB", 2000, 150)),
            Statement("ana", "Ana Silva", new DateOnly(2026, 6, 26), Earn(400)),

            // Ben was paid less than the minimum; Cy paid no Canadian tax at all.
            Statement("ben", "Ben Hall", new DateOnly(2026, 3, 13), Earn(500), Tax("CA-ON-PIT", "CA-ON", 500, 20)),
            Statement("cy", "Cy Young", new DateOnly(2026, 3, 13), Earn(9000), Tax("US-FIT", "US", 9000, 900)),
        };

        form.Put(new PayrollTaxFormSettings { PeopleJurisdiction = "CA" });

        var filing = PayrollTaxFormRecipes.Prepare(form, statements, _codes, 2026);

        Assert.Equal([("CA-ON", 3000m, 300m), ("CA-AB", 2400m, 150m)], filing.People.Select(person => (person.Jurisdiction, person.Boxes["INC"], person.Boxes["PROV"])));
        Assert.Equal((5400m, 450m, 2m, 450m), (filing.Boxes["INC"], filing.Boxes["PROV"], filing.Boxes["SLIPS"], filing.Boxes["PROV.CHECK"]));
    }

    [Fact]
    public void Checks_AddTheirNoteBeforeThePeoplesNotes_WhenTheyFail()
    {
        var form = WageForm();

        form.MissingTaxIdNote = "{0}: no tax id.";
        form.Checks =
        [
            new TaxFormCheck { Condition = "ISACCOUNT(ACCOUNT('US'), '##-#######')", Note = "Give the employer's EIN." },
            new TaxFormCheck { Condition = "[BAL] = 0", Note = "Taxes are left to deposit." },
            new TaxFormCheck { Condition = "FILINGYEAR() >= 2020", Note = "Too old." },
        ];

        var unregistered = PayrollTaxFormRecipes.Prepare(form, WageStatements(), _codes, 2026);
        var registered = PayrollTaxFormRecipes.Prepare(form, WageStatements(), _codes, 2026, employer: new Employer
        {
            ItemId = "employer",
            Registrations = [new EmployerTaxRegistration { Jurisdiction = "US", AccountId = "12-3456789" }],
        }, deposits: [new TaxDeposit { Jurisdiction = "US", Group = TaxLiabilityGroups.Withholding, Amount = 700 }]);

        Assert.Equal(["Give the employer's EIN.", "Taxes are left to deposit.", "Kim Ross: no tax id.", "Lee Park: no tax id."], unregistered.Notes);
        Assert.Equal(["Kim Ross: no tax id.", "Lee Park: no tax id."], registered.Notes);
    }

    [Fact]
    public void AFormForContractors_LeavesEmployeesOut_AndTheOtherWayRound()
    {
        var form = WageForm();
        var profiles = new Dictionary<string, EmployeePayProfile>(StringComparer.Ordinal)
        {
            ["profile-kim"] = new() { ItemId = "profile-kim", StaffMemberId = "kim", WorkerType = PayWorkerType.Contractor },
            ["profile-lee"] = new() { ItemId = "profile-lee", StaffMemberId = "lee", WorkerType = PayWorkerType.Employee },
        };

        form.Put(new PayrollTaxFormSettings { Workers = PayrollTaxFormWorkers.Contractors });

        var contractors = PayrollTaxFormRecipes.Prepare(form, WageStatements(), _codes, 2026, profiles: profiles);

        form.Put(new PayrollTaxFormSettings { Workers = PayrollTaxFormWorkers.Employees });

        var employees = PayrollTaxFormRecipes.Prepare(form, WageStatements(), _codes, 2026, profiles: profiles);

        Assert.Equal(["Kim Ross"], contractors.People.Select(person => person.Name));
        Assert.Equal(["Lee Park"], employees.People.Select(person => person.Name));
        Assert.Equal((2000m, 7500m), (contractors.Boxes["W"], employees.Boxes["W"]));
    }

    [Fact]
    public void TheShippedW2_TakesTheDeductionOffBox1_AndPutsItInBox12D_FromThePayCodeBox()
    {
        var w2 = PayrollTaxFormRecipes.Form(PayrollTaxFormRecipes.UnitedStates, UnitedStatesTaxForms.FormW2);
        var retirement = Code("k401w2", PayCodeClass.Deduction, settings =>
        {
            settings.TaxTreatment[IncomeTaxCategories.IncomeTax] = PayCodeTaxTreatment.Exempt;
            settings.FormBoxes = ["W2:12D"];
        });
        var codes = new Dictionary<string, PayCode>(_codes, StringComparer.Ordinal) { [retirement.ItemId] = retirement };
        var statements = new[]
        {
            Statement("lee", "Lee Park", new DateOnly(2026, 11, 20), Earn(5000), Earn(400, "tips"), Deduct(500, "k401w2"),
                Tax(UnitedStatesTaxCodes.IncomeTax, "US", 4900, 400), Tax(UnitedStatesTaxCodes.SocialSecurity, "US", 5400, 334.80m), Tax(UnitedStatesTaxCodes.Medicare, "US", 5400, 78.30m)),
        };

        var filing = PayrollTaxFormRecipes.Prepare(w2, statements, codes, 2026);
        var lee = Assert.Single(filing.People);

        // Box 1: 5,400 of pay less the 500 deferral; box 3 Social Security wages without the tips, which are box 7.
        Assert.Equal((4900m, 400m, 5000m, 334.80m, 5400m, 78.30m, 400m, 500m), (lee.Boxes["1"], lee.Boxes["2"], lee.Boxes["3"], lee.Boxes["4"], lee.Boxes["5"], lee.Boxes["6"], lee.Boxes["7"], lee.Boxes["12D"]));

        // Only the box 12 code a pay code reported is on the statement and the W-3, and printed.
        Assert.DoesNotContain("12E", lee.Boxes.Keys);
        Assert.Equal((500m, 1m), (filing.Boxes["12D"], filing.Boxes["c"]));
        Assert.Contains(PayrollTaxStatementDocument.TextsFor(w2, lee, new Employer()).Boxes, box => box.Label.StartsWith("12 Code D", StringComparison.Ordinal) && box.Amount == 500m);
    }

    [Theory]
    [InlineData("[NOPE] + 1", "NOPE")]
    [InlineData("WAGES('IncomeTax'", null)]
    [InlineData("MADEUP(1)", "MADEUP")]
    [InlineData("SUM([W])", "aggregate")]
    public void TheEditor_RefusesAFormulaThatDoesntWork(string formula, string expected)
    {
        var form = WageForm();

        form.Fields.Add(Filing("BAD", formula));

        Assert.Null(TaxFormFormulas.Compile(form, PayrollTaxFormRecipes.PayrollFunctions(), new PassThroughStringLocalizer<TaxForm>(), out var errors));

        var error = Assert.Single(errors);

        Assert.Equal("BAD", error.Field);

        if (expected is not null)
        {
            Assert.Contains(expected, error.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void TheEditor_RefusesBoxesThatReadEachOther_TwoBoxesOfOneNumber_AndAFilingBoxNamedLikeATotal()
    {
        static IList<(string Field, string Message)> Errors(params TaxFormField[] boxes)
        {
            var form = new TaxForm { Name = "T", DisplayName = "T", Jurisdiction = "US", Fields = boxes };

            Assert.Null(TaxFormFormulas.Compile(form, PayrollTaxFormRecipes.PayrollFunctions(), new PassThroughStringLocalizer<TaxForm>(), out var errors));

            return errors;
        }

        Assert.Contains(Errors(Filing("A", "[B] + 1"), Filing("B", "[C]"), Filing("C", "[A]")), error => error.Message.Contains("reads itself", StringComparison.Ordinal));
        Assert.Contains(Errors(Person("1", "GROSS()"), Person("1", "0")), error => error.Message.Contains("Two recipient fields", StringComparison.Ordinal));
        Assert.Contains(Errors(Person("1", "GROSS()", addToFiling: true), Filing("1", "0")), error => error.Field == "1");

        // A person box and a filing box may share a number when the person box isn't added up on the filing (Form 1096).
        var form = new TaxForm { Name = "T", DisplayName = "T", Jurisdiction = "US", Fields = [Person("4", "TAX('US-BWH')"), Filing("4", "TOTAL('4')")] };

        Assert.NotNull(TaxFormFormulas.Compile(form, PayrollTaxFormRecipes.PayrollFunctions(), new PassThroughStringLocalizer<TaxForm>(), out _));
    }

    [Theory]
    [InlineData("US-FIT", "US-FIT", true)]
    [InlineData("us-fit", "US-FIT", true)]
    [InlineData("CA-ON-PIT", "CA-*-PIT", true)]
    [InlineData("CA-QC-PIT", "CA-*-PIT", true)]
    [InlineData("CA-PIT", "CA-*-PIT", false)]
    [InlineData("CA-FIT", "CA-*-PIT", false)]
    [InlineData("US-SS-ER", "US-*", true)]
    [InlineData("US-SS-ER", "*-ER", true)]
    [InlineData("US-SS-EE", "*-ER", false)]
    public void ATaxCodePattern_MatchesWithAStar(string code, string pattern, bool matches)
        => Assert.Equal(matches, PayrollFilingSums.Matches(code, pattern));

    [Theory]
    [InlineData("123456789RP0001", "#########RP####", true)]
    [InlineData("123456789 rp 0001", "#########RP####", true)]
    [InlineData("12345678RP0001", "#########RP####", false)]
    [InlineData("1234567890TQ0001", "##########TQ####", true)]
    [InlineData("12-3456789", "##-#######", true)]
    [InlineData("AB-3456789", "@@-#######", true)]
    [InlineData(null, "#", false)]
    public void AnAccountNumber_HasAShape(string account, string shape, bool has)
        => Assert.Equal(has, TaxFormFunctions.HasShape(account, shape));

    [Fact]
    public async Task TheShippedRecipes_AddEveryFormThroughTheManager_AndTheFormsAreWorkedOutTheSameAsTheirChecksSay()
    {
        await using var harness = await PayRunTests.CreateHarnessAsync(services =>
        {
            services.AddPayrollFilingsCore();
            services.AddTaxFormFunctions<CanadaTaxFormFunctions>();
        });

        // SeedAsync fails when the manager refuses any form, so every shipped formula compiles.
        await PayrollTaxFormRecipes.SeedAsync(harness, PayrollTaxFormRecipes.UnitedStates);
        await PayrollTaxFormRecipes.SeedAsync(harness, PayrollTaxFormRecipes.Canada);

        var names = await harness.RunAsync(async services => (await services.GetRequiredService<ITaxFormManager>().GetAllAsync()).Select(form => form.Name).Order(StringComparer.Ordinal).ToList());

        Assert.Equal(["CA-RL1", "CA-T4", "US-1099NEC", "US-940", "US-941", "US-945", "US-W2"], names);

        // Running a recipe again adds nothing that is there.
        await harness.RunAsync(async services => Assert.Empty(await PayrollTaxFormRecipeSeeder.SeedAsync(services.GetRequiredService<ITaxFormManager>(), PayrollTaxFormRecipes.Forms(PayrollTaxFormRecipes.UnitedStates), null)));
    }

    [Fact]
    public async Task TheManager_RefusesAFormWhoseFormulasDontWork_OrWhoseNameIsTaken()
    {
        await using var harness = await PayRunTests.CreateHarnessAsync(services => services.AddPayrollFilingsCore());

        await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ITaxFormManager>();
            var first = WageForm();

            Assert.True((await manager.ValidateAsync(first)).Succeeded);
            await manager.CreateAsync(first);

            var taken = WageForm();

            taken.ItemId = "anotherform00000000000001";
            taken.Name = "test-1";

            var broken = WageForm();

            broken.ItemId = "brokenform000000000000001";
            broken.Name = "TEST-BROKEN";
            broken.Fields.Add(Filing("Z", "DENTALCODE()"));

            Assert.Contains((await manager.ValidateAsync(taken)).Errors, error => error.ErrorMessage.Contains("Another tax form", StringComparison.Ordinal));
            Assert.Contains((await manager.ValidateAsync(broken)).Errors, error => error.ErrorMessage.Contains("DENTALCODE", StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public async Task AFormMadeThroughTheManager_IsOfferedToRegisteredEmployers_PreparedByTheFilingService_AndOffWhenTurnedOff()
    {
        await using var harness = await PayRunTests.CreateHarnessAsync(services => services.AddPayrollFilingsCore());
        var setup = await PayRunTests.SetUpAsync(harness);
        var filer = CalendarTestHarness.User("filer", PayrollPermissions.ManagePayrollFilings.Name);

        var (offered, prepared, off, broken) = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ITaxFormManager>();
            var form = WageForm();

            form.Name = "TEST-SERVICE";
            await manager.CreateAsync(form);

            var profile = (await services.GetRequiredService<IPayProfileManager>().ListByEmployersAsync([setup.EmployerId])).Single();

            await services.GetRequiredService<PayStatementStore>().CreateAsync(new PayStatement
            {
                ItemId = "statementtaxform000000001",
                RunId = setup.RunId,
                Number = "1",
                EmployerId = setup.EmployerId,
                StaffMemberId = setup.StaffMemberId,
                ProfileId = profile.ItemId,
                PeriodStart = new DateOnly(2026, 11, 1),
                PeriodEnd = new DateOnly(2026, 11, 14),
                PayDate = new DateOnly(2026, 11, 20),
                Status = PayStatementStatus.Paid,
                Currency = "USD",
                Gross = 5000,
                Snapshot = new PayStatementSnapshot { EmployeeName = "Lee Park" },
                Lines =
                [
                    new PayStatementLine { Type = PayLineType.Earning, PayCodeId = setup.BonusCodeId, Amount = 5000 },
                    Tax(UnitedStatesTaxCodes.IncomeTax, "US", 5000, 400),
                    Tax(UnitedStatesTaxCodes.SocialSecurity, "US", 5000, 310),
                ],
            });

            var employer = await services.GetRequiredService<IEmployerManager>().FindByIdAsync(setup.EmployerId);
            var service = services.GetRequiredService<IPayrollFilingService>();
            var forms = await service.FormsForAsync(employer);
            var result = await service.PrepareAsync(employer, "test-service", 2026, 0, filer);

            form.Enabled = false;
            await manager.UpdateAsync(form);

            var stillOffered = (await service.FormsForAsync(employer)).Any(candidate => candidate.Name == "TEST-SERVICE");

            // A form whose formula stops working (a function whose feature was turned off) is an error of the action.
            var gone = WageForm();

            gone.Name = "TEST-GONE";
            gone.Fields.Add(Filing("Z", "DENTALCODE()"));
            await manager.CreateAsync(gone);

            return (forms.Select(candidate => candidate.Name).ToList(), result.Filing, stillOffered, await service.PrepareAsync(employer, "TEST-GONE", 2026, 0, filer));
        });

        Assert.Contains("TEST-SERVICE", offered);
        Assert.Equal(("TEST-SERVICE", 5000m, 400m, 1m), (prepared.Form, prepared.Boxes["W"], prepared.Boxes["T"], prepared.Boxes["N"]));
        Assert.Single(prepared.People);
        Assert.False(off);
        Assert.False(broken.Succeeded);
        Assert.Contains(broken.Errors, error => error.Contains("TEST-GONE", StringComparison.Ordinal));
    }

    private static CrestApps.OrchardCore.Taxation.Drivers.TaxFormDisplayDriver Driver()
    {
        var sources = new TaxFormSourceOptions();

        sources.Sources.Add(new TaxFormSource(PayrollConstants.TaxFormSource, null, null, null));

        return new CrestApps.OrchardCore.Taxation.Drivers.TaxFormDisplayDriver(
            [],
            PayrollTaxFormRecipes.Functions(),
            Microsoft.Extensions.Options.Options.Create(sources),
            Mock.Of<Microsoft.Extensions.Localization.IStringLocalizerFactory>(),
            new PassThroughStringLocalizer<CrestApps.OrchardCore.Taxation.Drivers.TaxFormDisplayDriver>());
    }

    private static TaxForm WageForm()
    {
        var form = new TaxForm
        {
            ItemId = "testform00000000000000001",
            Name = "TEST-1",
            DisplayName = "Test wage statement",
            Jurisdiction = "US",
            Period = TaxFormPeriod.Year,
            Source = PayrollConstants.TaxFormSource,
            HasRecipientStatements = true,
            MissingTaxIdNote = "{0}: no tax id.",
            Values =
            [
                new TaxFormValue { Name = "Rate", Year = 2025, Amount = 0.10m },
                new TaxFormValue { Name = "Rate", Year = 2026, Amount = 0.20m },
            ],
            Fields =
            [
                Person("W", "WAGES('IncomeTax')", addToFiling: true),
                Person("SS", "TAXABLE('US-SS-EE')", addToFiling: true),
                Person("T", "TAX('US-FIT')", addToFiling: true),
                new TaxFormField { Number = "D", Label = "D Deferrals", Level = TaxFormFieldLevel.Recipient, AcceptsReportedAmounts = true, AddToFiling = true, Print = TaxFormFieldPrint.WhenNotZero },
                Person("X", "ROUND([W] * VALUE('Rate'), 2)"),

                // The filing boxes read the people's totals, in any order.
                Filing("AVG", "ROUND([W] / [N], 2)"),
                Filing("N", "RECIPIENTS()"),
                Filing("DEP", "DEPOSITS('US', 'Withholding')"),
                Filing("BAL", "GREATEST(0, [T] - [DEP])"),
                Filing("OVER", "GREATEST(0, [DEP] - [T])"),
            ],
        };

        form.Put(new PayrollTaxFormSettings { Workers = PayrollTaxFormWorkers.Everyone });

        return form;
    }

    private static List<PayStatement> WageStatements()
        =>
        [
            Statement("lee", "Lee Park", new DateOnly(2026, 11, 20), Earn(5000), Deduct(500, "k401"), Tax("US-FIT", "US", 4500, 400), Tax("US-SS-EE", "US", 5000, 310)),
            Statement("lee", "Lee Park", new DateOnly(2026, 12, 18), Earn(3000), Tax("US-FIT", "US", 3000, 200), Tax("US-SS-EE", "US", 3000, 186)),
            Statement("kim", "Kim Ross", new DateOnly(2026, 11, 20), Earn(2000), Tax("US-FIT", "US", 2000, 100), Tax("US-SS-EE", "US", 2000, 124)),
            Statement("kim", "Kim Ross", new DateOnly(2026, 12, 4), Earn(500), Tax("US-FIT", "US", 500, 25), Tax("US-SS-EE", "US", 500, 31)),
            Void(Statement("kim", "Kim Ross", new DateOnly(2026, 12, 11), Earn(-500), Tax("US-FIT", "US", -500, -25), Tax("US-SS-EE", "US", -500, -31))),
        ];

    private static PayStatement Statement(string person, string name, DateOnly payDate, params PayStatementLine[] lines)
        => new()
        {
            ItemId = $"{person}{payDate:yyyyMMdd}".PadRight(26, '0')[..26],
            StaffMemberId = person,
            ProfileId = "profile-" + person,
            EmployerId = "employer",
            PeriodStart = payDate.AddDays(-13),
            PeriodEnd = payDate,
            PayDate = payDate,
            Status = PayStatementStatus.Paid,
            Gross = lines.Where(line => line.Type == PayLineType.Earning).Sum(line => line.Amount),
            Snapshot = new PayStatementSnapshot { EmployeeName = name },
            Lines = [.. lines],
        };

    private static PayStatement Void(PayStatement statement)
    {
        statement.VoidOf = "voided";

        return statement;
    }

    private static PayStatementLine Earn(decimal amount, string code = "reg")
        => new() { Type = PayLineType.Earning, PayCodeId = code, Amount = amount };

    private static PayStatementLine Deduct(decimal amount, string code)
        => new() { Type = PayLineType.Deduction, PayCodeId = code, Amount = amount };

    private static PayStatementLine Tax(string code, string jurisdiction, decimal taxable, decimal amount)
        => new() { Type = PayLineType.Tax, TaxCode = code, Jurisdiction = jurisdiction, Payer = PayLinePayer.Employee, TaxableWages = taxable, Amount = amount };

    private static TaxFormField Person(string key, string formula, bool addToFiling = false)
        => new() { Number = key, Label = key, Level = TaxFormFieldLevel.Recipient, Formula = formula, AddToFiling = addToFiling, Print = TaxFormFieldPrint.Always };

    private static TaxFormField Filing(string key, string formula)
        => new() { Number = key, Label = key, Level = TaxFormFieldLevel.Filing, Formula = formula };

    private static PayCode Code(string id, PayCodeClass payCodeClass, Action<PayrollPayCodeSettings> configure = null, string kind = PayCodeKinds.Regular)
    {
        var code = new PayCode { ItemId = id, Code = id.ToUpperInvariant(), Name = id, Class = payCodeClass, Kind = kind };
        var settings = PayrollPayCodeSettingsExtensions.DefaultsFor(code);

        configure?.Invoke(settings);
        code.Put(settings);

        return code;
    }
}
