using System.Security.Claims;
using CrestApps.Core;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Core.UnitedStates;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// Liabilities, deposits and filings: the deposit periods, Forms 941, 940 and W-2/W-3 worked by hand from two
/// statements, the liabilities with a deposit, filing and amending, year end and the closed year, the SSA file and who
/// may see the pages. Two statements of Lee, 2026: 20 November (5000 of regular pay, 500 to a 401(k) taken before
/// income tax) and 18 December (3000).
/// </summary>
public sealed class PayrollFilingsTests
{
    [Theory]
    [InlineData("Monthly", "2026-11-20", "2026-11-01", "2026-11-30", "2026-12-15")]
    [InlineData("SemiWeekly", "2026-11-20", "2026-11-18", "2026-11-20", "2026-11-25")]
    [InlineData("SemiWeekly", "2026-11-24", "2026-11-21", "2026-11-24", "2026-11-27")]
    [InlineData("Quarterly", "2026-11-20", "2026-10-01", "2026-12-31", "2027-01-31")]
    [InlineData("Annual", "2026-11-20", "2026-01-01", "2026-12-31", "2027-01-31")]
    public void DepositPeriod_FollowsTheSchedule(string schedule, string payDate, string start, string end, string due)
    {
        var (periodStart, periodEnd, dueDate) = TaxLiabilityService.Period(schedule, DateOnly.Parse(payDate, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal((DateOnly.Parse(start, System.Globalization.CultureInfo.InvariantCulture), DateOnly.Parse(end, System.Globalization.CultureInfo.InvariantCulture), DateOnly.Parse(due, System.Globalization.CultureInfo.InvariantCulture)), (periodStart, periodEnd, dueDate));
    }

    [Fact]
    public async Task Forms941940AndW2_AreWorkedOutFromTheStatements_AndTheLiabilitiesFromTheDeposits()
    {
        await using var harness = await CreateHarnessAsync();
        var (setup, employer) = await SeedAsync(harness);
        var preparer = CalendarTestHarness.User("filer", PayrollPermissions.ManagePayrollFilings.Name);

        var (q4, futa, w2, liabilities) = await harness.RunAsync(async services =>
        {
            var deposits = services.GetRequiredService<ITaxDepositManager>();
            var deposit = await deposits.NewAsync();

            deposit.EmployerId = setup.EmployerId;
            deposit.Jurisdiction = "US";
            deposit.PeriodStart = new DateOnly(2026, 11, 1);
            deposit.PeriodEnd = new DateOnly(2026, 11, 30);
            deposit.Amount = 1000;
            deposit.PaidOn = new DateOnly(2026, 12, 10);
            await deposits.CreateAsync(deposit);

            var service = services.GetRequiredService<IPayrollFilingService>();

            return (
                (await service.PrepareAsync(employer, Form941.FormName, 2026, 4, preparer)).Filing,
                (await service.PrepareAsync(employer, Form940.FormName, 2026, 0, preparer)).Filing,
                (await service.PrepareAsync(employer, FormW2.FormName, 2026, 0, preparer)).Filing,
                await services.GetRequiredService<ITaxLiabilityService>().ListAsync(setup.EmployerId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
        });

        // 941: wages 4500 + 3000; income tax 400 + 200; 8000 of Social Security wages at 12.4% = 992 and of Medicare at
        // 2.9% = 232; the 1224 withheld and matched is exactly that, so no fractions of cents; 1000 deposited.
        Assert.Equal(1m, q4.Boxes["1"]);
        Assert.Equal((7500m, 600m, 8000m, 992m, 8000m, 232m), (q4.Boxes["2"], q4.Boxes["3"], q4.Boxes["5a"], q4.Boxes["5a.tax"], q4.Boxes["5c"], q4.Boxes["5c.tax"]));
        Assert.Equal((1824m, 0m, 1824m, 1000m, 824m, 0m), (q4.Boxes["6"], q4.Boxes["7"], q4.Boxes["10"], q4.Boxes["13"], q4.Boxes["14"], q4.Boxes["15"]));

        // 940: 8000 paid, 1000 over the 7000 wage base, 7000 taxable at 0.6% = 42.
        Assert.Equal((8000m, 0m, 1000m, 7000m, 42m), (futa.Boxes["3"], futa.Boxes["4"], futa.Boxes["5"], futa.Boxes["7"], futa.Boxes["8"]));

        // W-2: box 1 without the 401(k), which is box 12 code D; Medicare tax with no additional tax.
        var lee = Assert.Single(w2.People);
        Assert.Equal((7500m, 600m, 8000m, 496m, 8000m, 116m, 500m), (lee.Boxes["1"], lee.Boxes["2"], lee.Boxes["3"], lee.Boxes["4"], lee.Boxes["5"], lee.Boxes["6"], lee.Boxes["12D"]));
        Assert.Equal("6789", lee.TaxIdHint);
        Assert.Equal((7500m, 1m), (w2.Boxes["1"], w2.Boxes["c"]));

        // Liabilities: November 400 + 620 + 145 = 1165 with 1000 deposited; December 200 + 372 + 87 = 659; FUTA for the
        // quarter 30 + 12 = 42, due 31 January.
        var november = liabilities.Single(period => period.Group == TaxLiabilityGroups.Withholding && period.PeriodStart.Month == 11);
        var december = liabilities.Single(period => period.Group == TaxLiabilityGroups.Withholding && period.PeriodStart.Month == 12);
        var unemployment = liabilities.Single(period => period.Group == TaxLiabilityGroups.Unemployment);

        Assert.Equal((1165m, 1000m, 165m, new DateOnly(2026, 12, 15)), (november.Owed, november.Deposited, november.Balance, november.DueDate));
        Assert.Equal(659m, december.Owed);
        Assert.Equal((42m, new DateOnly(2027, 1, 31)), (unemployment.Owed, unemployment.DueDate));
    }

    [Fact]
    public async Task AFiledForm_IsNeverPreparedAgain_AnAmendmentNamesIt_AndFilingTheAmendmentMarksTheFirstAmended()
    {
        await using var harness = await CreateHarnessAsync();
        var (_, employer) = await SeedAsync(harness);
        var filer = CalendarTestHarness.User("filer", PayrollPermissions.ManagePayrollFilings.Name);

        var (again, amendment, first) = await harness.RunAsync(async services =>
        {
            var service = services.GetRequiredService<IPayrollFilingService>();
            var draft = (await service.PrepareAsync(employer, Form941.FormName, 2026, 4, filer)).Filing;

            Assert.True((await service.MarkFiledAsync(draft, new DateOnly(2027, 1, 20), "CONF-1", filer)).Succeeded);

            var prepared = await service.PrepareAsync(employer, Form941.FormName, 2026, 4, filer);
            var amended = await service.AmendAsync(draft, filer);

            Assert.True((await service.MarkFiledAsync(amended.Filing, new DateOnly(2027, 2, 1), "CONF-2", filer)).Succeeded);

            return (prepared, amended.Filing, await services.GetRequiredService<IPayrollFilingManager>().FindByIdAsync(draft.ItemId));
        });

        Assert.False(again.Succeeded);
        Assert.Equal(first.ItemId, amendment.AmendsId);
        Assert.Equal(PayrollFilingStatus.Amended, first.Status);
        Assert.Equal(("CONF-1", PayrollFilingStatus.Filed), (first.Reference, amendment.Status));
    }

    [Fact]
    public async Task YearEnd_WaitsForEveryRun_ThenClosesTheYear_SoNoRunOrVoidChangesIt()
    {
        await using var harness = await CreateHarnessAsync();
        var (setup, employer) = await SeedAsync(harness);
        var filer = CalendarTestHarness.User("filer", PayrollPermissions.ManagePayrollFilings.Name);
        var preparer = CalendarTestHarness.User("preparer", PayrollPermissions.PreparePayRuns.Name);

        var (blocked, closed, calculate, voided, w2) = await harness.RunAsync(async services =>
        {
            var service = services.GetRequiredService<IPayrollFilingService>();
            var runs = services.GetRequiredService<IPayRunService>();
            var openRun = await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId);
            var first = await service.CloseYearAsync(employer, 2026, filer);

            await runs.CancelAsync(openRun, "Not needed");

            var second = await service.CloseYearAsync(employer, 2026, filer);
            var run = await services.GetRequiredService<IPayRunManager>().NewAsync();

            run.EmployerId = setup.EmployerId;
            run.PeriodStart = new DateOnly(2026, 12, 13);
            run.PeriodEnd = new DateOnly(2026, 12, 26);
            run.PayDate = new DateOnly(2026, 12, 31);

            var opened = await runs.OpenAsync(run, preparer);
            var statement = (await services.GetRequiredService<PayStatementStore>().ListAsync(index => index.EmployerId == setup.EmployerId))[0];
            var voiding = await runs.VoidStatementAsync(statement, CalendarTestHarness.User("voider", PayrollPermissions.VoidPayStatements.Name), "Wrong");

            return (first, second, opened, voiding, await services.GetRequiredService<IPayrollFilingManager>().FindCurrentAsync(setup.EmployerId, FormW2.FormName, 2026, 0));
        });

        Assert.False(blocked.Succeeded);
        Assert.Contains(blocked.Errors, error => error.Contains(setup.RunNumber, StringComparison.Ordinal));
        Assert.True(closed.Succeeded, string.Join(" ", closed.Errors));
        Assert.False(calculate.Succeeded);
        Assert.Contains(calculate.Errors, error => error.Contains("closed", StringComparison.Ordinal));
        Assert.False(voided.Succeeded);
        Assert.Equal(PayrollFilingStatus.Draft, w2.Status);
        Assert.Contains(2026, (await harness.RunAsync(async services => await services.GetRequiredService<IEmployerManager>().FindByIdAsync(setup.EmployerId), commit: false)).ClosedYears);
    }

    [Fact]
    public void Efw2_WritesRecordsOf512Characters_WithTheTotalsAndCountsOfThePeople()
    {
        var address = new Efw2Address(null, "1 Main St", "Springfield", "IL", "62701", null);
        var people = new[]
        {
            new Efw2Person("123-45-6789", "Lee", null, "Park", address, new Dictionary<string, decimal> { ["1"] = 7500m, ["2"] = 600m, ["3"] = 8000m, ["12D"] = 500m }),
            new Efw2Person("987654321", "Sam", "J", "Rivera", address, new Dictionary<string, decimal> { ["1"] = 1234.56m, ["2"] = 100m, ["3"] = 1234.56m }),
        };
        var file = UnitedStatesEfw2.Write(new Efw2Submitter("12-3456789", "ABCD1234", "Contoso Holdings", address, "Pat Lee", "555-0100", "pat@contoso.test"), [new Efw2Employer("123456789", "Contoso Holdings", address, people)], 2026);
        var records = file.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(["RA", "RE", "RW", "RW", "RT", "RF"], records.Select(record => record[..2]));
        Assert.All(records, record => Assert.Equal(512, record.Length));

        // RA: the EIN and user id; RE: the year; RW: the SSN and box 1 in cents at 188; RT: the count and the totals.
        Assert.Equal(("123456789", "ABCD1234"), (records[0][2..11], records[0][11..19]));
        Assert.Equal("2026", records[1][2..6]);
        Assert.Equal(("123456789", "LEE", "PARK"), (records[2][2..11], records[2][11..26].TrimEnd(), records[2][41..61].TrimEnd()));
        Assert.Equal("00000750000", records[2][187..198]);
        Assert.Equal("00000123456", records[3][187..198]);
        Assert.Equal("00000050000", records[2][286..297]);
        Assert.Equal("0000002", records[4][2..9]);
        Assert.Equal("000000000873456", records[4][9..24]);
        Assert.Equal("000000002", records[5][7..16]);

        // The preparer code (self-prepared) and the retirement plan indicator of who has a box 12 plan code.
        Assert.Equal('L', records[0][499]);
        Assert.Equal(('1', '0'), (records[2][489], records[3][489]));
        Assert.Equal("12W 1200.00", UnitedStatesEfw2.Unwritten(new Dictionary<string, decimal> { ["1"] = 10m, ["12D"] = 5m, ["12W"] = 1200m }));
    }

    [Fact]
    public async Task WageRecords_ReadOneFilingAYear_AndTheFileRefusesAMissingNumberAndABoxItCantHold()
    {
        await using var harness = await CreateHarnessAsync();
        var (setup, _) = await SeedAsync(harness);

        // The filed W-2s and an amendment being prepared: Lee is in both.
        await harness.RunAsync(async services =>
        {
            var filings = services.GetRequiredService<IPayrollFilingManager>();
            var profileId = (await services.GetRequiredService<IPayProfileManager>().ListByEmployersAsync([setup.EmployerId])).Single().ItemId;

            PayrollFilingPerson Lee(decimal wages)
                => new() { StaffMemberId = setup.StaffMemberId, ProfileId = profileId, Name = "Lee Park", Boxes = new Dictionary<string, decimal>(StringComparer.Ordinal) { ["1"] = wages } };

            var filed = new PayrollFiling { ItemId = "w2filed000000000000000001", EmployerId = setup.EmployerId, Form = FormW2.FormName, Year = 2026, Status = PayrollFilingStatus.Filed, FiledOn = new DateOnly(2027, 1, 20), People = [Lee(7500)] };
            var amendment = new PayrollFiling { ItemId = "w2amend000000000000000001", EmployerId = setup.EmployerId, Form = FormW2.FormName, Year = 2026, Status = PayrollFilingStatus.Draft, AmendsId = filed.ItemId, People = [Lee(7600)] };

            await filings.CreateAsync(filed);
            await filings.CreateAsync(amendment);
        });

        var exporter = CalendarTestHarness.User("exporter", PayrollPermissions.ExportPayrollData.Name);
        var stranger = CalendarTestHarness.User("stranger", PayrollPermissionTemplates.For(PayrollPermissions.ExportPayrollData, "fabrikam").Name);

        async Task<List<Dictionary<string, object>>> ReadAsync(string dataSet, System.Security.Claims.ClaimsPrincipal user, bool draft = false)
            => await harness.RunAsync(async services =>
            {
                var reader = new CrestApps.OrchardCore.Payroll.Services.PayrollReportReader(services.GetRequiredService<IPayrollScopeService>(), services.GetRequiredService<PayStatementStore>(), services.GetRequiredService<PayRunStore>(), services.GetRequiredService<EmployerStore>(), new Microsoft.AspNetCore.Http.HttpContextAccessor());
                var source = new CrestApps.OrchardCore.Payroll.DataSources.PayrollDataSource(
                [
                    ActivatorUtilities.CreateInstance<CrestApps.OrchardCore.Payroll.DataSources.WageRecordsDataSet>(services, reader),
                    ActivatorUtilities.CreateInstance<CrestApps.OrchardCore.Payroll.DataSources.TaxLiabilitiesDataSet>(services, reader),
                ]);
                var table = await CrestApps.OrchardCore.Tests.Modules.Reports.Designer.ReportDesignerTestServices.ReadAsync(source, new global::OrchardCore.DataSources.DataSourceQuery
                {
                    DataSet = dataSet,
                    Fields = new HashSet<string>(StringComparer.Ordinal),
                    MaxRows = 100,
                    Parameters = draft ? new Dictionary<string, object> { [CrestApps.OrchardCore.Payroll.DataSources.WageRecordsDataSet.DraftParameter] = "true" } : null,
                    Context = new global::OrchardCore.DataSources.DataSourceContext { User = user },
                }, TestContext.Current.CancellationToken);

                return table.Rows.Select(row => table.Fields.Select((field, index) => (field.Name, Value: row[index])).ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal)).ToList();
            }, commit: false);

        // Read once: the filed one, or the amendment when asked.
        Assert.Equal(7500m, Convert.ToDecimal(Assert.Single(await ReadAsync("WageRecords", exporter))["Box1"], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(7600m, Convert.ToDecimal(Assert.Single(await ReadAsync("WageRecords", exporter, draft: true))["Box1"], System.Globalization.CultureInfo.InvariantCulture));

        // A clerk of another employer reads neither the wage records nor the liabilities.
        Assert.Empty(await ReadAsync("WageRecords", stranger));
        Assert.Empty(await ReadAsync("TaxLiabilities", stranger));
        Assert.NotEmpty(await ReadAsync("TaxLiabilities", exporter));

        // The exporter may not see tax ids: the file is refused, never written with zeros.
        var row = Assert.Single(await ReadAsync("WageRecords", exporter));
        var protection = await harness.RunAsync(services => Task.FromResult(services.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>()), commit: false);
        var format = new CrestApps.OrchardCore.Payroll.DataSources.Efw2DataFileFormat(protection);

        async Task WriteAsync(Dictionary<string, object> values)
        {
            var fields = values.Keys.Select(name => new global::OrchardCore.DataSources.DataField(name, name, global::OrchardCore.DataSources.DataFieldType.Text)).ToList();

            static async IAsyncEnumerable<global::OrchardCore.DataSources.DataBatch> Batches(List<global::OrchardCore.DataSources.DataField> fields, object[] row)
            {
                await Task.Yield();

                yield return new global::OrchardCore.DataSources.DataBatch(fields, [row]);
            }

            await using var stream = new MemoryStream();

            await format.WriteAsync(stream, fields, Batches(fields, [.. values.Values]), null, TestContext.Current.CancellationToken);
        }

        Assert.Null(row[CrestApps.OrchardCore.Payroll.DataSources.WageRecordsDataSet.ProtectedSsnField]);
        Assert.False(row.ContainsKey("Ssn"));
        Assert.Contains("full social security number", (await Assert.ThrowsAsync<InvalidOperationException>(() => WriteAsync(row))).Message, StringComparison.Ordinal);

        // A number typed into the field is no number: only one the data set encrypted is read.
        row[CrestApps.OrchardCore.Payroll.DataSources.WageRecordsDataSet.ProtectedSsnField] = "123-45-6789";
        Assert.Contains("full social security number", (await Assert.ThrowsAsync<InvalidOperationException>(() => WriteAsync(row))).Message, StringComparison.Ordinal);

        row[CrestApps.OrchardCore.Payroll.DataSources.WageRecordsDataSet.ProtectedSsnField] = CrestApps.OrchardCore.Payroll.DataSources.FileTaxIds.Protect(protection, "123-45-6789");
        row[CrestApps.OrchardCore.Payroll.DataSources.WageRecordsDataSet.OtherBoxesField] = "12W 1200.00";

        Assert.Contains("12W 1200.00", (await Assert.ThrowsAsync<InvalidOperationException>(() => WriteAsync(row))).Message, StringComparison.Ordinal);

        row[CrestApps.OrchardCore.Payroll.DataSources.WageRecordsDataSet.OtherBoxesField] = null;
        await WriteAsync(row);

        // Who may see tax ids reads the number encrypted: a preview or a CSV shows ciphertext, EFW2 reads it back.
        var revealer = CalendarTestHarness.User("revealer", PayrollPermissions.ExportPayrollData.Name, PayrollPermissions.ViewTaxIds.Name);
        var encrypted = Convert.ToString(Assert.Single(await ReadAsync("WageRecords", revealer))[CrestApps.OrchardCore.Payroll.DataSources.WageRecordsDataSet.ProtectedSsnField], System.Globalization.CultureInfo.InvariantCulture);

        Assert.DoesNotContain("6789", encrypted, StringComparison.Ordinal);
        Assert.EndsWith("6789", CrestApps.OrchardCore.Payroll.DataSources.FileTaxIds.Unprotect(protection, encrypted), StringComparison.Ordinal);
    }

    [Fact]
    public async Task BackupWithholding_IsItsOwnLiability_OnForm945_AndStaysOutOfForm941()
    {
        await using var harness = await PayRunTests.CreateHarnessAsync(services =>
        {
            services.AddPayrollFilingsCore();
            services.AddPayrollFilingForm<Form941>();
            services.AddPayrollFilingForm<Form945>();
        });
        var setup = await PayRunTests.SetUpAsync(harness);
        var filer = CalendarTestHarness.User("filer", PayrollPermissions.ManagePayrollFilings.Name);

        var (liabilities, q4, annual) = await harness.RunAsync(async services =>
        {
            var profiles = services.GetRequiredService<IPayProfileManager>();
            var profile = (await profiles.ListByEmployersAsync([setup.EmployerId])).Single();

            profile.WorkerType = PayWorkerType.Contractor;
            await profiles.UpdateAsync(profile);

            // A contractor paid 3000 with 24% backup withholding, and a 700 deposit of it.
            await services.GetRequiredService<PayStatementStore>().CreateAsync(new PayStatement
            {
                ItemId = "statement00000000000000945",
                RunId = setup.RunId,
                Number = "945",
                EmployerId = setup.EmployerId,
                StaffMemberId = setup.StaffMemberId,
                ProfileId = profile.ItemId,
                PeriodStart = new DateOnly(2026, 11, 1),
                PeriodEnd = new DateOnly(2026, 11, 14),
                PayDate = new DateOnly(2026, 11, 20),
                Status = PayStatementStatus.Paid,
                Currency = "USD",
                Gross = 3000,
                Snapshot = new PayStatementSnapshot { EmployeeName = "Lee Park" },
                Lines =
                [
                    new PayStatementLine { Type = PayLineType.Earning, PayCodeId = setup.BonusCodeId, Amount = 3000 },
                    new PayStatementLine { Type = PayLineType.Tax, TaxCode = Form1099Nec.BackupWithholdingCode, TaxCategory = IncomeTaxCategories.ContractorWithholding, Jurisdiction = "US", Payer = PayLinePayer.Employee, TaxableWages = 3000, Amount = 720 },
                ],
            });

            var deposits = services.GetRequiredService<ITaxDepositManager>();
            var deposit = await deposits.NewAsync();

            deposit.EmployerId = setup.EmployerId;
            deposit.Jurisdiction = "US";
            deposit.Group = TaxLiabilityGroups.NonPayrollWithholding;
            deposit.PeriodStart = new DateOnly(2026, 11, 1);
            deposit.PeriodEnd = new DateOnly(2026, 11, 30);
            deposit.Amount = 700;
            deposit.PaidOn = new DateOnly(2026, 12, 10);
            await deposits.CreateAsync(deposit);

            var employer = await services.GetRequiredService<IEmployerManager>().FindByIdAsync(setup.EmployerId);
            var service = services.GetRequiredService<IPayrollFilingService>();

            return (
                await services.GetRequiredService<ITaxLiabilityService>().ListAsync(setup.EmployerId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)),
                (await service.PrepareAsync(employer, Form941.FormName, 2026, 4, filer)).Filing,
                (await service.PrepareAsync(employer, Form945.FormName, 2026, 0, filer)).Filing);
        });

        var period = Assert.Single(liabilities);

        Assert.Equal((TaxLiabilityGroups.NonPayrollWithholding, 720m, 700m), (period.Group, period.EmployeeTaxes, period.Deposited));
        Assert.Equal((0m, 0m, 0m), (q4.Boxes["3"], q4.Boxes["13"], q4.Boxes["15"]));
        Assert.Equal((720m, 720m, 700m, 20m, 0m), (annual.Boxes["2"], annual.Boxes["3"], annual.Boxes["4"], annual.Boxes["5"], annual.Boxes["6"]));
    }

    [Fact]
    public async Task FilingsDeployment_WritesPeoplesWageStatementsOnlyWithIncludeSensitiveData_ForWhoMaySeeTaxIds()
    {
        await using var harness = await CreateHarnessAsync();
        var (setup, employer) = await SeedAsync(harness);
        var filer = CalendarTestHarness.User("filer", PayrollPermissions.ManagePayrollFilings.Name);

        await harness.RunAsync(async services => await services.GetRequiredService<IPayrollFilingService>().PrepareAsync(employer, FormW2.FormName, 2026, 0, filer));

        async Task<System.Text.Json.Nodes.JsonObject> ExportAsync(System.Security.Claims.ClaimsPrincipal user, bool sensitive)
            => await harness.RunAsync(async services =>
            {
                var accessor = new Microsoft.AspNetCore.Http.HttpContextAccessor { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = user } };
                var source = ActivatorUtilities.CreateInstance<CrestApps.OrchardCore.Payroll.Deployments.PayrollFilingsDeploymentSource>(services, accessor, Moq.Mock.Of<global::OrchardCore.DisplayManagement.Notify.INotifier>(), new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughHtmlLocalizer<CrestApps.OrchardCore.Payroll.Deployments.PayrollFilingsDeploymentSource>());
                var result = new global::OrchardCore.Deployment.DeploymentPlanResult(null, new global::OrchardCore.Recipes.Models.RecipeDescriptor());

                await source.ProcessDeploymentStepAsync(new CrestApps.OrchardCore.Payroll.Deployments.PayrollFilingsDeploymentStep { IncludeSensitiveData = sensitive }, result);

                return result.Steps.OfType<System.Text.Json.Nodes.JsonObject>().Single(step => step["name"]!.GetValue<string>() == PayrollConstants.FilingsRecipeStepName);
            }, commit: false);

        var exporter = CalendarTestHarness.User("exporter", PayrollPermissions.ExportPayrollData.Name);
        var revealer = CalendarTestHarness.User("revealer", PayrollPermissions.ExportPayrollData.Name, PayrollPermissions.ViewTaxIds.Name);

        System.Text.Json.Nodes.JsonObject W2(System.Text.Json.Nodes.JsonObject step)
            => step["Filings"]!.AsArray().Select(node => node!.AsObject()).Single(node => node["Form"]!.GetValue<string>() == FormW2.FormName);

        Assert.False(W2(await ExportAsync(revealer, sensitive: false)).ContainsKey("People"));
        Assert.False(W2(await ExportAsync(exporter, sensitive: true)).ContainsKey("People"));
        Assert.Single(W2(await ExportAsync(revealer, sensitive: true))["People"]!.AsArray());
    }

    [Fact]
    public async Task Pages_AreForTheEmployersAUserFilesFor()
    {
        await using var harness = await CreateHarnessAsync();
        var (setup, employer) = await SeedAsync(harness);
        var filer = CalendarTestHarness.User("filer", PayrollPermissions.ManagePayrollFilings.Name);
        var otherClerk = CalendarTestHarness.User("other", PayrollPermissionTemplates.For(PayrollPermissions.ManagePayrollFilings, "fabrikam").Name);
        var clerk = CalendarTestHarness.User("clerk", PayrollPermissionTemplates.For(PayrollPermissions.ManagePayrollFilings, setup.EmployerId).Name);

        var filingId = await harness.RunAsync(async services => (await services.GetRequiredService<IPayrollFilingService>().PrepareAsync(employer, Form941.FormName, 2026, 4, filer)).Filing.ItemId);

        var results = await harness.RunAsync(async services => (
            await Controller(services, clerk).Details(filingId),
            await Controller(services, otherClerk).Details(filingId),
            await Controller(services, otherClerk).Liabilities(null, 2026),
            await Controller(services, CalendarTestHarness.User("nobody")).Index(null)), commit: false);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(results.Item1);
        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(results.Item2);
        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(results.Item3);
        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(results.Item4);
    }

    private static CrestApps.OrchardCore.Payroll.Controllers.PayrollFilingsController Controller(IServiceProvider services, ClaimsPrincipal user)
    {
        var controller = new CrestApps.OrchardCore.Payroll.Controllers.PayrollFilingsController(
            services.GetRequiredService<ITaxLiabilityService>(),
            services.GetRequiredService<ITaxDepositManager>(),
            services.GetRequiredService<IPayrollFilingManager>(),
            services.GetRequiredService<IPayrollFilingService>(),
            services.GetRequiredService<IEmployerManager>(),
            services.GetRequiredService<IPayrollScopeService>(),
            ActivatorUtilities.CreateInstance<PayrollTaxFormDocuments>(services, new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<PayrollTaxFormDocuments>()),
            services.GetRequiredService<global::OrchardCore.Modules.ILocalClock>(),
            Moq.Mock.Of<global::OrchardCore.DisplayManagement.Notify.INotifier>(),
            new CrestApps.OrchardCore.Tests.Doubles.SourceTextLocalizerFactory(),
            new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughHtmlLocalizer<CrestApps.OrchardCore.Payroll.Controllers.PayrollFilingsController>(),
            new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<CrestApps.OrchardCore.Payroll.Controllers.PayrollFilingsController>());

        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = user,
                RequestServices = new ServiceCollection()
                    .AddSingleton(Moq.Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory>())
                    .AddSingleton<Microsoft.AspNetCore.Mvc.ModelBinding.IModelMetadataProvider, Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider>()
                    .BuildServiceProvider(),
            },
        };
        controller.Url = Moq.Mock.Of<Microsoft.AspNetCore.Mvc.IUrlHelper>();

        return controller;
    }

