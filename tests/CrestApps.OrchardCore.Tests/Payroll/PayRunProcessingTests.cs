using System.Security.Claims;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.Services;
using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.Banking.Disbursements.Services;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Payroll.Core;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.SourceAccess;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Disbursements;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using UglyToad.PdfPig;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// Processing pay runs: statements made final and numbered, net pay deposited through Disbursements (an ACH file) and
/// paid by check through Checks (a printed check with the statement as its voucher), pay items moved toward their goals,
/// source inputs taken, and a void that reverses the accumulators, gives the inputs back and stops the check.
/// </summary>
public sealed class PayRunProcessingTests
{
    private static readonly DateOnly _payDate = new(2026, 11, 20);

    [Fact]
    public async Task Process_DepositsAndPrintsACheck_FromOneRun()
    {
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        await using var harness = await PayRunTests.CreateHarnessAsync(services => AddPayments(services, context, deposits: true, checks: true));
        var setup = await PayRunTests.SetUpAsync(
            harness,
            employerSetup: employer => employer.FundingBankAccountId = origin.ItemId,
            profileSetup: async profile =>
            {
                var payee = await context.PayeeAsync(staffId: profile.StaffMemberId);

                profile.Distribution =
                [
                    new NetPayRow { Method = NetPayMethod.BankAccount, BankAccountId = payee.ItemId, Split = NetPaySplit.Amount, Amount = 500 },
                    new NetPayRow { Method = NetPayMethod.Check, Split = NetPaySplit.Remainder },
                ];
            });
        var processor = CalendarTestHarness.User("processor", PayrollPermissionTemplates.For(PayrollPermissions.ProcessPayRuns, setup.EmployerId).Name);
        await ApproveAsync(harness, setup);

        var processed = await harness.RunAsync(async services =>
        {
            var run = await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId);

            return await services.GetRequiredService<IPayRunService>().ProcessAsync(run, processor);
        });

