using System.Xml.Linq;
using CrestApps.Core;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Payroll.Core;
using CrestApps.OrchardCore.Payroll.Core.Calculation;
using CrestApps.OrchardCore.Payroll.Core.Canada;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.DataSources;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Taxation.Core.Canada;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.DataSources;
using OrchardCore.DataSources.Files;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// Canada Payroll Filings: T4 and RL-1 slips and summaries from the statements, source deductions remitted to the agency
/// that collects them on the employer's remitter schedule, and the T4 XML file.
/// </summary>
public sealed class CanadaPayrollFilingsTests
{
    [Fact]
    public async Task T4_IsWorkedOutFromTheStatements_AndProvincialTaxIsRemittedToTheCraWithTheRest()
    {
        await using var harness = await CreateHarnessAsync();
        var (setup, employer) = await SeedAsync(harness, quebec: false);
        var filer = CalendarTestHarness.User("filer", PayrollPermissions.ManagePayrollFilings.Name);

        var (t4, liabilities) = await harness.RunAsync(async services =>
        {
            var deposits = services.GetRequiredService<ITaxDepositManager>();
            var deposit = await deposits.NewAsync();

            deposit.EmployerId = setup.EmployerId;
            deposit.Jurisdiction = "CA";
            deposit.PeriodStart = new DateOnly(2026, 11, 16);
            deposit.PeriodEnd = new DateOnly(2026, 11, 30);
            deposit.Amount = 600;
            deposit.PaidOn = new DateOnly(2026, 12, 10);
            await deposits.CreateAsync(deposit);

            return (
                (await services.GetRequiredService<IPayrollFilingService>().PrepareAsync(employer, FormT4.FormName, 2026, 0, filer)).Filing,
                await services.GetRequiredService<ITaxLiabilityService>().ListAsync(setup.EmployerId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
        });

        // Box 14 the wages, 16 CPP, 18 EI, 22 federal and Ontario tax together, 24 and 26 the bases.
        var lee = Assert.Single(t4.People);

        Assert.Equal((5000m, 290.49m, 81.50m, 900m, 5000m, 5000m), (lee.Boxes["14"], lee.Boxes["16"], lee.Boxes["18"], lee.Boxes["22"], lee.Boxes["24"], lee.Boxes["26"]));
        Assert.Equal("6789", lee.TaxIdHint);

        // Box 10, stored with the slip: the province of the statements' provincial tax.
        Assert.Equal("CA-ON", lee.Jurisdiction);

        // The Summary: the employer's CPP and EI (1.4 times), total deductions, the remittance and the balance.
        Assert.Equal((290.49m, 114.10m, 1676.58m, 600m, 1076.58m, 1m), (t4.Boxes["27"], t4.Boxes["19"], t4.Boxes["80"], t4.Boxes["82"], t4.Boxes["86"], t4.Boxes["88"]));
        Assert.Empty(t4.Notes);

        // One CRA period for everything, Ontario's tax included, twice a month: the 16th to the 30th due 10 December.
        var period = Assert.Single(liabilities);

        Assert.Equal(("CA", TaxLiabilityGroups.Withholding, DepositSchedules.TwiceMonthly), (period.Jurisdiction, period.Group, period.Schedule));
        Assert.Equal((new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 30), new DateOnly(2026, 12, 10)), (period.PeriodStart, period.PeriodEnd, period.DueDate));
        Assert.Equal((1676.58m, 600m, 1076.58m), (period.Owed, period.Deposited, period.Balance));
    }

    [Fact]
    public async Task Rl1_AndT4_ForAQuebecEmployee_AndRevenuQuebecIsOwedItsOwnDeductions()
    {
        await using var harness = await CreateHarnessAsync();
        var (setup, employer) = await SeedAsync(harness, quebec: true);
        var filer = CalendarTestHarness.User("filer", PayrollPermissions.ManagePayrollFilings.Name);

        var (rl1, t4, liabilities) = await harness.RunAsync(async services =>
        {
            var filings = services.GetRequiredService<IPayrollFilingService>();

            return (
                (await filings.PrepareAsync(employer, FormRl1.FormName, 2026, 0, filer)).Filing,
                (await filings.PrepareAsync(employer, FormT4.FormName, 2026, 0, filer)).Filing,
                await services.GetRequiredService<ITaxLiabilityService>().ListAsync(setup.EmployerId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
        });

        var slip = Assert.Single(rl1.People);

        Assert.Equal((5000m, 298.04m, 65.00m, 500m, 5000m, 21.50m, 5000m), (slip.Boxes["A"], slip.Boxes["B.A"], slip.Boxes["C"], slip.Boxes["E"], slip.Boxes["G"], slip.Boxes["H"], slip.Boxes["I"]));
        Assert.Equal((298.04m, 30.10m), (rl1.Boxes["QPP.Employer"], rl1.Boxes["QPIP.Employer"]));

        // The employer's health services fund contribution is on the RL-1 Summary and owed to Revenu Québec.
        Assert.Equal((82.50m, 500m + 298.04m + 21.50m + 298.04m + 30.10m + 82.50m), (rl1.Boxes["HSF"], rl1.Boxes["Owed"]));

        // The T4 has the QPP in box 17 and the federal tax only (never Quebec's) in box 22.
        var t4Slip = Assert.Single(t4.People);

        Assert.Equal((298.04m, 0m, 65.00m, 400m, 21.50m), (t4Slip.Boxes["17"], t4Slip.Boxes["16"], t4Slip.Boxes["18"], t4Slip.Boxes["22"], t4Slip.Boxes["55"]));

        // The CRA is owed the federal tax and EI; Revenu Québec its tax, QPP and QPIP, monthly.
        var cra = Assert.Single(liabilities, item => item.Jurisdiction == "CA");
        var quebec = Assert.Single(liabilities, item => item.Jurisdiction == "CA-QC");

        Assert.Equal(400m + 65.00m + 91.00m, cra.Owed);
        Assert.Equal((500m + 298.04m + 298.04m + 21.50m + 30.10m + 82.50m, new DateOnly(2026, 12, 15)), (quebec.Owed, quebec.DueDate));
    }

    [Theory]
    [InlineData(DepositSchedules.QuarterlyMidMonth, "2026-11-20", "2026-10-01", "2026-12-31", "2027-01-15")]
    [InlineData(DepositSchedules.TwiceMonthly, "2026-11-06", "2026-11-01", "2026-11-15", "2026-11-25")]
    [InlineData(DepositSchedules.TwiceMonthly, "2026-11-20", "2026-11-16", "2026-11-30", "2026-12-10")]
    [InlineData(DepositSchedules.FourTimesMonthly, "2026-11-06", "2026-11-01", "2026-11-07", "2026-11-11")]
    [InlineData(DepositSchedules.FourTimesMonthly, "2026-11-27", "2026-11-22", "2026-11-30", "2026-12-03")]
    public void RemitterSchedules_GiveThePeriodAndItsDueDate(string schedule, string payDate, string start, string end, string due)
    {
        var (first, last, dueDate) = TaxLiabilityService.Period(schedule, DateOnly.Parse(payDate, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal((DateOnly.Parse(start, System.Globalization.CultureInfo.InvariantCulture), DateOnly.Parse(end, System.Globalization.CultureInfo.InvariantCulture), DateOnly.Parse(due, System.Globalization.CultureInfo.InvariantCulture)), (first, last, dueDate));
    }

    [Fact]
    public async Task TheTaxesStep_KeepsWhichAgencyATaxIsRemittedTo()
    {
        var provider = new FixedProvider(new IncomeTaxLine { TaxCode = "CA-ON-PIT", Name = "Ontario income tax", Jurisdiction = "CA-ON", CollectedBy = "CA", Category = IncomeTaxCategories.IncomeTax, TaxableIncome = 1000, Amount = 50 },
            new IncomeTaxLine { TaxCode = "CA-QC-PIT", Name = "Quebec income tax", Jurisdiction = "CA-QC", CollectedBy = "CA-QC", Category = IncomeTaxCategories.IncomeTax, TaxableIncome = 1000, Amount = 60 });
        var step = new TaxesPayCalculationStep([provider], Options.Create(new IncomeTaxCategoryOptions()), new ServiceCollection().BuildServiceProvider(), new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<TaxesPayCalculationStep>());
        var context = new PayCalculationContext { Profile = new EmployeePayProfile(), PayDate = new DateOnly(2026, 11, 20) };

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal([("CA-ON", "CA"), ("CA-QC", null)], context.Lines.Select(line => (line.Jurisdiction, line.RemittedTo)));
    }

    [Fact]
    public async Task T4_HasASlipForEachProvinceOfEmployment_AndNotesAMissingDentalBenefitsCode()
    {
        await using var harness = await CreateHarnessAsync();
        var (setup, employer) = await SeedAsync(harness, quebec: false);
        var filer = CalendarTestHarness.User("filer", PayrollPermissions.ManagePayrollFilings.Name);

        var t4 = await harness.RunAsync(async services =>
        {
            var regular = await services.GetRequiredService<IPayCodeManager>().FindByCodeAsync("REG");
            var profile = (await services.GetRequiredService<IPayProfileManager>().ListByEmployersAsync([setup.EmployerId])).Single();

            // Lee moves to British Columbia in December: a second slip, box 10 BC.
            await services.GetRequiredService<PayStatementStore>().CreateAsync(new PayStatement
            {
                ItemId = "castatement000000000000002",
                RunId = setup.RunId,
                Number = "2",
                EmployerId = setup.EmployerId,
                StaffMemberId = setup.StaffMemberId,
                ProfileId = profile.ItemId,
                PeriodStart = new DateOnly(2026, 11, 29),
                PeriodEnd = new DateOnly(2026, 12, 12),
                PayDate = new DateOnly(2026, 12, 18),
                Status = PayStatementStatus.Paid,
                Currency = "CAD",
                Gross = 3000,
                Snapshot = new PayStatementSnapshot { EmployeeName = "Lee Park" },
                Lines =
                [
                    new PayStatementLine { Type = PayLineType.Earning, PayCodeId = regular.ItemId, Amount = 3000 },
                    new PayStatementLine { Type = PayLineType.Tax, TaxCode = CanadaIncomeTax.TaxCodes.FederalIncomeTax, TaxCategory = IncomeTaxCategories.IncomeTax, Jurisdiction = "CA", Payer = PayLinePayer.Employee, TaxableWages = 3000, Amount = 340 },
                    new PayStatementLine { Type = PayLineType.Tax, TaxCode = "CA-BC-PIT", TaxCategory = IncomeTaxCategories.IncomeTax, Jurisdiction = "CA-BC", RemittedTo = "CA", Payer = PayLinePayer.Employee, TaxableWages = 3000, Amount = 140 },
                    new PayStatementLine { Type = PayLineType.Tax, TaxCode = CanadaIncomeTax.TaxCodes.Cpp, TaxCategory = IncomeTaxCategories.SocialInsurance, Jurisdiction = "CA", Payer = PayLinePayer.Employee, TaxableWages = 3000, Amount = 170.49m },
                    new PayStatementLine { Type = PayLineType.Tax, TaxCode = CanadaIncomeTax.TaxCodes.EmploymentInsurance, TaxCategory = CanadaIncomeTax.Categories.InsurableEarnings, Jurisdiction = "CA", Payer = PayLinePayer.Employee, TaxableWages = 3000, Amount = 48.90m },
                ],
            });

            employer.Put(new CanadaEmployerSettings());

            return (await services.GetRequiredService<IPayrollFilingService>().PrepareAsync(employer, FormT4.FormName, 2026, 0, filer)).Filing;
        });

        Assert.Equal([("CA-ON", 5000m, 900m), ("CA-BC", 3000m, 480m)], t4.People.Select(person => (person.Jurisdiction, person.Boxes["14"], person.Boxes["22"])));
        Assert.Equal((8000m, 2m), (t4.Boxes["14"], t4.Boxes["88"]));
        Assert.Contains(t4.Notes, note => note.Contains("box 45", StringComparison.Ordinal));
    }

    [Fact]
    public void ByProvinceOfEmployment_PutsAStatementWithNoProvinceWithTheOneBefore()
    {
        static PayStatement Statement(int day, string province)
            => new()
            {
                PayDate = new DateOnly(2026, 3, day),
                Lines = province is null ? [] : [new PayStatementLine { Type = PayLineType.Tax, TaxCode = province + "-PIT", Jurisdiction = province, Amount = 1 }],
            };

        var groups = FormT4.ByProvinceOfEmployment([Statement(20, "CA-AB"), Statement(6, null), Statement(13, "CA-ON"), Statement(27, null)]);

        // The first statement has none before it: the first province after it.
        Assert.Equal([("ON", 2), ("AB", 2)], groups.Select(group => (group.Province, group.Statements.Count)));
    }

    [Fact]
    public void TheTaxProvider_IsTheOneNamed_ElseTheOnlyOne_ElseTheOnlyOneBesidesTheBuiltInRules()
    {
        var builtIn = new NamedProvider(CrestApps.OrchardCore.Taxation.Core.Services.IncomeTaxRulesProvider.ProviderName);
        var engine = new NamedProvider("Engine");
        var other = new NamedProvider("Other");

        Assert.Same(builtIn, TaxesPayCalculationStep.ChooseProvider([builtIn], null));
        Assert.Same(engine, TaxesPayCalculationStep.ChooseProvider([builtIn, engine], null));
        Assert.Same(builtIn, TaxesPayCalculationStep.ChooseProvider([builtIn, engine], builtIn.Name));
        Assert.Null(TaxesPayCalculationStep.ChooseProvider([builtIn, engine, other], null));
        Assert.Null(TaxesPayCalculationStep.ChooseProvider([builtIn, engine], "Gone"));
    }

    [Fact]
    public void TheTaxRequest_CarriesTheEmployersRegistrations_WithTheirRateOnThePayDate()
    {
        var employer = new Employer
        {
            Registrations =
            [
                new EmployerTaxRegistration { Jurisdiction = "CA-QC", AccountId = "1234567890TQ0001", Rates = [new DatedRate { EffectiveFrom = new DateOnly(2026, 1, 1), Rate = 2.31m }, new DatedRate { EffectiveFrom = new DateOnly(2026, 12, 1), Rate = 2.5m }] },
                new EmployerTaxRegistration { Jurisdiction = "CA", AccountId = "123456789RP0001" },
            ],
        };
        var step = new TaxesPayCalculationStep([], Options.Create(new IncomeTaxCategoryOptions()), new ServiceCollection().BuildServiceProvider(), new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<TaxesPayCalculationStep>());
        var request = step.Request(new PayCalculationContext { Employer = employer, PayDate = new DateOnly(2026, 11, 20) });

        Assert.Equal([("CA-QC", "1234567890TQ0001", (decimal?)2.31m), ("CA", "123456789RP0001", null)], request.Registrations.Select(registration => (registration.Jurisdiction, registration.AccountId, registration.Rate)));
    }

    [Theory]
    [InlineData("046 454 286", true)]
    [InlineData("046-454-286", true)]
    [InlineData("130 692 544", true)]
    [InlineData("046 454 287", false)]
    [InlineData("046 454 28", false)]
    [InlineData("046 454 2860", false)]
    [InlineData("000 000 000", false)]
    [InlineData("O46 454 286", false)]
    public void ASocialInsuranceNumber_PassesItsCheckDigit(string sin, bool valid)
        => Assert.Equal(valid, CanadaSin.IsValid(sin));

    [Fact]
    public void ThePayProfileEditor_RefusesASinThatFailsItsCheckDigit_ForACanadianEmployerOnly()
    {
        var canadian = new Employer { Registrations = [new EmployerTaxRegistration { Jurisdiction = "CA" }] };
        var american = new Employer { CountryPack = "US" };

        Assert.True(CrestApps.OrchardCore.Payroll.Drivers.PayProfileDisplayDriver.RefusesTaxId(canadian, "046 454 287"));
        Assert.False(CrestApps.OrchardCore.Payroll.Drivers.PayProfileDisplayDriver.RefusesTaxId(canadian, "046 454 286"));
        Assert.True(CrestApps.OrchardCore.Payroll.Drivers.PayProfileDisplayDriver.RefusesTaxId(new Employer { CountryPack = "CA" }, "123-45-6789"));
        Assert.False(CrestApps.OrchardCore.Payroll.Drivers.PayProfileDisplayDriver.RefusesTaxId(american, "123-45-6789"));
    }

    [Fact]
    public async Task T4Xml_WritesEveryRequiredElement_AndNoEmptyOne()
    {
        var protection = new EphemeralDataProtectionProvider();
        var format = new T4XmlDataFileFormat(protection);
        var row = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["Year"] = 2026,
            ["BusinessNumber"] = "123456789RP0001",
            ["EmployerName"] = "Contoso Canada",
            ["ContactName"] = "Pat Lee",
            ["ContactPhone"] = "867 555 0100",
            ["DentalBenefitsCode"] = 1,
            ["StaffMemberId"] = "kim",
            [T4SlipsDataSet.ProtectedSinField] = FileTaxIds.Protect(protection, "130 692 544"),

            // One name only, no address, and no EI or CPP earnings (an employee exempt from both).
            ["LastName"] = "Kim",
            ["ProvinceOfEmployment"] = "NU",
            ["Box14"] = 1200m,
        };
        var fields = row.Keys.Select(name => new DataField(name, name, DataFieldType.Text)).ToList();

        using var stream = new MemoryStream();

        await format.WriteAsync(stream, fields, Batches(fields, row), new DataFileOptions(), TestContext.Current.CancellationToken);

        var xml = XDocument.Parse(System.Text.Encoding.UTF8.GetString(stream.ToArray()).TrimStart('\uFEFF'));
        var slip = xml.Descendants("T4Slip").Single();

        // The T4 XML specification's required elements of a slip, checked one by one (the CRA's XSD is not shipped
        // with the tests), with boxes 24 and 26 written at zero rather than left to be read as box 14.
        foreach (var path in new[] { "EMPE_NM/snm", "EMPE_ADDR/cntry_cd", "sin", "bn", "rpt_tcd", "empt_prov_cd", "empr_dntl_ben_rpt_cd", "T4_AMT/empt_incamt", "T4_AMT/ei_insu_ern_amt", "T4_AMT/cpp_qpp_ern_amt" })
        {
            Assert.True(path.Split('/').Aggregate((XElement)slip, (element, name) => element?.Element(name)) is not null, path);
        }

        Assert.Equal(("130692544", "1", "0.00", "0.00"), (slip.Element("sin")!.Value, slip.Element("empr_dntl_ben_rpt_cd")!.Value, slip.Element("T4_AMT")!.Element("ei_insu_ern_amt")!.Value, slip.Element("T4_AMT")!.Element("cpp_qpp_ern_amt")!.Value));

        // And the Summary's.
        var summary = xml.Descendants("T4Summary").Single();

        foreach (var path in new[] { "bn", "EMPR_NM/l1_nm", "tx_yr", "slp_cnt", "rpt_tcd", "T4_TAMT/tot_empt_incamt", "T4_TAMT/tot_itx_ddct_amt" })
        {
            Assert.True(path.Split('/').Aggregate((XElement)summary, (element, name) => element?.Element(name)) is not null, path);
        }

        // An optional element with nothing to say is left out, never written empty.
        Assert.Empty(xml.Descendants().Where(element => !element.HasElements && string.IsNullOrWhiteSpace(element.Value)).Select(element => element.Name.LocalName));
        Assert.Null(slip.Element("EMPE_NM")!.Element("gvn_nm"));

        // A SIN that fails its check digit, no dental benefits code or no province of employment: refused.
        async Task<string> RefusedAsync(string name, object value)
        {
            var changed = new Dictionary<string, object>(row, StringComparer.Ordinal) { [name] = value };

            using var refused = new MemoryStream();

            return (await Assert.ThrowsAsync<InvalidOperationException>(() => format.WriteAsync(refused, fields, Batches(fields, changed), new DataFileOptions(), TestContext.Current.CancellationToken))).Message;
        }

        Assert.Contains("check digit", await RefusedAsync(T4SlipsDataSet.ProtectedSinField, FileTaxIds.Protect(protection, "130 692 545")), StringComparison.Ordinal);
        Assert.Contains("dental benefits code", await RefusedAsync("DentalBenefitsCode", null), StringComparison.Ordinal);
        Assert.Contains("province of employment", await RefusedAsync("ProvinceOfEmployment", null), StringComparison.Ordinal);
        Assert.Contains("contact name and phone", await RefusedAsync("ContactPhone", null), StringComparison.Ordinal);

        // A SIN typed into the encrypted field, or encrypted by another site, is no SIN.
        Assert.Contains("full social insurance number", await RefusedAsync(T4SlipsDataSet.ProtectedSinField, "130 692 544"), StringComparison.Ordinal);
        Assert.Contains("full social insurance number", await RefusedAsync(T4SlipsDataSet.ProtectedSinField, FileTaxIds.Protect(new EphemeralDataProtectionProvider(), "130 692 544")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task T4Xml_WritesTheSlipsAndSummary_AndRefusesAMissingNumber()
    {
        var protection = new EphemeralDataProtectionProvider();
        var format = new T4XmlDataFileFormat(protection);
        var values = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["Year"] = 2026,
            ["BusinessNumber"] = "123456789 RP 0001",
            ["EmployerName"] = "Contoso Canada",
            ["EmployerAddress"] = "1 King St W",
            ["EmployerCity"] = "Toronto",
            ["EmployerProvince"] = "ON",
            ["EmployerPostalCode"] = "M5H 1A1",
            ["ContactName"] = "Pat Lee",
            ["ContactPhone"] = "416-555-0100",
            ["EmployerCpp"] = 290.49m,
            ["EmployerCpp2"] = 0m,
            ["EmployerEi"] = 114.10m,
            ["DentalBenefitsCode"] = 3,
            ["StaffMemberId"] = "lee",
            [T4SlipsDataSet.ProtectedSinField] = FileTaxIds.Protect(protection, "046 454 286"),
            ["FirstName"] = "Lee",
            ["LastName"] = "Park",
            ["Address"] = "2 Bay St",
            ["City"] = "Toronto",
            ["Province"] = "ON",
            ["PostalCode"] = "M5J 2N8",
            ["ProvinceOfEmployment"] = "ON",
            ["Box14"] = 5000m,
            ["Box16"] = 290.49m,
            ["Box18"] = 81.50m,
            ["Box22"] = 900m,
            ["Box24"] = 5000m,
            ["Box26"] = 5000m,
        };

        async Task<string> WriteAsync(Dictionary<string, object> row)
        {
            var fields = row.Keys.Select(name => new DataField(name, name, DataFieldType.Text)).ToList();

            using var stream = new MemoryStream();

            await format.WriteAsync(stream, fields, Batches(fields, row), new DataFileOptions(), TestContext.Current.CancellationToken);

            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }

        var xml = XDocument.Parse((await WriteAsync(values)).TrimStart('﻿'));
        var slip = xml.Descendants("T4Slip").Single();
        var summary = xml.Descendants("T4Summary").Single();

        Assert.Equal("123456789RP0001", xml.Descendants("bn15").Single().Value);
        Assert.Equal(("046454286", "Park", "ON", "5000.00", "290.49", "900.00"), (slip.Element("sin")!.Value, slip.Element("EMPE_NM")!.Element("snm")!.Value, slip.Element("empt_prov_cd")!.Value, slip.Element("T4_AMT")!.Element("empt_incamt")!.Value, slip.Element("T4_AMT")!.Element("cpp_cntrb_amt")!.Value, slip.Element("T4_AMT")!.Element("itx_ddct_amt")!.Value));
        Assert.Equal(("2026", "1", "290.49", "114.10"), (summary.Element("tx_yr")!.Value, summary.Element("slp_cnt")!.Value, summary.Element("T4_TAMT")!.Element("tot_empr_cpp_amt")!.Value, summary.Element("T4_TAMT")!.Element("tot_empr_eip_amt")!.Value));

        // No full SIN, or no RP account: refused rather than written.
        await Assert.ThrowsAsync<InvalidOperationException>(() => WriteAsync(new Dictionary<string, object>(values, StringComparer.Ordinal) { [T4SlipsDataSet.ProtectedSinField] = null }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => WriteAsync(new Dictionary<string, object>(values, StringComparer.Ordinal) { ["BusinessNumber"] = "123456789" }));
    }

    private static async IAsyncEnumerable<DataBatch> Batches(IReadOnlyList<DataField> fields, Dictionary<string, object> row)
    {
        await Task.Yield();

        yield return new DataBatch(fields, [[.. fields.Select(field => row[field.Name])]]);
    }

    private static async Task<(PayRunTests.Setup Setup, Employer Employer)> SeedAsync(CalendarTestHarness harness, bool quebec)
    {
        var setup = await PayRunTests.SetUpAsync(harness);

        var employer = await harness.RunAsync(async services =>
        {
            var employers = services.GetRequiredService<IEmployerManager>();
            var employer = await employers.FindByIdAsync(setup.EmployerId);

            employer.Currency = "CAD";
            employer.Put(new CanadaEmployerSettings { DentalBenefitsCode = 3 });
            employer.Registrations =
            [
                new EmployerTaxRegistration { Jurisdiction = "CA", Agency = "CRA", AccountId = "123456789RP0001", DepositFrequency = DepositSchedules.TwiceMonthly },
                new EmployerTaxRegistration { Jurisdiction = "CA-QC", Agency = "Revenu Québec", AccountId = "1234567890TQ0001" },
            ];
            await employers.UpdateAsync(employer);

            var regular = await services.GetRequiredService<IPayCodeManager>().FindByCodeAsync("REG");
            var profile = (await services.GetRequiredService<IPayProfileManager>().ListByEmployersAsync([setup.EmployerId])).Single();

            static PayStatementLine Tax(string code, string jurisdiction, string category, PayLinePayer payer, decimal taxable, decimal amount, string remittedTo = null)
                => new() { Type = PayLineType.Tax, TaxCode = code, TaxCategory = category, Jurisdiction = jurisdiction, RemittedTo = remittedTo, Payer = payer, TaxableWages = taxable, Amount = amount };

            List<PayStatementLine> lines = quebec
                ?
                [
                    new PayStatementLine { Type = PayLineType.Earning, PayCodeId = regular.ItemId, Amount = 5000 },
                    Tax(CanadaIncomeTax.TaxCodes.FederalIncomeTax, "CA", IncomeTaxCategories.IncomeTax, PayLinePayer.Employee, 5000, 400),
                    Tax(CanadaIncomeTax.TaxCodes.QuebecIncomeTax, "CA-QC", IncomeTaxCategories.IncomeTax, PayLinePayer.Employee, 5000, 500),
                    Tax(CanadaIncomeTax.TaxCodes.Qpp, "CA-QC", IncomeTaxCategories.SocialInsurance, PayLinePayer.Employee, 5000, 298.04m),
                    Tax(CanadaIncomeTax.TaxCodes.Qpp + "-ER", "CA-QC", IncomeTaxCategories.SocialInsurance, PayLinePayer.Employer, 5000, 298.04m),
                    Tax(CanadaIncomeTax.TaxCodes.EmploymentInsuranceQuebec, "CA-QC", CanadaIncomeTax.Categories.InsurableEarnings, PayLinePayer.Employee, 5000, 65.00m, remittedTo: "CA"),
                    Tax(CanadaIncomeTax.TaxCodes.EmploymentInsuranceQuebec + "-ER", "CA-QC", CanadaIncomeTax.Categories.InsurableEarnings, PayLinePayer.Employer, 5000, 91.00m, remittedTo: "CA"),
                    Tax(CanadaIncomeTax.TaxCodes.Qpip, "CA-QC", CanadaIncomeTax.Categories.InsurableEarnings, PayLinePayer.Employee, 5000, 21.50m),
                    Tax(CanadaIncomeTax.TaxCodes.Qpip + "-ER", "CA-QC", CanadaIncomeTax.Categories.InsurableEarnings, PayLinePayer.Employer, 5000, 30.10m),
                    Tax(CanadaIncomeTax.TaxCodes.HealthServicesFund, "CA-QC", IncomeTaxCategories.IncomeTax, PayLinePayer.Employer, 5000, 82.50m),
                ]
                :
                [
                    new PayStatementLine { Type = PayLineType.Earning, PayCodeId = regular.ItemId, Amount = 5000 },
                    Tax(CanadaIncomeTax.TaxCodes.FederalIncomeTax, "CA", IncomeTaxCategories.IncomeTax, PayLinePayer.Employee, 5000, 600),
                    Tax("CA-ON-PIT", "CA-ON", IncomeTaxCategories.IncomeTax, PayLinePayer.Employee, 5000, 300, remittedTo: "CA"),
                    Tax(CanadaIncomeTax.TaxCodes.Cpp, "CA", IncomeTaxCategories.SocialInsurance, PayLinePayer.Employee, 5000, 290.49m),
                    Tax(CanadaIncomeTax.TaxCodes.Cpp + "-ER", "CA", IncomeTaxCategories.SocialInsurance, PayLinePayer.Employer, 5000, 290.49m),
                    Tax(CanadaIncomeTax.TaxCodes.EmploymentInsurance, "CA", CanadaIncomeTax.Categories.InsurableEarnings, PayLinePayer.Employee, 5000, 81.50m),
                    Tax(CanadaIncomeTax.TaxCodes.EmploymentInsurance + "-ER", "CA", CanadaIncomeTax.Categories.InsurableEarnings, PayLinePayer.Employer, 5000, 114.10m),
                ];

            await services.GetRequiredService<PayStatementStore>().CreateAsync(new PayStatement
            {
                ItemId = "castatement000000000000001",
                RunId = setup.RunId,
                Number = "1",
                EmployerId = setup.EmployerId,
                StaffMemberId = setup.StaffMemberId,
                ProfileId = profile.ItemId,
                PeriodStart = new DateOnly(2026, 11, 1),
                PeriodEnd = new DateOnly(2026, 11, 14),
                PayDate = new DateOnly(2026, 11, 20),
                Status = PayStatementStatus.Paid,
                Currency = "CAD",
                Gross = 5000,
                Snapshot = new PayStatementSnapshot { EmployeeName = "Lee Park" },
                Lines = lines,
            });

            return employer;
        });

        return (setup, employer);
    }

    private static Task<CalendarTestHarness> CreateHarnessAsync()
        => PayRunTests.CreateHarnessAsync(services =>
        {
            services.AddPayrollFilingsCore();
            services.AddPayrollFilingForm<FormT4>();
            services.AddPayrollFilingForm<FormRl1>();
        });

    private sealed class NamedProvider : IIncomeTaxProvider
    {
        public NamedProvider(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public LocalizationSource DisplayName { get; } = LocalizationSource.Create<NamedProvider>("Named");

        public Task<IncomeTaxResult> CalculateAsync(IncomeTaxRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new IncomeTaxResult());
    }

    private sealed class FixedProvider : IIncomeTaxProvider
    {
        private readonly IncomeTaxLine[] _lines;

        public FixedProvider(params IncomeTaxLine[] lines)
        {
            _lines = lines;
        }

        public string Name => "Fixed";

        public LocalizationSource DisplayName { get; } = LocalizationSource.Create<FixedProvider>("Fixed");

        public Task<IncomeTaxResult> CalculateAsync(IncomeTaxRequest request, CancellationToken cancellationToken = default)
        {
            var result = new IncomeTaxResult();

            foreach (var line in _lines)
            {
                result.Lines.Add(line);
            }

            return Task.FromResult(result);
        }
    }
}