    // Lee's two statements of 2026 at Contoso, registered in US.
    private static async Task<(PayRunTests.Setup Setup, Employer Employer)> SeedAsync(CalendarTestHarness harness)
    {
        var setup = await PayRunTests.SetUpAsync(harness);

        var employer = await harness.RunAsync(async services =>
        {
            var codes = services.GetRequiredService<IPayCodeManager>();
            var regular = await codes.FindByCodeAsync("REG");
            var retirement = await codes.NewAsync();

            retirement.Code = "401K";
            retirement.Name = "401(k)";
            retirement.Class = PayCodeClass.Deduction;
            retirement.Kind = PayCodeKinds.Contribution;

            var settings = PayrollPayCodeSettingsExtensions.DefaultsFor(retirement);

            settings.TaxTreatment[IncomeTaxCategories.IncomeTax] = PayCodeTaxTreatment.Exempt;
            settings.FormBoxes = ["W2:12D"];
            retirement.Put(settings);
            await codes.CreateAsync(retirement);

            var profile = (await services.GetRequiredService<IPayProfileManager>().ListByEmployersAsync([setup.EmployerId])).Single();
            var store = services.GetRequiredService<PayStatementStore>();

            static PayStatementLine Tax(string code, string category, PayLinePayer payer, decimal taxable, decimal amount)
                => new() { Type = PayLineType.Tax, TaxCode = code, TaxCategory = category, Jurisdiction = "US", Payer = payer, TaxableWages = taxable, Amount = amount };

            await store.CreateAsync(new PayStatement
            {
                ItemId = "statement00000000000000001",
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
                    new PayStatementLine { Type = PayLineType.Earning, PayCodeId = regular.ItemId, Amount = 5000 },
                    new PayStatementLine { Type = PayLineType.Deduction, PayCodeId = retirement.ItemId, Amount = 500 },
                    Tax(UnitedStatesTaxCodes.IncomeTax, IncomeTaxCategories.IncomeTax, PayLinePayer.Employee, 4500, 400),
                    Tax(UnitedStatesTaxCodes.SocialSecurity, IncomeTaxCategories.SocialInsurance, PayLinePayer.Employee, 5000, 310),
                    Tax(UnitedStatesTaxCodes.SocialSecurityEmployer, IncomeTaxCategories.SocialInsurance, PayLinePayer.Employer, 5000, 310),
                    Tax(UnitedStatesTaxCodes.Medicare, IncomeTaxCategories.SocialInsurance, PayLinePayer.Employee, 5000, 72.50m),
                    Tax(UnitedStatesTaxCodes.MedicareEmployer, IncomeTaxCategories.SocialInsurance, PayLinePayer.Employer, 5000, 72.50m),
                    Tax(UnitedStatesTaxCodes.Unemployment, IncomeTaxCategories.Unemployment, PayLinePayer.Employer, 5000, 30),
                ],
            });

            await store.CreateAsync(new PayStatement
            {
                ItemId = "statement00000000000000002",
                RunId = setup.RunId,
                Number = "2",
                EmployerId = setup.EmployerId,
                StaffMemberId = setup.StaffMemberId,
                ProfileId = profile.ItemId,
                PeriodStart = new DateOnly(2026, 11, 29),
                PeriodEnd = new DateOnly(2026, 12, 12),
                PayDate = new DateOnly(2026, 12, 18),
                Status = PayStatementStatus.Paid,
                Currency = "USD",
                Gross = 3000,
                Snapshot = new PayStatementSnapshot { EmployeeName = "Lee Park" },
                Lines =
                [
                    new PayStatementLine { Type = PayLineType.Earning, PayCodeId = regular.ItemId, Amount = 3000 },
                    Tax(UnitedStatesTaxCodes.IncomeTax, IncomeTaxCategories.IncomeTax, PayLinePayer.Employee, 3000, 200),
                    Tax(UnitedStatesTaxCodes.SocialSecurity, IncomeTaxCategories.SocialInsurance, PayLinePayer.Employee, 3000, 186),
                    Tax(UnitedStatesTaxCodes.SocialSecurityEmployer, IncomeTaxCategories.SocialInsurance, PayLinePayer.Employer, 3000, 186),
                    Tax(UnitedStatesTaxCodes.Medicare, IncomeTaxCategories.SocialInsurance, PayLinePayer.Employee, 3000, 43.50m),
                    Tax(UnitedStatesTaxCodes.MedicareEmployer, IncomeTaxCategories.SocialInsurance, PayLinePayer.Employer, 3000, 43.50m),
                    Tax(UnitedStatesTaxCodes.Unemployment, IncomeTaxCategories.Unemployment, PayLinePayer.Employer, 2000, 12),
                ],
            });

            return await services.GetRequiredService<IEmployerManager>().FindByIdAsync(setup.EmployerId);
        });

        return (setup, employer);
    }

    private static Task<CalendarTestHarness> CreateHarnessAsync()
        => PayRunTests.CreateHarnessAsync(services =>
        {
            services.AddPayrollFilingsCore();
            services.AddPayrollFilingForm<Form941>();
            services.AddPayrollFilingForm<Form940>();
            services.AddPayrollFilingForm<FormW2>();
        });
}