        var (run, statement, again) = await harness.RunAsync(async services =>
        {
            var found = await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId);
            var statements = await services.GetRequiredService<IPayRunService>().ListStatementsAsync(found);

            return (found, Assert.Single(statements), await services.GetRequiredService<IPayRunService>().ProcessAsync(found, processor));
        }, commit: false);

        Assert.True(processed.Succeeded, string.Join(" ", processed.Errors));
        Assert.False(again.Succeeded);
        Assert.Equal(PayRunStatus.Paid, run.Status);
        Assert.Equal("processor", run.ProcessedBy);
        Assert.Equal(PayStatementStatus.Paid, statement.Status);
        Assert.False(string.IsNullOrEmpty(statement.Number));
        Assert.Equal(1500.80m, statement.Net);
        Assert.Equal(new[] { NetPayMethod.BankAccount, NetPayMethod.Check }, statement.Payments.Select(payment => payment.Method));
        Assert.Equal(new[] { 500m, 1000.80m }, statement.Payments.Select(payment => payment.Amount));

        // The deposit is an ACH disbursement from the employer's funding account, scoped by the employer, once.
        var deposit = Assert.Single(context.Store.Entries);
        Assert.Equal(statement.Payments[0].ReferenceId, deposit.ItemId);
        Assert.Equal((PayStatementSources.Kind, statement.ItemId, setup.EmployerId, origin.ItemId, 500m), (deposit.SourceKind, deposit.SourceId, deposit.AccessScope, deposit.SourceBankAccountId, deposit.Amount));

        var (batch, problems) = await context.Service.CreateBatchAsync("Nacha", [deposit.ItemId], _payDate, "PAYROLL", TestContext.Current.CancellationToken);
        Assert.Empty(problems);
        await context.Service.ReleaseBatchAsync(batch, TestContext.Current.CancellationToken);
        var achFile = System.Text.Encoding.ASCII.GetString(context.Files.Files[batch.ItemId]);
        Assert.Contains("PAYROLL", achFile, StringComparison.Ordinal);
        Assert.Contains("0000050000", achFile, StringComparison.Ordinal);

        // The rest is a check on the same account, printed with the statement as its voucher.
        var check = await context.Checks.Manager.FindByIdAsync(statement.Payments[1].ReferenceId, TestContext.Current.CancellationToken);
        Assert.Equal((1000.80m, PayStatementSources.Kind, statement.ItemId, origin.ItemId), (check.Amount, check.SourceKind, check.SourceId, check.BankAccountId));
        Assert.StartsWith("Check ", statement.Payments[1].Reference, StringComparison.Ordinal);
        Assert.Equal("to Pay account", statement.Payments[0].Reference);

        var printed = await harness.RunAsync(async services =>
        {
            var vouchers = services.GetServices<ICheckVoucherSource>().ToArray();
            var layout = new CheckLayout { ItemId = "layout1", Name = "Pre-printed", Stock = CheckStock.PrePrinted };

            return await context.Checks.Printer(vouchers).PrintAsync(layout, [check], "print1");
        }, commit: false);

        Assert.True(printed.Succeeded, string.Join(" ", printed.Errors));
        using var document = PdfDocument.Open(printed.Pdf);
        var text = document.GetPage(1).Text;
        Assert.Contains("Net pay", text, StringComparison.Ordinal);
        Assert.Contains("Social insurance", text, StringComparison.Ordinal);
        Assert.Contains(statement.Number, text, StringComparison.Ordinal);
        Assert.Contains("This check", text, StringComparison.Ordinal);
        Assert.Contains("Deposit to Pay account", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Void_ReversesTheAccumulatorsAndThePayItem_GivesTheInputsBack_AndStopsTheCheck()
    {
        var context = new DisbursementsTestContext();
        var origin = await context.Checks.BusinessAccountAsync();
        var source = new RecordingPayInputSource();
        await using var harness = await PayRunTests.CreateHarnessAsync(services =>
        {
            AddPayments(services, context, deposits: false, checks: true);
            services.AddSingleton<IPayInputSource>(source);
        });
        var setup = await PayRunTests.SetUpAsync(harness, employerSetup: employer => employer.FundingBankAccountId = origin.ItemId);
        var loanItemId = await harness.RunAsync(async services =>
        {
            var profile = Assert.Single(await services.GetRequiredService<IPayProfileManager>().ListByEmployersAsync([setup.EmployerId]));
            var items = services.GetRequiredService<IPayItemManager>();
            var item = await items.NewAsync();

            item.ProfileId = profile.ItemId;
            item.PayCodeId = (await services.GetRequiredService<IPayCodeManager>().FindByCodeAsync("LOAN")).ItemId;
            item.Amount = 100;
            item.Goal = 1000;
            item.StartDate = new DateOnly(2026, 1, 1);
            await items.CreateAsync(item);

            source.Inputs.Add(new PayInput
            {
                SourceKind = RecordingPayInputSource.Kind,
                SourceId = "sheet-1",
                StaffMemberId = setup.StaffMemberId,
                PayCodeId = (await services.GetRequiredService<IPayCodeManager>().FindByCodeAsync("REG")).ItemId,
                Hours = 8,
                PeriodStart = new DateOnly(2026, 11, 1),
                PeriodEnd = new DateOnly(2026, 11, 14),
            });

            return item.ItemId;
        });
        var processor = CalendarTestHarness.User("processor", PayrollPermissions.ProcessPayRuns.Name, PayrollPermissions.VoidPayStatements.Name);
        await ApproveAsync(harness, setup);

        await harness.RunAsync(async services =>
        {
            var run = await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId);
            var result = await services.GetRequiredService<IPayRunService>().ProcessAsync(run, processor);

            Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        });

        var (statementId, regularBefore, loanPaid, consumed) = await harness.RunAsync(async services =>
        {
            var statement = Assert.Single(await services.GetRequiredService<IPayRunService>().ListStatementsAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId)));
            var accumulators = await services.GetRequiredService<IPayAccumulatorService>().GetAsync(setup.StaffMemberId, setup.EmployerId, _payDate, statement.PeriodStart, statement.PeriodEnd);
            var regular = (await services.GetRequiredService<IPayCodeManager>().FindByCodeAsync("REG")).ItemId;

            return (statement.ItemId, accumulators.For(PayAccumulators.CodeKey(regular)).Year, (await services.GetRequiredService<IPayItemManager>().FindByIdAsync(loanItemId)).PaidTowardGoal, source.Consumed.ToList());
        }, commit: false);

        var voided = await harness.RunAsync(async services =>
        {
            var statement = await services.GetRequiredService<PayStatementStore>().FindByIdAsync(statementId);
            var refused = await services.GetRequiredService<IPayRunService>().VoidStatementAsync(statement, processor, " ");

            Assert.False(refused.Succeeded);

            return await services.GetRequiredService<IPayRunService>().VoidStatementAsync(statement, processor, "Paid the wrong hours");
        });

        var (original, reversal, regularAfter, grossAfter, loanAfter, run) = await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<PayStatementStore>();
            var statement = await store.FindByIdAsync(statementId);
            var accumulators = await services.GetRequiredService<IPayAccumulatorService>().GetAsync(setup.StaffMemberId, setup.EmployerId, _payDate, statement.PeriodStart, statement.PeriodEnd);
            var regular = (await services.GetRequiredService<IPayCodeManager>().FindByCodeAsync("REG")).ItemId;

            return (
                statement,
                await store.FindByIdAsync(statement.VoidedBy),
                accumulators.For(PayAccumulators.CodeKey(regular)).Year,
                accumulators.Totals.Values.Sum(total => total.Year),
                (await services.GetRequiredService<IPayItemManager>().FindByIdAsync(loanItemId)).PaidTowardGoal,
                await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId));
        }, commit: false);

        var check = Assert.Single(context.Checks.Store.Entries);

        Assert.True(voided.Succeeded, string.Join(" ", voided.Errors));
        Assert.Equal(1760m, regularBefore);
        Assert.Equal(100m, loanPaid);
        Assert.Equal(new[] { ("payrun:" + setup.RunId, "sheet-1") }, consumed);
        Assert.Equal(PayStatementStatus.Voided, original.Status);
        Assert.Equal(original.ItemId, reversal.VoidOf);
        Assert.Equal(-original.Net, reversal.Net);
        Assert.Equal(original.Lines.Select(line => -line.Amount), reversal.Lines.Select(line => line.Amount));
        Assert.Equal(0m, regularAfter);
        Assert.Equal(0m, grossAfter);
        Assert.Equal(0m, loanAfter);
        Assert.Equal(CheckStatus.Voided, check.Status);
        Assert.Equal(new[] { ("payrun:" + setup.RunId, "sheet-1") }, source.Released);
        Assert.Equal(0, run.Totals.Statements);
        Assert.Equal(voided.Statement.ItemId, reversal.ItemId);
    }

    [Fact]
    public async Task Process_WithoutAFeatureToPay_LeavesTheRunPartlyPaid_AndADepositFallsBackToACheck()
    {
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();

        // Neither Direct Deposit nor Paychecks: the statement is final and numbered, its payment waits.
        await using (var bare = await PayRunTests.CreateHarnessAsync())
        {
            var setup = await PayRunTests.SetUpAsync(bare, employerSetup: employer => employer.FundingBankAccountId = origin.ItemId);
            var processor = CalendarTestHarness.User("processor", PayrollPermissions.ProcessPayRuns.Name);
            await ApproveAsync(bare, setup);

            var result = await bare.RunAsync(async services => await services.GetRequiredService<IPayRunService>().ProcessAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId), processor));
            var (run, statement) = await bare.RunAsync(async services =>
            {
                var found = await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId);

                return (found, Assert.Single(await services.GetRequiredService<IPayRunService>().ListStatementsAsync(found)));
            }, commit: false);

            Assert.False(result.Succeeded);
            Assert.Equal(PayRunStatus.PartiallyPaid, run.Status);
            Assert.Equal(PayStatementStatus.Final, statement.Status);
            Assert.False(string.IsNullOrEmpty(statement.Number));
            Assert.Equal("Turn on Paychecks to pay by check.", statement.Payments[0].Problem);
        }

        // Paychecks only: a deposit row is paid by check.
        await using var harness = await PayRunTests.CreateHarnessAsync(services => AddPayments(services, context, deposits: false, checks: true));
        var second = await PayRunTests.SetUpAsync(
            harness,
            employerSetup: employer => employer.FundingBankAccountId = origin.ItemId,
            profileSetup: async profile =>
            {
                var payee = await context.PayeeAsync(staffId: profile.StaffMemberId);

                profile.Distribution = [new NetPayRow { Method = NetPayMethod.BankAccount, BankAccountId = payee.ItemId, Split = NetPaySplit.Remainder }];
            });
        await ApproveAsync(harness, second);

        var paid = await harness.RunAsync(async services => await services.GetRequiredService<IPayRunService>().ProcessAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(second.RunId), CalendarTestHarness.User("processor", PayrollPermissions.ProcessPayRuns.Name)));
        var payment = await harness.RunAsync(async services => Assert.Single(Assert.Single(await services.GetRequiredService<IPayRunService>().ListStatementsAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(second.RunId))).Payments), commit: false);

        Assert.True(paid.Succeeded, string.Join(" ", paid.Errors));
        Assert.Equal(NetPayMethod.Check, payment.Method);
        Assert.Empty(context.Store.Entries);
        Assert.Equal(payment.ReferenceId, Assert.Single(context.Checks.Store.Entries).ItemId);
    }

    [Fact]
    public async Task SourceAccess_LimitsPayStatementDisbursementsAndChecksToTheEmployersAUserMayProcess()
    {
        await using var harness = await PayRunTests.CreateHarnessAsync();
        var setup = await PayRunTests.SetUpAsync(harness);

        var (clerk, processor, viewerManaging) = await harness.RunAsync(async services =>
        {
            var access = services.GetServices<ISourceAccess>().Single(source => source.SourceKind == PayStatementSources.Kind);

            return (
                await access.GetScopeAsync(CalendarTestHarness.User("clerk", PayrollPermissionTemplates.For(PayrollPermissions.ViewPayRuns, setup.EmployerId).Name), SourceAccessLevel.View),
                await access.GetScopeAsync(CalendarTestHarness.User("processor", PayrollPermissions.ProcessPayRuns.Name), SourceAccessLevel.Manage),
                await access.GetScopeAsync(CalendarTestHarness.User("viewer", PayrollPermissions.ViewPayRuns.Name), SourceAccessLevel.Manage));
        }, commit: false);

        Assert.Equal(new[] { setup.EmployerId }, clerk.Scopes.ToArray());
        Assert.True(processor.IsAll);
        Assert.Empty(viewerManaging.Scopes);
        Assert.False(viewerManaging.IsAll);
    }

    [Fact]
    public async Task PaymentPages_FollowTheEmployersScope_AndTheStubIsAPdf()
    {
        var context = new DisbursementsTestContext();
        var origin = await context.Checks.BusinessAccountAsync();
        var observer = new RecordingStatementObserver();
        await using var harness = await PayRunTests.CreateHarnessAsync(services =>
        {
            AddPayments(services, context, deposits: false, checks: true);
            services.AddSingleton<IPayStatementObserver>(observer);
        });
        var setup = await PayRunTests.SetUpAsync(harness, employerSetup: employer => employer.FundingBankAccountId = origin.ItemId);
        var viewer = CalendarTestHarness.User("viewer", PayrollPermissionTemplates.For(PayrollPermissions.ViewPayRuns, setup.EmployerId).Name);
        var stranger = CalendarTestHarness.User("stranger", PayrollPermissionTemplates.For(PayrollPermissions.ProcessPayRuns, "fabrikam").Name, PayrollPermissionTemplates.For(PayrollPermissions.VoidPayStatements, "fabrikam").Name);
        var processor = CalendarTestHarness.User("processor", PayrollPermissionTemplates.For(PayrollPermissions.ProcessPayRuns, setup.EmployerId).Name);
        await ApproveAsync(harness, setup);

        var (viewerProcess, strangerProcess) = await harness.RunAsync(async services => (
            await PayRunTests.Controller(services, viewer).Process(setup.RunId),
            await PayRunTests.Controller(services, stranger).Process(setup.RunId)), commit: false);

        var processed = await harness.RunAsync(async services => await PayRunTests.Controller(services, processor).Process(setup.RunId));
        var statement = await harness.RunAsync(async services => Assert.Single(await services.GetRequiredService<IPayRunService>().ListStatementsAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId))), commit: false);

        var (viewerPdf, strangerPdf, viewerVoid, strangerVoid, strangerReissue, processorVoid) = await harness.RunAsync(async services =>
        {
            var statements = services.GetRequiredService<PayStatementStore>();
            var stubs = new CrestApps.OrchardCore.Payroll.Services.PayStubService(
                services.GetRequiredService<IPayCodeManager>(),
                services.GetRequiredService<IPayAccumulatorService>(),
                new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<CrestApps.OrchardCore.Payroll.Services.PayStubService>());

            return (
                await PayRunTests.Controller(services, viewer).StatementPdf(statement.ItemId, statements, stubs),
                await PayRunTests.Controller(services, stranger).StatementPdf(statement.ItemId, statements, stubs),
                await PayRunTests.Controller(services, viewer).VoidStatement(statement.ItemId, "Wrong", statements),
                await PayRunTests.Controller(services, stranger).VoidStatement(statement.ItemId, "Wrong", statements),
                await PayRunTests.Controller(services, stranger).ReissuePayment(statement.ItemId, 0, "Lost", false, statements),
                await PayRunTests.Controller(services, processor).VoidStatement(statement.ItemId, "Wrong", statements));
        }, commit: false);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(viewerProcess);
        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(strangerProcess);
        Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToActionResult>(processed);
        Assert.Equal(PayStatementStatus.Paid, statement.Status);
        Assert.Equal(new[] { statement.ItemId }, observer.Issued);
        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(strangerPdf);
        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(viewerVoid);
        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(strangerVoid);
        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(strangerReissue);

        // Processing does not let one void: that is its own permission.
        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(processorVoid);

        var file = Assert.IsType<Microsoft.AspNetCore.Mvc.FileContentResult>(viewerPdf);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal($"pay-statement-{statement.Number}.pdf", file.FileDownloadName);
        using var document = PdfDocument.Open(file.FileContents);
        var text = document.GetPage(1).Text;
        Assert.Contains("Net pay", text, StringComparison.Ordinal);
        Assert.Contains("Lee Park", text, StringComparison.Ordinal);
        Assert.Contains("Year to date", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reissue_VoidsTheLostCheck_AndPrintsANewOne()
    {
        var context = new DisbursementsTestContext();
        var origin = await context.Checks.BusinessAccountAsync();
        await using var harness = await PayRunTests.CreateHarnessAsync(services => AddPayments(services, context, deposits: false, checks: true));
        var setup = await PayRunTests.SetUpAsync(harness, employerSetup: employer => employer.FundingBankAccountId = origin.ItemId);
        var clerk = CalendarTestHarness.User("clerk", PayrollPermissions.ProcessPayRuns.Name, PayrollPermissions.VoidPayStatements.Name);
        await ApproveAsync(harness, setup);
        await harness.RunAsync(async services => Assert.True((await services.GetRequiredService<IPayRunService>().ProcessAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId), clerk)).Succeeded));

        var (first, result) = await harness.RunAsync(async services =>
        {
            var statement = Assert.Single(await services.GetRequiredService<IPayRunService>().ListStatementsAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId)));
            var reference = statement.Payments[0].ReferenceId;

            return (reference, await services.GetRequiredService<IPayRunService>().ReissuePaymentAsync(statement, 0, clerk, "Lost in the mail"));
        });

        var payment = await harness.RunAsync(async services => Assert.Single(await services.GetRequiredService<IPayRunService>().ListStatementsAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId))).Payments[0], commit: false);

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Equal(1, payment.Attempt);
        Assert.NotEqual(first, payment.ReferenceId);
        Assert.Equal(CheckStatus.Voided, context.Checks.Store.Entries.Single(check => check.ItemId == first).Status);
        Assert.Equal(CheckStatus.Issued, context.Checks.Store.Entries.Single(check => check.ItemId == payment.ReferenceId).Status);
    }

    [Fact]
    public async Task Process_WhenAPaymentCommitsTheSession_SavesEachRecordOnce()
    {
        await using var harness = await PayRunTests.CreateHarnessAsync(services =>
        {
            services.AddScoped<IPayStatementPaymentMethod>(provider => new CommittingPaymentMethod(NetPayMethod.Check, provider.GetRequiredService<global::YesSql.ISession>()));
            services.AddScoped<IPayStatementPaymentMethod>(provider => new CommittingPaymentMethod(NetPayMethod.BankAccount, provider.GetRequiredService<global::YesSql.ISession>()));
        });
        var setup = await PayRunTests.SetUpAsync(harness, profileSetup: profile =>
        {
            profile.Distribution =
            [
                new NetPayRow { Method = NetPayMethod.BankAccount, BankAccountId = "account-1", Split = NetPaySplit.Amount, Amount = 500 },
                new NetPayRow { Method = NetPayMethod.Check, Split = NetPaySplit.Remainder },
            ];

            return Task.CompletedTask;
        });
        var processor = CalendarTestHarness.User("processor", PayrollPermissions.ProcessPayRuns.Name, PayrollPermissions.VoidPayStatements.Name);
        await ApproveAsync(harness, setup);

        var processed = await harness.RunAsync(async services => await services.GetRequiredService<IPayRunService>().ProcessAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId), processor));
        var statementId = await harness.RunAsync(async services => Assert.Single(await services.GetRequiredService<IPayRunService>().ListStatementsAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId))).ItemId, commit: false);
        var reissued = await harness.RunAsync(async services => await services.GetRequiredService<IPayRunService>().ReissuePaymentAsync(await services.GetRequiredService<PayStatementStore>().FindByIdAsync(statementId), 1, processor, "Lost"));
        var voided = await harness.RunAsync(async services => await services.GetRequiredService<IPayRunService>().VoidStatementAsync(await services.GetRequiredService<PayStatementStore>().FindByIdAsync(statementId), processor, "Wrong hours"));

        var (runRows, statementRows, run, original) = await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<global::YesSql.ISession>();

            return (
                await session.Query<PayRun, CrestApps.OrchardCore.Payroll.Core.Services.PayRunIndex>(index => index.ItemId == setup.RunId, collection: PayrollConstants.CollectionName).CountAsync(),
                await session.Query<PayStatement, CrestApps.OrchardCore.Payroll.Core.Indexes.PayStatementIndex>(index => index.RunId == setup.RunId, collection: PayrollConstants.CollectionName).CountAsync(),
                await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId),
                await services.GetRequiredService<PayStatementStore>().FindByIdAsync(statementId));
        }, commit: false);

        Assert.True(processed.Succeeded, string.Join(" ", processed.Errors));
        Assert.True(reissued.Succeeded, string.Join(" ", reissued.Errors));
        Assert.True(voided.Succeeded, string.Join(" ", voided.Errors));
        Assert.Equal(1, runRows);

        // The statement and its reversal: two rows, never a copy of either.
        Assert.Equal(2, statementRows);
        Assert.Equal(PayRunStatus.Paid, run.Status);
        Assert.Equal(PayStatementStatus.Voided, original.Status);
        Assert.Equal(new[] { "BankAccount-1", "Check-3" }, original.Payments.Select(payment => payment.ReferenceId));
        Assert.Equal(1, original.Payments[1].Attempt);
        Assert.False(string.IsNullOrEmpty(original.Number));
    }

    [Fact]
    public async Task FundingAccount_IsOneOfTheBusinesssOwnAccounts_TheOnesDisbursementsAndChecksPayFrom()
    {
        var banking = new CrestApps.OrchardCore.Tests.Banking.BankingTestContext();
        var business = await banking.CreateAsync("900000001", name: "Payroll funding");
        var staffOwned = await banking.CreateAsync("123456789", new CrestApps.OrchardCore.Parties.Models.PartyRef("Staff", "lee"), "Lee pay account");
        var employerOwned = await banking.CreateAsync("555000111", new CrestApps.OrchardCore.Parties.Models.PartyRef(PayrollConstants.EmployerPartyKind, "contoso"), "Old style");
        var driver = new CrestApps.OrchardCore.Payroll.Drivers.EmployerFundingDisplayDriver(banking.Manager, new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<CrestApps.OrchardCore.Payroll.Drivers.EmployerFundingDisplayDriver>());
        var handler = new CrestApps.OrchardCore.Payroll.Handlers.EmployerFundingHandler(banking.Manager, new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<CrestApps.OrchardCore.Payroll.Handlers.EmployerFundingHandler>());

        // The editor takes the choice; the employer's handler decides, on every save, the editor's and a recipe's alike.
        async Task<(int Errors, string Funding)> ChooseAsync(string accountId)
        {
            var employer = new Employer { ItemId = "contoso", Name = "Contoso" };
            var updater = new CrestApps.OrchardCore.Tests.Taxation.Fakes.FakeUpdateModel(new CrestApps.OrchardCore.Payroll.ViewModels.EmployerFundingViewModel { FundingBankAccountId = accountId });

            await driver.UpdateAsync(employer, new global::OrchardCore.DisplayManagement.Handlers.UpdateEditorContext(null, string.Empty, false, string.Empty, null, null, updater));

            var validation = new CrestApps.Core.Models.ValidatingContext<Employer>(employer);

            await handler.ValidatingAsync(validation, TestContext.Current.CancellationToken);

            return (updater.ModelState.ErrorCount + validation.Result.Errors.Count, employer.FundingBankAccountId);
        }

        Assert.Equal((0, business.ItemId), await ChooseAsync(business.ItemId));
        Assert.Equal(1, (await ChooseAsync(staffOwned.ItemId)).Errors);
        Assert.Equal(1, (await ChooseAsync(employerOwned.ItemId)).Errors);
        Assert.Equal(1, (await ChooseAsync("no-such-account")).Errors);
        Assert.Equal((0, (string)null), await ChooseAsync(string.Empty));
    }

    [Fact]
    public async Task Notice_TellsThePersonTheirStatementIsReady_WithoutAnAmount_AndIsRemovedOnVoid()
    {
        var sent = new List<(object To, global::OrchardCore.Notifications.INotificationMessage Message)>();
        var deleted = new List<global::OrchardCore.Notifications.NotificationFilter>();
        var notifications = new Moq.Mock<global::OrchardCore.Notifications.INotificationService>();
        var manager = new Moq.Mock<global::OrchardCore.Notifications.INotificationManager>();
        var lee = new global::OrchardCore.Users.Models.User { UserId = "lee-user", UserName = "lee" };
        var users = new Moq.Mock<Microsoft.AspNetCore.Identity.UserManager<global::OrchardCore.Users.IUser>>(Moq.Mock.Of<Microsoft.AspNetCore.Identity.IUserStore<global::OrchardCore.Users.IUser>>(), null, null, null, null, null, null, null, null);

        notifications
            .Setup(service => service.SendAsync(Moq.It.IsAny<object>(), Moq.It.IsAny<global::OrchardCore.Notifications.INotificationMessage>(), Moq.It.IsAny<CancellationToken>()))
            .Callback<object, global::OrchardCore.Notifications.INotificationMessage, CancellationToken>((to, message, _) => sent.Add((to, message)))
            .ReturnsAsync(new global::OrchardCore.Notifications.NotificationSendResult());
        manager
            .Setup(service => service.DeleteAsync(Moq.It.IsAny<global::OrchardCore.Notifications.NotificationFilter>(), Moq.It.IsAny<CancellationToken>()))
            .Callback<global::OrchardCore.Notifications.NotificationFilter, CancellationToken>((filter, _) => deleted.Add(filter))
            .ReturnsAsync(1);
        users.Setup(manager => manager.FindByIdAsync("lee-user")).ReturnsAsync(lee);

        await using var harness = await PayRunTests.CreateHarnessAsync(services =>
        {
            services.AddSingleton(notifications.Object);
            services.AddSingleton(manager.Object);
            services.AddSingleton(users.Object);
            services.AddScoped<IPayStatementObserver, PayStatementNotificationObserver>();
        });
        var setup = await PayRunTests.SetUpAsync(harness);
        await harness.RunAsync(async services =>
        {
            var staff = services.GetRequiredService<CrestApps.OrchardCore.Staff.Services.IStaffMemberManager>();
            var member = await staff.FindByIdAsync(setup.StaffMemberId);

            member.UserId = "lee-user";
            await staff.UpdateAsync(member);
        });
        var processor = CalendarTestHarness.User("processor", PayrollPermissions.ProcessPayRuns.Name, PayrollPermissions.VoidPayStatements.Name);
        await ApproveAsync(harness, setup);

        await harness.RunAsync(async services => await services.GetRequiredService<IPayRunService>().ProcessAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId), processor));
        var statement = await harness.RunAsync(async services => Assert.Single(await services.GetRequiredService<IPayRunService>().ListStatementsAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId))), commit: false);
        await harness.RunAsync(async services => await services.GetRequiredService<IPayRunService>().VoidStatementAsync(await services.GetRequiredService<PayStatementStore>().FindByIdAsync(statement.ItemId), processor, "Wrong hours"));

        var (to, message) = Assert.Single(sent);
        Assert.Same(lee, to);
        Assert.Equal(PayrollConstants.PayStatementReadyNotificationType, message.Type);
        Assert.Equal(statement.ItemId, message.ReferenceId);
        Assert.Equal("~/Admin/my-pay/statements/" + statement.ItemId, message.Url);

        // No amount in a notice: it may be sent by e-mail or SMS.
        Assert.DoesNotContain(statement.Net.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), message.Subject + message.Summary + message.TextBody, StringComparison.Ordinal);
        Assert.Equal((PayrollConstants.PayStatementReadyNotificationType, statement.ItemId), (Assert.Single(deleted).Type, deleted[0].ReferenceId));
    }

    [Fact]
    public async Task RunObservers_HearEachStepOfARun_InOrder_WithWhoDidIt()
    {
        var context = new DisbursementsTestContext();
        var origin = await context.Checks.BusinessAccountAsync();
        var observer = new RecordingRunObserver();
        await using var harness = await PayRunTests.CreateHarnessAsync(services =>
        {
            AddPayments(services, context, deposits: false, checks: true);
            services.AddSingleton<IPayRunObserver>(observer);
        });
        var setup = await PayRunTests.SetUpAsync(harness, employerSetup: employer => employer.FundingBankAccountId = origin.ItemId);
        var preparer = CalendarTestHarness.User("preparer", PayrollPermissions.PreparePayRuns.Name);

        var opened = await harness.RunAsync(async services =>
        {
            var runs = services.GetRequiredService<IPayRunManager>();
            var run = await runs.NewAsync();
            var existing = await runs.FindByIdAsync(setup.RunId);

            run.EmployerId = existing.EmployerId;
            run.PayScheduleId = existing.PayScheduleId;
            run.PeriodStart = existing.PeriodStart;
            run.PeriodEnd = existing.PeriodEnd;
            run.PayDate = existing.PayDate;

            return await services.GetRequiredService<IPayRunService>().OpenAsync(run, preparer);
        });

        await ApproveAsync(harness, setup);
        await harness.RunAsync(async services => Assert.True((await services.GetRequiredService<IPayRunService>().ProcessAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId), CalendarTestHarness.User("processor", PayrollPermissions.ProcessPayRuns.Name))).Succeeded));

        Assert.True(opened.Succeeded, string.Join(" ", opened.Errors));
        Assert.Equal(
            new[] { PayRunChangeKind.Opened, PayRunChangeKind.Calculated, PayRunChangeKind.Submitted, PayRunChangeKind.Approved, PayRunChangeKind.Paid },
            observer.Changes.Select(change => change.Kind));
        Assert.Equal(new[] { "preparer", "preparer", "preparer", "approver", "processor" }, observer.Changes.Select(change => change.Actor));
        Assert.All(observer.Changes.Skip(1), change => Assert.Equal(setup.RunId, change.Run.ItemId));
    }

    [Fact]
    public async Task DoubleClicks_ProcessPaysOnce_VoidReversesOnce_AndAProcessedRunIsNotCalculatedAgain()
    {
        var context = new DisbursementsTestContext();
        var origin = await context.Checks.BusinessAccountAsync();
        await using var harness = await PayRunTests.CreateHarnessAsync(services => AddPayments(services, context, deposits: false, checks: true));
        var setup = await PayRunTests.SetUpAsync(harness, employerSetup: employer => employer.FundingBankAccountId = origin.ItemId);
        var clerk = CalendarTestHarness.User("clerk", PayrollPermissions.ProcessPayRuns.Name, PayrollPermissions.VoidPayStatements.Name, PayrollPermissions.PreparePayRuns.Name);
        await ApproveAsync(harness, setup);

        // The copy a second click holds: read before the first click processed the run.
        var stale = await harness.RunAsync(services => services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId).AsTask(), commit: false);

        Assert.Equal(PayRunStatus.Approved, stale.Status);
        await harness.RunAsync(async services => Assert.True((await services.GetRequiredService<IPayRunService>().ProcessAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId), clerk)).Succeeded));

        var (again, recalculated) = await harness.RunAsync(async services => (
            await services.GetRequiredService<IPayRunService>().ProcessAsync(stale, clerk),
            await services.GetRequiredService<IPayRunService>().CalculateAsync(stale, clerk)));

        Assert.False(again.Succeeded);
        Assert.False(recalculated.Succeeded);
        Assert.Single(context.Checks.Store.Entries);

        var (run, statements) = await harness.RunAsync(async services =>
        {
            var current = await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId);

            return (current, await services.GetRequiredService<IPayRunService>().ListStatementsAsync(current));
        }, commit: false);

        Assert.Equal(PayRunStatus.Paid, run.Status);
        Assert.Equal(PayStatementStatus.Paid, Assert.Single(statements).Status);

        // Void twice with the copy the second click holds: one reversal.
        var original = statements[0];
        var (firstVoid, secondVoid) = await harness.RunAsync(async services =>
        {
            var service = services.GetRequiredService<IPayRunService>();
            var first = await service.VoidStatementAsync(original, clerk, "Wrong hours");

            return (first, await service.VoidStatementAsync(original, clerk, "Wrong hours"));
        });

        Assert.True(firstVoid.Succeeded, string.Join(" ", firstVoid.Errors));
        Assert.False(secondVoid.Succeeded);

        var afterVoid = await harness.RunAsync(async services => await services.GetRequiredService<IPayRunService>().ListStatementsAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId)), commit: false);

        Assert.Single(afterVoid, statement => statement.VoidOf == original.ItemId);
        Assert.Equal(PayStatementStatus.Voided, afterVoid.Single(statement => statement.ItemId == original.ItemId).Status);
    }

    [Fact]
    public async Task AReturnedDeposit_IsReissued_AgainOrByCheck_AndAnAccountNoLongerVerifiedIsPaidByCheck()
    {
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        await using var harness = await PayRunTests.CreateHarnessAsync(services => AddPayments(services, context, deposits: true, checks: true));
        string payeeId = null;
        var setup = await PayRunTests.SetUpAsync(
            harness,
            employerSetup: employer => employer.FundingBankAccountId = origin.ItemId,
            profileSetup: async profile =>
            {
                var payee = await context.PayeeAsync(staffId: profile.StaffMemberId);

                payeeId = payee.ItemId;
                profile.Distribution = [new NetPayRow { Method = NetPayMethod.BankAccount, BankAccountId = payee.ItemId, Split = NetPaySplit.Remainder }];
            });
        var clerk = CalendarTestHarness.User("clerk", PayrollPermissions.ProcessPayRuns.Name, PayrollPermissions.VoidPayStatements.Name);
        await ApproveAsync(harness, setup);
        await harness.RunAsync(async services => Assert.True((await services.GetRequiredService<IPayRunService>().ProcessAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId), clerk)).Succeeded));

        // The bank returned the deposit (R03, no account).
        var deposit = Assert.Single(context.Store.Entries);

        deposit.Status = DisbursementStatus.Returned;
        deposit.FailureCode = "R03";
        await context.Store.UpdateAsync(deposit, TestContext.Current.CancellationToken);

        Task<PayStatement> StatementAsync()
            => harness.RunAsync(async services => Assert.Single(await services.GetRequiredService<IPayRunService>().ListStatementsAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId))), commit: false);

        var again = await harness.RunAsync(async services => await services.GetRequiredService<IPayRunService>().ReissuePaymentAsync(await StatementAsync(), 0, clerk, "Returned R03"));
        var afterDeposit = await StatementAsync();

        Assert.True(again.Succeeded, string.Join(" ", again.Errors));
        Assert.Equal(2, context.Store.Entries.Count);
        Assert.Equal((1, NetPayMethod.BankAccount), (afterDeposit.Payments[0].Attempt, afterDeposit.Payments[0].Method));

        // Returned again: this time by check.
        var second = context.Store.Entries.Single(entry => entry.ItemId == afterDeposit.Payments[0].ReferenceId);

        second.Status = DisbursementStatus.Returned;
        await context.Store.UpdateAsync(second, TestContext.Current.CancellationToken);

        var byCheck = await harness.RunAsync(async services => await services.GetRequiredService<IPayRunService>().ReissuePaymentAsync(await StatementAsync(), 0, clerk, "Returned again", NetPayMethod.Check));
        var afterCheck = await StatementAsync();

        Assert.True(byCheck.Succeeded, string.Join(" ", byCheck.Errors));
        Assert.Equal(NetPayMethod.Check, afterCheck.Payments[0].Method);
        Assert.Equal(afterCheck.Payments[0].ReferenceId, Assert.Single(context.Checks.Store.Entries).ItemId);

        // An account that is no longer verified when a later reissue pays it is paid by check.
        var account = await context.Banking.Manager.FindByIdAsync(payeeId, TestContext.Current.CancellationToken);

        account.Status = CrestApps.OrchardCore.Banking.Models.BankAccountStatus.Unverified;
        await context.Banking.Manager.UpdateAsync(account, cancellationToken: TestContext.Current.CancellationToken);

        var check = context.Checks.Store.Entries.Single();
        var lost = await harness.RunAsync(async services => await services.GetRequiredService<IPayRunService>().ReissuePaymentAsync(await StatementAsync(), 0, clerk, "Lost", NetPayMethod.BankAccount));
        var afterLost = await StatementAsync();

        Assert.True(lost.Succeeded, string.Join(" ", lost.Errors));
        Assert.Equal(NetPayMethod.Check, afterLost.Payments[0].Method);
        Assert.Equal(2, context.Store.Entries.Count);
        Assert.Equal(2, context.Checks.Store.Entries.Count);
    }

    internal static void AddPayments(IServiceCollection services, DisbursementsTestContext context, bool deposits, bool checks)
    {
        services.AddSingleton<IDisbursementService>(context.Service);
        services.AddSingleton<IDisbursementManager>(context.Manager);
        services.AddSingleton<ICheckService>(context.Checks.Service);
        services.AddSingleton<ICheckManager>(context.Checks.Manager);
        services.AddSingleton<CrestApps.OrchardCore.Banking.Services.IBankAccountManager>(context.Banking.Manager);

        if (deposits)
        {
            services.AddPayrollDirectDepositCore();
        }

        if (checks)
        {
            services.AddPayrollPaychecksCore();
        }
    }

    internal static async Task ApproveAsync(CalendarTestHarness harness, PayRunTests.Setup setup)
    {
        var preparer = CalendarTestHarness.User("preparer", PayrollPermissions.PreparePayRuns.Name);
        var approver = CalendarTestHarness.User("approver", PayrollPermissions.ApprovePayRuns.Name);

        await harness.RunAsync(async services =>
        {
            var service = services.GetRequiredService<IPayRunService>();
            var run = await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId);

            Assert.True((await service.CalculateAsync(run, preparer)).Succeeded);
            Assert.True((await service.SubmitAsync(run, preparer)).Succeeded);
        });

        await harness.RunAsync(async services =>
        {
            var run = await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId);
            var request = await services.GetRequiredService<IApprovalRequestManager>().FindByIdAsync(run.ApprovalRequestId);
            var decision = await services.GetRequiredService<IApprovalService>().DecideAsync(request, approver, ApprovalDecisionKind.Approve, null);

            Assert.True(decision.Succeeded, string.Join(" ", decision.Errors));
        });

        var (runs, status) = await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<global::YesSql.ISession>();
            var count = await session.Query<PayRun, CrestApps.OrchardCore.Payroll.Core.Services.PayRunIndex>(index => index.ItemId == setup.RunId, collection: PayrollConstants.CollectionName).CountAsync();

            return (count, (await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId)).Status);
        }, commit: false);

        Assert.Equal(1, runs);
        Assert.Equal(PayRunStatus.Approved, status);
    }

    /// <summary>
    /// Pays by committing the session first, as Disbursements and Checks do, and numbers what it pays.
    /// </summary>
    internal sealed class CommittingPaymentMethod : IPayStatementPaymentMethod
    {
        private readonly global::YesSql.ISession _session;
        public CommittingPaymentMethod(NetPayMethod method, global::YesSql.ISession session)
        {
            Method = method;
            _session = session;
        }

        public NetPayMethod Method { get; }

        public async Task<PayStatementPaymentResult> PayAsync(PayStatementPaymentContext context, CancellationToken cancellationToken = default)
        {
            await _session.SaveChangesAsync(cancellationToken);

            return new PayStatementPaymentResult { ReferenceId = $"{Method}-{context.Index + context.Payment.Attempt + 1}", Reference = Method.ToString() };
        }

        public async Task<bool> VoidAsync(PayStatementPaymentContext context, string reason, CancellationToken cancellationToken = default)
        {
            await _session.SaveChangesAsync(cancellationToken);

            return true;
        }
    }

    /// <summary>
    /// Records the changes of pay runs.
    /// </summary>
    internal sealed class RecordingRunObserver : IPayRunObserver
    {
        public List<PayRunChange> Changes { get; } = [];

        public Task ChangedAsync(PayRunChange change, CancellationToken cancellationToken = default)
        {
            Changes.Add(change);

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Records the statements issued and voided.
    /// </summary>
    internal sealed class RecordingStatementObserver : IPayStatementObserver
    {
        public List<string> Issued { get; } = [];

        public List<string> Voided { get; } = [];

        public Task IssuedAsync(PayStatement statement, CancellationToken cancellationToken = default)
        {
            Issued.Add(statement.ItemId);

            return Task.CompletedTask;
        }

        public Task VoidedAsync(PayStatement statement, PayStatement reversal, CancellationToken cancellationToken = default)
        {
            Voided.Add(statement.ItemId);

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// A source of pay inputs that records what was taken and given back.
    /// </summary>
    internal sealed class RecordingPayInputSource : IPayInputSource
    {
        public const string Kind = "Sheet";

        public List<PayInput> Inputs { get; } = [];

        public List<(string ConsumerId, string SourceId)> Consumed { get; } = [];

        public List<(string ConsumerId, string SourceId)> Released { get; } = [];

        public string SourceKind => Kind;

        public Task<IReadOnlyList<PayInput>> GetInputsAsync(PayInputQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PayInput>>([.. Inputs.Where(input => (query.StaffMemberIds.Count == 0 || query.StaffMemberIds.Contains(input.StaffMemberId)) && (query.IncludeConsumed || !Consumed.Any(taken => taken.SourceId == input.SourceId)))]);

        public Task MarkConsumedAsync(string consumerId, IReadOnlyCollection<string> sourceIds, CancellationToken cancellationToken = default)
        {
            Consumed.AddRange(sourceIds.Select(id => (consumerId, id)));

            return Task.CompletedTask;
        }

        public Task ReleaseAsync(string consumerId, IReadOnlyCollection<string> sourceIds, CancellationToken cancellationToken = default)
        {
            Released.AddRange(sourceIds.Select(id => (consumerId, id)));

            return Task.CompletedTask;
        }
    }
}
