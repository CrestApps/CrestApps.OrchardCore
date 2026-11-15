using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Disbursements.Models;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Payroll.AuditTrail;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.DataSources;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Recipes;
using CrestApps.OrchardCore.Payroll.Reports;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.Workflows;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Disbursements;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Webhooks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;
using OrchardCore.DataSources;
using OrchardCore.Modules;
using OrchardCore.Notifications;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// Payroll's Orchard integration: the data sets and reports read only the employers a user may see, the workflow events,
/// webhooks and audit events follow each step, the pay dates on the calendar, a person's pay records in an export, pay
/// history imported read-only, and the notices of a returned deposit and of a changed bank account.
/// </summary>
public sealed class PayrollIntegrationTests
{
    [Fact]
    public async Task DataSets_ReadTheFinalPayOfTheEmployersAUserMaySee_AndNeverABankAccount()
    {
        var (harness, setup, _) = await ProcessedAsync();
        await using var _ = harness;
        var viewer = CalendarTestHarness.User("viewer", PayrollPermissionTemplates.For(PayrollPermissions.ViewPayrollReports, setup.EmployerId).Name);
        var stranger = CalendarTestHarness.User("stranger", PayrollPermissionTemplates.For(PayrollPermissions.ViewPayrollReports, "fabrikam").Name);

        var (statements, earnings, taxes, payments, yearToDate, runs, strangerRows, strangerMay) = await harness.RunAsync(async services =>
        {
            var reader = new PayrollReportReader(services.GetRequiredService<IPayrollScopeService>(), services.GetRequiredService<PayStatementStore>(), services.GetRequiredService<PayRunStore>(), services.GetRequiredService<EmployerStore>(), new HttpContextAccessor());
            var source = new PayrollDataSource(
            [
                new PayStatementsDataSet(reader, new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<PayStatementsDataSet>()),
                new PayEarningsDataSet(reader, new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<PayEarningsDataSet>()),
                new PayTaxesDataSet(reader, new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<PayTaxesDataSet>()),
                new PayStatementPaymentsDataSet(reader, new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<PayStatementPaymentsDataSet>()),
                new YearToDateBalancesDataSet(reader, new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<YearToDateBalancesDataSet>()),
                new PayRunsDataSet(reader, new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<PayRunsDataSet>()),
            ]);

            async Task<List<Dictionary<string, object>>> ReadAsync(string dataSet, ClaimsPrincipal user)
                => Rows(await ReportDesignerTestServices.ReadAsync(source, new DataSourceQuery { DataSet = dataSet, Fields = new HashSet<string>(StringComparer.Ordinal), MaxRows = 100, Context = new DataSourceContext { User = user } }, TestContext.Current.CancellationToken));

            return (
                await ReadAsync("PayStatements", viewer),
                await ReadAsync("PayEarnings", viewer),
                await ReadAsync("PayTaxes", viewer),
                await ReadAsync("PayPayments", viewer),
                await ReadAsync("PayYearToDate", viewer),
                await ReadAsync("PayRuns", viewer),
                await ReadAsync("PayStatements", stranger),
                await new PayStatementsDataSet(reader, new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<PayStatementsDataSet>()).CanReadAsync(new DataSourceContext { User = stranger }, TestContext.Current.CancellationToken));
        }, commit: false);

        var statement = Assert.Single(statements);
        Assert.Equal((1600m, 1500.80m, "Paid"), ((decimal)statement["Gross"], (decimal)statement["Net"], (string)statement["Status"]));
        Assert.Equal(1600m, earnings.Sum(row => (decimal)row["Amount"]));
        Assert.Equal(99.20m, taxes.Sum(row => (decimal)row["Amount"]));
        var payment = Assert.Single(payments);
        Assert.True((bool)payment["Paid"]);
        Assert.DoesNotContain(payments.SelectMany(row => row.Keys), key => key.Contains("Bank", StringComparison.OrdinalIgnoreCase) || key.Contains("Account", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(yearToDate, row => (string)row["Type"] == "Earning" && (decimal)row["Amount"] == 1600m && Convert.ToInt32(row["Year"], System.Globalization.CultureInfo.InvariantCulture) == 2026);
        Assert.Equal("Paid", (string)Assert.Single(runs)["Status"]);
        Assert.Empty(strangerRows);
        Assert.False(strangerMay);
    }

    [Fact]
    public async Task JournalEntries_BalanceForTheEmployersAUserMayExport_AndNoneForOthers()
    {
        var (harness, setup, _) = await ProcessedAsync();
        await using var _ = harness;
        var exporter = CalendarTestHarness.User("exporter", PayrollPermissionTemplates.For(PayrollPermissions.ExportPayrollData, setup.EmployerId).Name);
        var viewer = CalendarTestHarness.User("viewer", PayrollPermissionTemplates.For(PayrollPermissions.ViewPayRuns, setup.EmployerId).Name);
        var site = new global::OrchardCore.Settings.SiteSettings();

        global::OrchardCore.Entities.EntityExtensions.Put(site, new PayrollJournalSettings { NetPayAccount = "2100", WageExpenseAccount = "6000", TaxLiabilityAccount = "2300", EmployerTaxExpenseAccount = "6100", DeductionLiabilityAccount = "2200" });

        var siteService = new Mock<global::OrchardCore.Settings.ISiteService>();

        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site);

        var (entries, viewerRows, viewerMay) = await harness.RunAsync(async services =>
        {
            var reader = new PayrollReportReader(services.GetRequiredService<IPayrollScopeService>(), services.GetRequiredService<PayStatementStore>(), services.GetRequiredService<PayRunStore>(), services.GetRequiredService<EmployerStore>(), new HttpContextAccessor());
            // Staff registers the team services even with Teams off, when the teams' tables don't exist: without the
            // Teams feature's marker the data set never reads teams.
            var teams = new Mock<CrestApps.OrchardCore.Teams.Services.ITeamManager>(MockBehavior.Strict);
            var dataSet = new JournalEntriesDataSet(reader, services.GetRequiredService<IPayCodeManager>(), siteService.Object, new WithService(services, typeof(CrestApps.OrchardCore.Teams.Services.ITeamManager), teams.Object), new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<JournalEntriesDataSet>());
            var source = new PayrollDataSource([dataSet]);

            async Task<List<Dictionary<string, object>>> ReadAsync(ClaimsPrincipal user)
                => Rows(await ReportDesignerTestServices.ReadAsync(source, new DataSourceQuery { DataSet = "JournalEntries", Fields = new HashSet<string>(StringComparer.Ordinal), MaxRows = 1000, Context = new DataSourceContext { User = user } }, TestContext.Current.CancellationToken));

            return (await ReadAsync(exporter), await ReadAsync(viewer), await dataSet.CanReadAsync(new DataSourceContext { User = viewer }, TestContext.Current.CancellationToken));
        }, commit: false);

        Assert.NotEmpty(entries);
        Assert.Equal(entries.Sum(row => (decimal)row["Debit"]), entries.Sum(row => (decimal)row["Credit"]));
        Assert.Equal(1600m, entries.Where(row => (string)row["Account"] == "6000").Sum(row => (decimal)row["Debit"]));

        // What is left on net pay owed is the statement's net pay.
        Assert.Equal(1500.80m, entries.Where(row => (string)row["Account"] == "2100").Sum(row => (decimal)row["Credit"] - (decimal)row["Debit"]));
        Assert.Empty(viewerRows);
        Assert.False(viewerMay);
    }

    [Fact]
    public async Task Reports_TotalThePayOfThePeriod_AndAnEmptyExportIsEmpty()
    {
        var (harness, setup, _) = await ProcessedAsync();
        await using var _ = harness;
        var viewer = CalendarTestHarness.User("viewer", PayrollPermissions.ViewPayRuns.Name);

        var (register, summary, cash, emptyExport) = await harness.RunAsync(async services =>
        {
            var reader = new PayrollReportReader(services.GetRequiredService<IPayrollScopeService>(), services.GetRequiredService<PayStatementStore>(), services.GetRequiredService<PayRunStore>(), services.GetRequiredService<EmployerStore>(), new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = viewer } });
            var clock = services.GetRequiredService<ILocalClock>();
            var all = new ReportContext(new ReportFilter());
            var later = new ReportFilter();

            later.SetDateRange(new ReportDateRange { FromUtc = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), ToUtc = new DateTime(2027, 1, 31, 0, 0, 0, DateTimeKind.Utc) });

            return (
                await new PayrollRegisterReport(reader, clock, new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<PayrollRegisterReport>()).RunAsync(all, TestContext.Current.CancellationToken),
                await new PayrollSummaryReport(reader, clock, new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<PayrollSummaryReport>()).RunAsync(all, TestContext.Current.CancellationToken),
                await new CashRequirementReport(reader, clock, new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<CashRequirementReport>()).RunAsync(all, TestContext.Current.CancellationToken),
                await new PayrollRegisterReport(reader, clock, new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<PayrollRegisterReport>()).RunAsync(new ReportContext(later, ReportRunMode.Export), TestContext.Current.CancellationToken));
        }, commit: false);

        var table = register.Sections.Single(section => section.Kind == ReportSectionKind.Table);
        var total = Assert.Single(table.Rows, row => row.Kind == ReportRowKind.GrandTotal);
        Assert.Equal("1600.00 USD", total.Cells[3]);
        Assert.Equal("1500.80 USD", total.Cells[6]);
        Assert.Contains(summary.Sections.Single().Rows, row => row.Cells[0] == "Social insurance");
        Assert.Contains(cash.Sections.Single(section => section.Kind == ReportSectionKind.Table).Rows, row => row.Kind == ReportRowKind.GrandTotal);
        Assert.Empty(emptyExport.Sections);
    }

    [Fact]
    public async Task Reports_ExportOnlyWithExportPayrollData_AndReadNothingWithoutAUser()
    {
        var (harness, setup, _) = await ProcessedAsync();
        await using var _ = harness;

        // Every payroll report needs Export payroll data to be exported or attached.
        Assert.All(
            [PayrollRegisterReport.ExportPermission, PayrollSummaryReport.ExportPermission, CashRequirementReport.ExportPermission, DeductionsAndContributionsReport.ExportPermission, LaborDistributionReport.ExportPermission, YearToDateEarningsReport.ExportPermission],
            permission => Assert.Equal(PayrollPermissions.ExportPayrollData.Name, permission.Name));

        // Sees Contoso's runs, but exports only another employer's data: on screen the register has rows, in a file none.
        var viewer = CalendarTestHarness.User("viewer", PayrollPermissions.ViewPayRuns.Name, PayrollPermissionTemplates.For(PayrollPermissions.ExportPayrollData, "fabrikam").Name);

        var (display, export, background, owner) = await harness.RunAsync(async services =>
        {
            PayrollReportReader Reader(System.Security.Claims.ClaimsPrincipal user)
                => new(services.GetRequiredService<IPayrollScopeService>(), services.GetRequiredService<PayStatementStore>(), services.GetRequiredService<PayRunStore>(), services.GetRequiredService<EmployerStore>(), new HttpContextAccessor { HttpContext = user is null ? null : new DefaultHttpContext { User = user } });

            PayrollRegisterReport Register(System.Security.Claims.ClaimsPrincipal user)
                => new(Reader(user), services.GetRequiredService<ILocalClock>(), new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<PayrollRegisterReport>());

            return (
                await Register(viewer).RunAsync(new ReportContext(new ReportFilter()), TestContext.Current.CancellationToken),
                await Register(viewer).RunAsync(new ReportContext(new ReportFilter(), ReportRunMode.Export), TestContext.Current.CancellationToken),

                // A subscription with no request and no owner reads nothing; with its owner, what the owner sees.
                await Register(null).RunAsync(new ReportContext(new ReportFilter()), TestContext.Current.CancellationToken),
                await Register(null).RunAsync(new ReportContext(new ReportFilter()) { User = viewer }, TestContext.Current.CancellationToken));
        }, commit: false);

        static int Rows(ReportDocument document)
            => document.Sections.Where(section => section.Kind == ReportSectionKind.Table).Sum(section => section.Rows.Count);

        Assert.True(Rows(display) > 0);
        Assert.Empty(export.Sections);
        Assert.Equal(0, Rows(background));
        Assert.Equal(Rows(display), Rows(owner));
    }

    [Fact]
    public async Task WorkflowTasks_WriteTheRunsAmountsOnlyWhenAskedAndAllowed()
    {
        var context = new DisbursementsTestContext();
        await using var harness = await PayRunTests.CreateHarnessAsync(services => PayRunProcessingTests.AddPayments(services, context, deposits: false, checks: true));
        var setup = await PayRunTests.SetUpAsync(harness);
        var evaluator = new Mock<global::OrchardCore.Workflows.Services.IWorkflowExpressionEvaluator>();

        evaluator
            .Setup(service => service.EvaluateAsync(It.IsAny<global::OrchardCore.Workflows.Models.WorkflowExpression<string>>(), It.IsAny<global::OrchardCore.Workflows.Models.WorkflowExecutionContext>(), It.IsAny<System.Text.Encodings.Web.TextEncoder>()))
            .Returns<global::OrchardCore.Workflows.Models.WorkflowExpression<string>, global::OrchardCore.Workflows.Models.WorkflowExecutionContext, System.Text.Encodings.Web.TextEncoder>((expression, _, _) => Task.FromResult(expression.Expression));

        async Task<IDictionary<string, object>> CalculateAsync(string includeTotals, System.Security.Claims.ClaimsPrincipal user)
            => await harness.RunAsync(async services =>
            {
                if (services.GetService<IHttpContextAccessor>() is { } accessor)
                {
                    accessor.HttpContext = user is null ? null : new DefaultHttpContext { User = user };
                }

                var task = new CalculatePayRunTask(services, evaluator.Object, new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<CalculatePayRunTask>()) { PayRunId = setup.RunId, IncludeTotals = includeTotals };
                var workflow = Context();

                await task.ExecuteAsync(workflow, null);

                return workflow.Output;
            });

        var preparer = CalendarTestHarness.User("preparer", PayrollPermissions.PreparePayRuns.Name);
        var reader = CalendarTestHarness.User("reader", PayrollPermissions.PreparePayRuns.Name, PayrollPermissions.ViewPayrollReports.Name);

        var notAsked = await CalculateAsync(null, reader);
        var notAllowed = await CalculateAsync("true", preparer);
        var allowed = await CalculateAsync("true", reader);

        Assert.True(notAsked.ContainsKey("Statements"));
        Assert.False(notAsked.ContainsKey("Gross"));
        Assert.False(notAllowed.ContainsKey("Gross"));
        Assert.Equal(1600m, Convert.ToDecimal(allowed["Gross"], System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void WorkflowEvents_FollowEachStepOfARun()
    {
        Assert.Equal([nameof(PayRunOpenedEvent)], PayrollWorkflowObserver.EventsFor(PayRunChangeKind.Opened));
        Assert.Equal([nameof(PayRunApprovedEvent)], PayrollWorkflowObserver.EventsFor(PayRunChangeKind.Approved));
        Assert.Equal([nameof(PayRunProcessedEvent), nameof(PayRunPaidEvent)], PayrollWorkflowObserver.EventsFor(PayRunChangeKind.Paid));
        Assert.Equal([nameof(PayRunProcessedEvent)], PayrollWorkflowObserver.EventsFor(PayRunChangeKind.PartiallyPaid));
        Assert.Equal([nameof(PayRunProcessedEvent), nameof(PayRunFailedEvent)], PayrollWorkflowObserver.EventsFor(PayRunChangeKind.Failed));
        Assert.Empty(PayrollWorkflowObserver.EventsFor(PayRunChangeKind.Canceled));
    }

    [Fact]
    public async Task Webhooks_SendARunsCountAndDates_ButNeverAnAmount()
    {
        var published = new List<(string Name, string Payload)>();
        var publisher = new Mock<IWebhookPublisher>();

        publisher
            .Setup(service => service.PublishAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, object, string, CancellationToken>((name, payload, _, _) => published.Add((name, JsonSerializer.Serialize(payload))))
            .Returns(Task.CompletedTask);

        var observer = new PayrollWebhookObserver(publisher.Object);
        var run = new PayRun { ItemId = "run1", Number = "7", EmployerId = "contoso", Status = PayRunStatus.Paid, Totals = new PayRunTotals { Statements = 2, Gross = 3200m, Net = 3001.60m } };
        var statement = new PayStatement { ItemId = "s1", Number = "11", RunId = "run1", StaffMemberId = "lee", EmployerId = "contoso", Gross = 1600m, Net = 1500.80m, Status = PayStatementStatus.Paid };

        await observer.ChangedAsync(new PayRunChange { Run = run, Kind = PayRunChangeKind.Calculated }, TestContext.Current.CancellationToken);
        await observer.ChangedAsync(new PayRunChange { Run = run, Kind = PayRunChangeKind.Paid }, TestContext.Current.CancellationToken);
        await observer.IssuedAsync(statement, TestContext.Current.CancellationToken);

        Assert.Equal([PayrollWebhookEvents.PayRunProcessed, PayrollWebhookEvents.PayStatementIssued], published.Select(entry => entry.Name));
        Assert.Contains("\"statements\":2", published[0].Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("3001.60", published[0].Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("3200", published[0].Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("1500.80", published[1].Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("1600", published[1].Payload, StringComparison.Ordinal);
        Assert.Contains("\"staffMemberId\":\"lee\"", published[1].Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuditTrail_RecordsWhoSubmittedDecidedProcessedAndVoided()
    {
        var recorded = new List<AuditTrailContext<PayrollAuditData>>();
        var manager = new Mock<IAuditTrailManager>();

        manager
            .Setup(service => service.RecordEventAsync(It.IsAny<AuditTrailContext<PayrollAuditData>>()))
            .Callback<AuditTrailContext<PayrollAuditData>>(context => recorded.Add(context))
            .Returns(Task.CompletedTask);

        var observer = new AuditTrailPayRunObserver(manager.Object, new HttpContextAccessor());
        var run = new PayRun { ItemId = "run1", Number = "7", EmployerId = "contoso", Totals = new PayRunTotals { Statements = 2 } };

        foreach (var kind in new[] { PayRunChangeKind.Calculated, PayRunChangeKind.Submitted, PayRunChangeKind.Approved, PayRunChangeKind.Paid, PayRunChangeKind.Canceled })
        {
            await observer.ChangedAsync(new PayRunChange { Run = run, Kind = kind, Actor = "pat", ActorId = "pat-id" }, TestContext.Current.CancellationToken);
        }

        await observer.VoidedAsync(new PayStatement { ItemId = "s1", Number = "11", StaffMemberId = "lee", EmployerId = "contoso", Net = 1500.80m }, new PayStatement { Warnings = ["Wrong hours"] }, TestContext.Current.CancellationToken);

        Assert.Equal(
            [PayrollAuditTrailEvents.RunSubmitted, PayrollAuditTrailEvents.RunDecided, PayrollAuditTrailEvents.RunProcessed, PayrollAuditTrailEvents.RunCanceled, PayrollAuditTrailEvents.StatementVoided],
            recorded.Select(context => context.Name));
        Assert.Equal("pat", recorded[0].UserName);
        Assert.Equal("7", recorded[0].AuditTrailEventItem.RunNumber);
        Assert.Equal("Wrong hours", recorded[^1].AuditTrailEventItem.Reason);
    }

    [Fact]
    public void Calendar_ShowsEachPayDateOfASchedulesPeriods()
    {
        var schedule = new PaySchedule { Frequency = PayFrequency.Biweekly, AnchorDate = new DateOnly(2026, 11, 1), PayDateOffsetDays = 6 };

        var payDates = PayDateCalendarEventSource.PayDates(schedule, new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 31)).ToList();

        Assert.Equal(payDates.Distinct().Count(), payDates.Count);
        Assert.All(payDates, day => Assert.InRange(day, new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 31)));
        Assert.InRange(payDates.Count, 4, 5);
        Assert.All(payDates.Zip(payDates.Skip(1)), pair => Assert.InRange(pair.Second.DayNumber - pair.First.DayNumber, 12, 16));
    }

    [Fact]
    public async Task PersonalData_ExportsThePersonsProfilesAndStatements_KeepingThemForTheLaw()
    {
        var (harness, setup, _) = await ProcessedAsync(linkUser: "lee-user");
        await using var _ = harness;
        var export = new PersonalDataExport();
        var result = new PersonalDataAnonymizeResult();

        await harness.RunAsync(async services =>
        {
            var contributor = new PayrollPersonalDataContributor(
                services.GetRequiredService<CrestApps.OrchardCore.Staff.Services.IStaffDirectory>(),
                services.GetRequiredService<IPayProfileManager>(),
                services.GetRequiredService<PayStatementStore>(),
                services.GetRequiredService<EmployerStore>(),
                new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<PayrollPersonalDataContributor>());
            var subject = new PersonalDataSubject(userId: "lee-user");

            await contributor.ExportAsync(new PersonalDataExportContext(subject, export), TestContext.Current.CancellationToken);
            await contributor.AnonymizeAsync(new PersonalDataAnonymizeContext(subject, null, result), TestContext.Current.CancellationToken);
        }, commit: false);

        var profile = Assert.Single(export.Records, record => record.Kind == PayrollPersonalDataContributor.ProfileKind);
        var statement = Assert.Single(export.Records, record => record.Kind == PayrollPersonalDataContributor.StatementKind);
        Assert.Equal("6789", profile.Fields["TaxIdLastCharacters"]);
        Assert.DoesNotContain(profile.Fields.Values, value => value is not null && value.Contains("123456789", StringComparison.Ordinal));
        Assert.Equal("1500.80", statement.Fields["Net"]);
        Assert.Equal(0, result.Total);
    }

    [Fact]
    public async Task PayHistory_IsImportedAsHistory_CountsInTheYear_AndCantBeVoided()
    {
        await using var harness = await PayRunTests.CreateHarnessAsync();
        var setup = await PayRunTests.SetUpAsync(harness);
        var step = new JsonObject
        {
            ["name"] = PayrollConstants.PayHistoryRecipeStepName,
            ["Statements"] = new JsonArray(new JsonObject
            {
                ["ItemId"] = "history1",
                ["Number"] = "H-1",
                ["EmployerId"] = setup.EmployerId,
                ["EmployeeNumber"] = "1001",
                ["PayDate"] = "2026-03-15",
                ["PeriodStart"] = "2026-03-01",
                ["PeriodEnd"] = "2026-03-14",
                ["Currency"] = "USD",
                ["Gross"] = 2000m,
                ["Net"] = 1876m,
                ["Status"] = "Paid",
                ["Lines"] = new JsonArray(
                    new JsonObject { ["Type"] = "Earning", ["PayCodeId"] = setup.BonusCodeId, ["Description"] = "Bonus", ["Amount"] = 2000m },
                    new JsonObject { ["Type"] = "Tax", ["TaxCode"] = "US-SS", ["Payer"] = "Employee", ["Amount"] = 124m, ["TaxableWages"] = 2000m }),
            }),
        };

        var errors = await harness.RunAsync(async services => await RecipeRoundTrip.RunAsync(Step(services), step));
        var again = await harness.RunAsync(async services => await RecipeRoundTrip.RunAsync(Step(services), step));

        var (statement, run, yearBonus, refused) = await harness.RunAsync(async services =>
        {
            var stored = await services.GetRequiredService<PayStatementStore>().FindByIdAsync("history1");
            var accumulators = await services.GetRequiredService<IPayAccumulatorService>().GetAsync(setup.StaffMemberId, setup.EmployerId, new DateOnly(2026, 11, 20), new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 14));
            var voided = await services.GetRequiredService<IPayRunService>().VoidStatementAsync(stored, CalendarTestHarness.User("processor", PayrollPermissions.VoidPayStatements.Name), "Try");

            return (stored, await services.GetRequiredService<IPayRunManager>().FindByIdAsync(stored.RunId), accumulators.For(PayAccumulators.CodeKey(setup.BonusCodeId)).Year, voided);
        }, commit: false);

        Assert.Empty(errors);
        Assert.Empty(again);
        Assert.Equal((PayStatementStatus.Paid, "H-1"), (statement.Status, statement.Number));
        Assert.Equal((PayRunType.Historical, PayRunStatus.Paid, 1), (run.Type, run.Status, run.Totals.Statements));
        Assert.Equal(2000m, yearBonus);
        Assert.False(refused.Succeeded);

        static PayHistoryRecipeStep Step(IServiceProvider services)
            => new(
                services.GetRequiredService<PayStatementStore>(),
                services.GetRequiredService<IPayRunManager>(),
                services.GetRequiredService<EmployerStore>(),
                services.GetRequiredService<CrestApps.OrchardCore.Staff.Services.IStaffMemberManager>(),
                services.GetRequiredService<CrestApps.OrchardCore.Numbering.INumberSequence>(),
                services.GetRequiredService<IClock>());
    }

    [Fact]
    public async Task Notices_TellAPersonOfAReturnedDepositAndOfAChangedBankAccount_OnlyTheirOwn()
    {
        var sent = new List<global::OrchardCore.Notifications.INotificationMessage>();
        var notifications = new Mock<INotificationService>();
        var lee = new global::OrchardCore.Users.Models.User { UserId = "lee-user", UserName = "lee" };
        var users = new Mock<Microsoft.AspNetCore.Identity.UserManager<global::OrchardCore.Users.IUser>>(Mock.Of<Microsoft.AspNetCore.Identity.IUserStore<global::OrchardCore.Users.IUser>>(), null, null, null, null, null, null, null, null);

        notifications
            .Setup(service => service.SendAsync(It.IsAny<object>(), It.IsAny<global::OrchardCore.Notifications.INotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<object, global::OrchardCore.Notifications.INotificationMessage, CancellationToken>((_, message, _) => sent.Add(message))
            .ReturnsAsync(new NotificationSendResult());
        users.Setup(manager => manager.FindByIdAsync("lee-user")).ReturnsAsync(lee);

        await using var harness = await PayRunTests.CreateHarnessAsync(services =>
        {
            services.AddSingleton(notifications.Object);
            services.AddSingleton(users.Object);
        });
        var setup = await PayRunTests.SetUpAsync(harness);
        await harness.RunAsync(async services =>
        {
            var staff = services.GetRequiredService<CrestApps.OrchardCore.Staff.Services.IStaffMemberManager>();
            var member = await staff.FindByIdAsync(setup.StaffMemberId);

            member.UserId = "lee-user";
            await staff.UpdateAsync(member);
        });

        await harness.RunAsync(async services =>
        {
            var deposits = new DepositReturnedNotificationObserver(services, new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<DepositReturnedNotificationObserver>());
            var accounts = new BankAccountChangedNotificationObserver(services, new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<BankAccountChangedNotificationObserver>());
            var returned = new Disbursement { ItemId = "d1", Payee = new CrestApps.OrchardCore.Parties.Models.PartyRef("Staff", setup.StaffMemberId), SourceKind = PayStatementSources.Kind, SourceId = "s1", FailureCode = "R03", FailureReason = "No account" };

            await deposits.OnChangedAsync(new DisbursementChange { Disbursement = returned, Kind = DisbursementChangeKind.Returned }, TestContext.Current.CancellationToken);
            await deposits.OnChangedAsync(new DisbursementChange { Disbursement = returned, Kind = DisbursementChangeKind.Settled }, TestContext.Current.CancellationToken);
            await accounts.OnChangedAsync(new BankAccountChange { Account = new BankAccount { ItemId = "a1", Name = "Pay account", Owner = new CrestApps.OrchardCore.Parties.Models.PartyRef("Staff", setup.StaffMemberId) }, Kinds = [BankAccountChangeKind.NumberChanged], Actor = "pat" }, TestContext.Current.CancellationToken);
            await accounts.OnChangedAsync(new BankAccountChange { Account = new BankAccount { ItemId = "a2", Name = "Vendor", Owner = new CrestApps.OrchardCore.Parties.Models.PartyRef("Vendor", "v1") }, Kinds = [BankAccountChangeKind.NumberChanged] }, TestContext.Current.CancellationToken);
        }, commit: false);

        Assert.Equal([PayrollConstants.DepositReturnedNotificationType, PayrollConstants.BankAccountChangedNotificationType], sent.Select(message => message.Type));
        Assert.Equal("~/Admin/my-pay/statements/s1", sent[0].Url);
        Assert.Contains("pat", sent[1].Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorkflowTasks_OpenARunOncePerPeriod_AndANoUserApprovalIsNotAllowed()
    {
        var context = new DisbursementsTestContext();
        var origin = await context.Checks.BusinessAccountAsync();
        await using var harness = await PayRunTests.CreateHarnessAsync(services => PayRunProcessingTests.AddPayments(services, context, deposits: false, checks: true));
        var setup = await PayRunTests.SetUpAsync(harness, employerSetup: employer => employer.FundingBankAccountId = origin.ItemId);
        var evaluator = new Mock<global::OrchardCore.Workflows.Services.IWorkflowExpressionEvaluator>();

        evaluator
            .Setup(service => service.EvaluateAsync(It.IsAny<global::OrchardCore.Workflows.Models.WorkflowExpression<string>>(), It.IsAny<global::OrchardCore.Workflows.Models.WorkflowExecutionContext>(), It.IsAny<System.Text.Encodings.Web.TextEncoder>()))
            .Returns<global::OrchardCore.Workflows.Models.WorkflowExpression<string>, global::OrchardCore.Workflows.Models.WorkflowExecutionContext, System.Text.Encodings.Web.TextEncoder>((expression, _, _) => Task.FromResult(expression.Expression));

        var (first, second, schedule) = await harness.RunAsync(async services =>
        {
            var run = await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId);
            var task = new OpenPayRunTask(services, evaluator.Object, new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<OpenPayRunTask>())
            {
                EmployerId = setup.EmployerId,
                PayScheduleId = run.PayScheduleId,
                PeriodStart = "2026-12-01",
                PeriodEnd = "2026-12-14",
                PayDate = "2026-12-18",
            };

            var workflow = Context();
            var opened = await task.ExecuteAsync(workflow, null);
            var again = await task.ExecuteAsync(Context(), null);

            return (opened, again, workflow.Output["PayRunId"]);
        });

        await PayRunProcessingTests.ApproveAsync(harness, setup);

        Assert.Equal("Done", Assert.Single(first.Outcomes));
        Assert.Equal("Exists", Assert.Single(second.Outcomes));
        Assert.NotNull(schedule);

        // A workflow with no person can't approve: the policy wants a person, and not the one who prepared the run.
        var (preparerRun, noUser) = await harness.RunAsync(async services =>
        {
            var runs = services.GetRequiredService<IPayRunManager>();
            var newRun = await runs.FindByIdAsync((string)schedule);

            await services.GetRequiredService<IPayRunService>().CalculateAsync(newRun, CalendarTestHarness.User("preparer", PayrollPermissions.PreparePayRuns.Name));

            return (newRun, 0);
        });

        var decided = await harness.RunAsync(async services =>
        {
            var newRun = await services.GetRequiredService<IPayRunManager>().FindByIdAsync((string)schedule);

            await services.GetRequiredService<IPayRunService>().SubmitAsync(newRun, CalendarTestHarness.User("preparer", PayrollPermissions.PreparePayRuns.Name));

            var task = new ApprovePayRunTask(services, evaluator.Object, new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<ApprovePayRunTask>()) { PayRunId = (string)schedule };

            return await task.ExecuteAsync(Context(), null);
        });

        Assert.Equal("NotAllowed", Assert.Single(decided.Outcomes));
        Assert.NotNull(preparerRun);
        Assert.Equal(0, noUser);
    }

    private static global::OrchardCore.Workflows.Models.WorkflowExecutionContext Context()
        => new(
            new global::OrchardCore.Workflows.Models.WorkflowType { WorkflowTypeId = "t1" },
            new global::OrchardCore.Workflows.Models.Workflow { WorkflowId = "w1" },
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            [],
            null,
            []);

    // A run of Contoso approved and processed: one statement for Lee, gross 1600 and net 1500.80, paid by check.
    private static async Task<(CalendarTestHarness Harness, PayRunTests.Setup Setup, DisbursementsTestContext Payments)> ProcessedAsync(string linkUser = null)
    {
        var context = new DisbursementsTestContext();
        var origin = await context.Checks.BusinessAccountAsync();
        var harness = await PayRunTests.CreateHarnessAsync(services => PayRunProcessingTests.AddPayments(services, context, deposits: false, checks: true));
        var setup = await PayRunTests.SetUpAsync(harness, employerSetup: employer => employer.FundingBankAccountId = origin.ItemId);

        if (linkUser is not null)
        {
            await harness.RunAsync(async services =>
            {
                var staff = services.GetRequiredService<CrestApps.OrchardCore.Staff.Services.IStaffMemberManager>();
                var member = await staff.FindByIdAsync(setup.StaffMemberId);

                member.UserId = linkUser;
                await staff.UpdateAsync(member);
            });
        }

        await PayRunProcessingTests.ApproveAsync(harness, setup);
        await harness.RunAsync(async services => Assert.True((await services.GetRequiredService<IPayRunService>().ProcessAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId), CalendarTestHarness.User("processor", PayrollPermissions.ProcessPayRuns.Name))).Succeeded));

        return (harness, setup, context);
    }

    // The scope's services, with one service replaced.
    private sealed class WithService(IServiceProvider services, Type serviceType, object instance) : IServiceProvider
    {
        public object GetService(Type type)
            => type == serviceType ? instance : services.GetService(type);
    }

    [Fact]
    public async Task PayHistory_RefusesATaxLineWithoutItsTaxableWages()
    {
        var (harness, setup, _) = await ProcessedAsync();
        await using var _ = harness;
        var step = new JsonObject
        {
            ["name"] = PayrollConstants.PayHistoryRecipeStepName,
            [PayHistoryRecipeStep.EntriesName] = new JsonArray(new JsonObject
            {
                ["EmployerId"] = setup.EmployerId,
                ["StaffMemberId"] = setup.StaffMemberId,
                ["PayDate"] = "2026-03-06",
                ["Status"] = "Paid",
                ["Gross"] = 1000,
                ["Net"] = 923.50,
                ["Lines"] = new JsonArray(
                    new JsonObject { ["Type"] = "Earning", ["PayCodeId"] = setup.BonusCodeId, ["Amount"] = 1000 },
                    new JsonObject { ["Type"] = "Tax", ["TaxCode"] = "US-SS", ["Amount"] = 62, ["Payer"] = "Employee" },
                    new JsonObject { ["Type"] = "Tax", ["TaxCode"] = "US-MED", ["Amount"] = 14.50, ["Payer"] = "Employee", ["TaxableWages"] = 1000 }),
            }),
        };

        var errors = await harness.RunAsync(async services =>
        {
            var context = new global::OrchardCore.Recipes.Models.RecipeExecutionContext { Name = PayrollConstants.PayHistoryRecipeStepName, Step = step };

            await ActivatorUtilities.CreateInstance<PayHistoryRecipeStep>(services).ExecuteAsync(context);

            return context.Errors.ToList();
        });

        Assert.Contains(errors, error => error.Contains("without TaxableWages", StringComparison.Ordinal));
        Assert.Empty(await harness.RunAsync(async services => await services.GetRequiredService<PayStatementStore>().ListAsync(index => index.PayDay == 20260306), commit: false));
    }

    private static List<Dictionary<string, object>> Rows(DataRowSet table)
        => [.. table.Rows.Select(row => table.Fields.Select((field, index) => (field.Name, Value: row[index])).ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal))];
}
