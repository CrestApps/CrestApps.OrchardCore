using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Checks.Models;
using CrestApps.OrchardCore.Checks.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.DataSources;
using CrestApps.OrchardCore.Payroll.Handlers;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Disbursements;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using OrchardCore.DataSources;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// The fixes of the payroll review: net pay only to the person's own accounts, garnishment payments with their own
/// voucher, capped at the balance, given back on a void only once stopped and sent again after a problem, the support
/// and levy rules, the case number only for who manages garnishments, and observers that run for every final statement.
/// </summary>
public sealed class PayrollReviewFixesTests
{
    [Fact]
    public async Task NetPay_GoesOnlyToThePersonsOwnAccounts_OnEverySave_AndAgainAtPayment()
    {
        var context = new DisbursementsTestContext();
        var origin = await context.OriginAsync();
        await using var harness = await PayRunTests.CreateHarnessAsync(services =>
        {
            PayRunProcessingTests.AddPayments(services, context, deposits: true, checks: false);
            services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<EmployeePayProfile>, PayProfileAccountHandler>());
        });
        string ownId = null;
        var setup = await PayRunTests.SetUpAsync(
            harness,
            employerSetup: employer => employer.FundingBankAccountId = origin.ItemId,
            profileSetup: async profile =>
            {
                ownId = (await context.PayeeAsync(staffId: profile.StaffMemberId)).ItemId;
                profile.Distribution = [new NetPayRow { Method = NetPayMethod.BankAccount, BankAccountId = ownId, Split = NetPaySplit.Remainder }];
            });

        var clerks = await context.PayeeAsync("222222222", staffId: "clerk");
        var vendor = await context.Banking.CreateAsync("333333333", new PartyRef("Vendor", "v1"), "Vendor account");
        var users = await context.Banking.CreateAsync("444444444", new PartyRef(PartyKinds.User, "lee-user"), "Lee's own");

        var (toClerk, toVendor, toOwnUser) = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IPayProfileManager>();
            var profile = (await manager.ListByEmployersAsync([setup.EmployerId])).Single();
            var staff = services.GetRequiredService<CrestApps.OrchardCore.Staff.Services.IStaffMemberManager>();
            var member = await staff.FindByIdAsync(setup.StaffMemberId);

            member.UserId = "lee-user";
            await staff.UpdateAsync(member);

            async Task<bool> SavesAsync(string accountId)
            {
                profile.Distribution = [new NetPayRow { Method = NetPayMethod.BankAccount, BankAccountId = accountId, Split = NetPaySplit.Remainder }];

                return (await manager.ValidateAsync(profile)).Succeeded;
            }

            (bool Clerk, bool Vendor, bool OwnUser) result = (await SavesAsync(clerks.ItemId), await SavesAsync(vendor.ItemId), await SavesAsync(users.ItemId));

            profile.Distribution = [new NetPayRow { Method = NetPayMethod.BankAccount, BankAccountId = ownId, Split = NetPaySplit.Remainder }];

            return result;
        });

        Assert.False(toClerk);
        Assert.False(toVendor);
        Assert.True(toOwnUser);

        // The account changed hands after the run was approved: no money goes to it.
        await PayRunProcessingTests.ApproveAsync(harness, setup);

        var own = await context.Banking.Manager.FindByIdAsync(ownId, TestContext.Current.CancellationToken);

        own.Owner = new PartyRef("Staff", "clerk");
        await context.Banking.Manager.UpdateAsync(own, cancellationToken: TestContext.Current.CancellationToken);

        var clerk = CalendarTestHarness.User("clerk", PayrollPermissions.ProcessPayRuns.Name);
        await harness.RunAsync(async services => await services.GetRequiredService<IPayRunService>().ProcessAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId), clerk));

        var statement = await harness.RunAsync(async services => Assert.Single(await services.GetRequiredService<IPayRunService>().ListStatementsAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId))), commit: false);

        Assert.Empty(context.Store.Entries);
        Assert.Contains("person's own", statement.Payments[0].Problem, StringComparison.Ordinal);
        Assert.Null(statement.Payments[0].ReferenceId);
    }

    [Fact]
    public async Task GarnishmentOrder_RefusesAPersonalPayeeAccount_ALevyWithoutAnExemptAmount_AndSupportAboveTheFederalLimit()
    {
        var context = new DisbursementsTestContext();
        await using var harness = await PayRunTests.CreateHarnessAsync(services =>
        {
            services.AddGarnishmentsCore();
            services.AddSingleton<CrestApps.OrchardCore.Banking.Services.IBankAccountManager>(context.Banking.Manager);
        });
        var setup = await PayRunTests.SetUpAsync(harness);
        var personal = await context.Banking.CreateAsync("555555555", new PartyRef(PartyKinds.User, "someone"), "Personal");
        var otherStaff = await context.PayeeAsync("666666666", staffId: "another");
        var agency = await context.Banking.CreateAsync("777777777", new PartyRef("Vendor", "sdu"), "State disbursement unit");

        var results = await harness.RunAsync(async services =>
        {
            await services.GetRequiredService<GarnishmentPayCodeSeeder>().SeedAsync();

            var manager = services.GetRequiredService<IGarnishmentOrderManager>();
            var profile = (await services.GetRequiredService<IPayProfileManager>().ListByEmployersAsync([setup.EmployerId])).Single();
            var code = (await services.GetRequiredService<IPayCodeManager>().FindByCodeAsync(GarnishmentPayCodeSeeder.Code)).ItemId;

            GarnishmentOrder Order(GarnishmentType type, Action<GarnishmentOrder> change)
            {
                var order = new GarnishmentOrder
                {
                    ProfileId = profile.ItemId,
                    Type = type,
                    PayCodeId = code,
                    Amount = 100,
                    StartDate = new DateOnly(2026, 1, 1),
                    PayeeName = "Payee",
                    PaymentMethod = GarnishmentPaymentMethod.Outside,
                };

                change(order);
                GarnishmentOrderHandler.Defaults(order);

                return order;
            }

            async Task<bool> ValidAsync(GarnishmentOrder order)
                => (await manager.ValidateAsync(order)).Succeeded;

            return new[]
            {
                await ValidAsync(Order(GarnishmentType.Creditor, order => (order.PaymentMethod, order.PayeeBankAccountId) = (GarnishmentPaymentMethod.BankAccount, personal.ItemId))),
                await ValidAsync(Order(GarnishmentType.Creditor, order => (order.PaymentMethod, order.PayeeBankAccountId) = (GarnishmentPaymentMethod.BankAccount, otherStaff.ItemId))),
                await ValidAsync(Order(GarnishmentType.Creditor, order => (order.PaymentMethod, order.PayeeBankAccountId) = (GarnishmentPaymentMethod.BankAccount, agency.ItemId))),
                await ValidAsync(Order(GarnishmentType.FederalTaxLevy, order => { })),
                await ValidAsync(Order(GarnishmentType.FederalTaxLevy, order => order.ExemptAmount = 600)),
                await ValidAsync(Order(GarnishmentType.Support, order => (order.OtherDependents, order.Percent) = (true, 55))),
                await ValidAsync(Order(GarnishmentType.Support, order => (order.OtherDependents, order.ArrearsOver12Weeks, order.Percent) = (true, true, 55))),
                await ValidAsync(Order(GarnishmentType.Support, order => order.LimitPercent = 70)),
                await ValidAsync(Order(GarnishmentType.Support, order => order.LimitPercent = 40)),
            };
        }, commit: false);

        Assert.Equal([false, false, true, false, true, false, true, false, true], results);

        // The limit follows the answers: 50, 55, 60 or 65.
        Assert.Equal(
            [50m, 55m, 60m, 65m],
            new[] { (true, false), (true, true), (false, false), (false, true) }.Select(answers => GarnishmentDefaults.SupportLimitPercent(answers.Item1, answers.Item2)));
    }

    [Fact]
    public async Task Garnishment_IsCappedAtTheBalance_AndAVoidGivesItBackOnlyOnceThePaymentIsStopped()
    {
        await using var harness = await PayRunTests.CreateHarnessAsync(services => services.AddGarnishmentsCore());
        var setup = await PayRunTests.SetUpAsync(harness);

        var orderId = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IGarnishmentOrderManager>();
            var order = await manager.NewAsync();

            order.ProfileId = "p";
            order.EmployerId = setup.EmployerId;
            order.StaffMemberId = setup.StaffMemberId;
            order.Type = GarnishmentType.Creditor;
            order.TotalOwed = 400;
            order.Withheld = 250;
            order.PaymentMethod = GarnishmentPaymentMethod.Outside;
            await manager.CreateAsync(order);

            return order.ItemId;
        });

        PayStatement Statement(string id, decimal amount)
            => new()
            {
                ItemId = id,
                Number = id,
                EmployerId = setup.EmployerId,
                StaffMemberId = setup.StaffMemberId,
                PayDate = new DateOnly(2026, 11, 20),
                Lines = [new PayStatementLine { Type = PayLineType.Deduction, GarnishmentOrderId = orderId, Amount = amount }],
            };

        // A second run calculated against the same balance withheld 300: only the 150 still owed goes to the payee.
        await harness.RunAsync(async services => await services.GetRequiredService<GarnishmentStatementObserver>().IssuedAsync(Statement("s2", 300)));

        var capped = await harness.RunAsync(async services => await services.GetRequiredService<IGarnishmentOrderManager>().FindByIdAsync(orderId), commit: false);
        var row = Assert.Single(capped.Remittances);

        Assert.Equal((400m, GarnishmentStatus.Completed, 150m), (capped.Withheld, capped.Status, row.Amount));
        Assert.Contains("150.00 more was withheld", row.Problem, StringComparison.Ordinal);

        // A payment that can't be stopped keeps the withholding on the order, flagged.
        await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IGarnishmentOrderManager>();
            var order = await manager.FindByIdAsync(orderId);

            order.Remittances[0].Method = GarnishmentPaymentMethod.Check;
            order.Remittances[0].ReferenceId = "a-check-that-cleared";
            await manager.UpdateAsync(order);
        });

        await harness.RunAsync(async services => await services.GetRequiredService<GarnishmentStatementObserver>().VoidedAsync(Statement("s2", 300), null));

        var kept = await harness.RunAsync(async services => await services.GetRequiredService<IGarnishmentOrderManager>().FindByIdAsync(orderId), commit: false);

        Assert.Equal(400m, kept.Withheld);
        Assert.False(kept.Remittances[0].Voided);
        Assert.Contains("could not be stopped", kept.Remittances[0].Problem, StringComparison.Ordinal);

        // With no payment out, a void gives back what was sent.
        await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IGarnishmentOrderManager>();
            var order = await manager.FindByIdAsync(orderId);

            order.Remittances[0].Method = GarnishmentPaymentMethod.Outside;
            order.Remittances[0].ReferenceId = null;
            await manager.UpdateAsync(order);
        });

        await harness.RunAsync(async services => await services.GetRequiredService<GarnishmentStatementObserver>().VoidedAsync(Statement("s2", 300), null));

        var given = await harness.RunAsync(async services => await services.GetRequiredService<IGarnishmentOrderManager>().FindByIdAsync(orderId), commit: false);

        Assert.Equal((250m, GarnishmentStatus.Active, true), (given.Withheld, given.Status, given.Remittances[0].Voided));
    }

    [Fact]
    public async Task PayWhatIsLeft_RunsTheObserversOfAFinalStatement_ACheckToThePayeeShowsOnlyTheOrder_AndAFailedOneIsSentAgain()
    {
        var payments = new DisbursementsTestContext();
        var origin = await payments.OriginAsync();
        await using var harness = await PayRunTests.CreateHarnessAsync(services =>
        {
            PayRunProcessingTests.AddPayments(services, payments, deposits: false, checks: true);
            services.AddGarnishmentsCore();
        });

        // No funding account yet: the first try pays nothing.
        var setup = await PayRunTests.SetUpAsync(harness);
        var orderId = await harness.RunAsync(async services =>
        {
            await services.GetRequiredService<GarnishmentPayCodeSeeder>().SeedAsync();

            var profile = (await services.GetRequiredService<IPayProfileManager>().ListByEmployersAsync([setup.EmployerId])).Single();
            var manager = services.GetRequiredService<IGarnishmentOrderManager>();
            var order = await manager.NewAsync();

            order.ProfileId = profile.ItemId;
            order.Type = GarnishmentType.Support;
            order.CaseNumber = "CS-3003";
            order.PayCodeId = (await services.GetRequiredService<IPayCodeManager>().FindByCodeAsync(GarnishmentPayCodeSeeder.Code)).ItemId;
            order.Amount = 400;
            order.StartDate = new DateOnly(2026, 1, 1);
            order.PayeeName = "State disbursement unit";
            order.PayeeAddress = new CrestApps.OrchardCore.Addresses.Models.Address { AddressLine1 = "1 Main St", City = "Springfield", Region = "IL", PostalCode = "62701", Country = "US" };
            order.PaymentMethod = GarnishmentPaymentMethod.Check;
            GarnishmentOrderHandler.Defaults(order);
            await manager.CreateAsync(order);

            return order.ItemId;
        });

        await PayRunProcessingTests.ApproveAsync(harness, setup);

        var processor = CalendarTestHarness.User("processor", PayrollPermissions.ProcessPayRuns.Name);

        async Task ProcessAsync()
            => await harness.RunAsync(async services => await services.GetRequiredService<IPayRunService>().ProcessAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId), processor));

        await ProcessAsync();
        Assert.Empty(payments.Checks.Store.Entries);

        // The observers of the first try were lost (a crash after the statement became final).
        await harness.RunAsync(async services =>
        {
            var statements = services.GetRequiredService<PayStatementStore>();
            var statement = Assert.Single(await services.GetRequiredService<IPayRunService>().ListStatementsAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId)));

            statement.IssuedObserved = false;
            await statements.UpdateAsync(statement);

            var orders = services.GetRequiredService<IGarnishmentOrderManager>();
            var order = await orders.FindByIdAsync(orderId);

            order.Withheld = 0;
            order.Remittances.Clear();
            await orders.UpdateAsync(order);

            var employers = services.GetRequiredService<IEmployerManager>();
            var employer = await employers.FindByIdAsync(setup.EmployerId);

            employer.FundingBankAccountId = origin.ItemId;
            await employers.UpdateAsync(employer);
        });

        // Pay what is left: net pay and the garnishment the lost observers never sent.
        await ProcessAsync();

        var paid = await harness.RunAsync(async services => await services.GetRequiredService<IGarnishmentOrderManager>().FindByIdAsync(orderId), commit: false);
        var remittance = Assert.Single(paid.Remittances);
        var garnishmentCheck = payments.Checks.Store.Entries.Single(check => check.ItemId == remittance.ReferenceId);

        Assert.Equal(2, payments.Checks.Store.Entries.Count);
        Assert.Equal((400m, GarnishmentSources.Kind), (paid.Withheld, garnishmentCheck.SourceKind));

        // Its voucher shows the order, not the person's pay.
        var voucher = await harness.RunAsync(async services => await services.GetServices<ICheckVoucherSource>().Single(source => source.SourceKind == GarnishmentSources.Kind).GetVoucherAsync(garnishmentCheck), commit: false);
        var labels = voucher.Lines.Select(line => line.Label).ToList();

        Assert.Contains("Case number", labels);
        Assert.Contains("CS-3003", voucher.Lines.SelectMany(line => line.Values));
        Assert.DoesNotContain("Gross pay", labels);
        Assert.DoesNotContain("Net pay", labels);
        Assert.DoesNotContain(voucher.Lines, line => line.Label.Contains("Federal", StringComparison.OrdinalIgnoreCase) || line.Label.Contains("REG", StringComparison.Ordinal));

        // The check was lost before it went out: once voided outside payroll, it is sent again with a new key.
        await payments.Checks.Service.VoidAsync(garnishmentCheck, "Lost in the mail", TestContext.Current.CancellationToken);
        await harness.RunAsync(async services =>
        {
            var orders = services.GetRequiredService<IGarnishmentOrderManager>();
            var order = await orders.FindByIdAsync(orderId);

            order.Remittances[0].ReferenceId = null;
            order.Remittances[0].Problem = "Lost in the mail";
            await orders.UpdateAsync(order);
        });

        var problem = await harness.RunAsync(async services => await services.GetRequiredService<GarnishmentStatementObserver>().RetryAsync(orderId, remittance.StatementId));
        var retried = await harness.RunAsync(async services => await services.GetRequiredService<IGarnishmentOrderManager>().FindByIdAsync(orderId), commit: false);

        Assert.Null(problem);
        Assert.Equal(1, retried.Remittances[0].Attempt);
        Assert.NotNull(retried.Remittances[0].ReferenceId);
        Assert.Equal(3, payments.Checks.Store.Entries.Count);
        Assert.EndsWith(":1", payments.Checks.Store.Entries.Single(check => check.ItemId == retried.Remittances[0].ReferenceId).IdempotencyKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Process_RefusesARunWhosePersonHadAnotherStatementFinalizedAfterItWasCalculated()
    {
        var context = new DisbursementsTestContext();
        var origin = await context.Checks.BusinessAccountAsync();
        await using var harness = await PayRunTests.CreateHarnessAsync(services => PayRunProcessingTests.AddPayments(services, context, deposits: false, checks: true));
        var setup = await PayRunTests.SetUpAsync(harness, employerSetup: employer => employer.FundingBankAccountId = origin.ItemId);

        await PayRunProcessingTests.ApproveAsync(harness, setup);

        // An off-cycle pay of the same person was finalized after the run was calculated.
        await harness.RunAsync(async services =>
        {
            var draft = Assert.Single(await services.GetRequiredService<IPayRunService>().ListStatementsAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId)));

            await services.GetRequiredService<PayStatementStore>().CreateAsync(new PayStatement
            {
                ItemId = "offcycle1",
                RunId = "another-run",
                StaffMemberId = draft.StaffMemberId,
                EmployerId = draft.EmployerId,
                PayDate = draft.PayDate,
                PeriodStart = draft.PeriodStart,
                PeriodEnd = draft.PeriodEnd,
                Status = PayStatementStatus.Paid,
                CreatedUtc = draft.CreatedUtc.AddMinutes(1),
                ProcessedUtc = draft.CreatedUtc.AddMinutes(1),
            });
        });

        var clerk = CalendarTestHarness.User("clerk", PayrollPermissions.ProcessPayRuns.Name);
        var result = await harness.RunAsync(async services => await services.GetRequiredService<IPayRunService>().ProcessAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId), clerk));
        var run = await harness.RunAsync(async services => await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId), commit: false);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("Calculate the run again", StringComparison.Ordinal));
        Assert.Equal(PayRunStatus.Approved, run.Status);
        Assert.Empty(context.Checks.Store.Entries);
    }

    [Fact]
    public async Task TaxIdHashKey_IsKeptProtected_AndAKeyKeptInClearIsProtectedWithTheSameValue()
    {
        await using var harness = await PayRunTests.CreateHarnessAsync();
        var legacy = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);

        // An earlier version kept the key in clear.
        await harness.RunAsync(async services => await services.GetRequiredService<global::YesSql.ISession>().SaveAsync(new PayrollKeys { TaxIdHashKey = Convert.ToBase64String(legacy) }, false, PayrollConstants.CollectionName));

        var hash = await harness.RunAsync(async services => await services.GetRequiredService<PayrollTaxIdHasher>().HashAsync("123-45-6789"));
        var stored = await harness.RunAsync(async services => await services.GetRequiredService<global::YesSql.ISession>().Query<PayrollKeys>(collection: PayrollConstants.CollectionName).FirstOrDefaultAsync(), commit: false);

        // The hashes already stored still match: the same key, now protected.
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(legacy, System.Text.Encoding.UTF8.GetBytes("123456789"))), hash);
        Assert.Null(stored.TaxIdHashKey);
        Assert.False(string.IsNullOrEmpty(stored.ProtectedTaxIdHashKey));
        Assert.DoesNotContain(Convert.ToBase64String(legacy), stored.ProtectedTaxIdHashKey, StringComparison.Ordinal);

        // A second hasher, in another scope, reads the protected key back.
        Assert.Equal(hash, await harness.RunAsync(async services => await services.GetRequiredService<PayrollTaxIdHasher>().HashAsync("123 45 6789"), commit: false));
    }

    [Fact]
    public async Task TaxIdHashKey_MadeNew_IsNeverStoredAsItsRawBytes()
    {
        await using var harness = await PayRunTests.CreateHarnessAsync();

        await harness.RunAsync(async services => await services.GetRequiredService<PayrollTaxIdHasher>().HashAsync("987654321"));

        var stored = Assert.Single(await harness.RunAsync(async services => await services.GetRequiredService<global::YesSql.ISession>().Query<PayrollKeys>(collection: PayrollConstants.CollectionName).ListAsync(), commit: false));
        var raw = new byte[64];

        Assert.Null(stored.TaxIdHashKey);
        Assert.False(Convert.TryFromBase64String(stored.ProtectedTaxIdHashKey, raw, out var length) && length == 32);
    }

    [Fact]
    public void SameTaxId_NamesOnlyProfilesAtEmployersTheUserManages()
    {
        var same = new[]
        {
            new EmployeePayProfile { StaffMemberId = "ana", EmployerId = "contoso" },
            new EmployeePayProfile { StaffMemberId = "ben", EmployerId = "fabrikam" },
            new EmployeePayProfile { StaffMemberId = "cy", EmployerId = "fabrikam" },
        };
        var names = new Dictionary<string, string> { ["ana"] = "Ana Diaz", ["ben"] = "Ben Ode", ["cy"] = "Cy Wu" };

        Assert.Equal(["Ana Diaz", "another pay profile"], CrestApps.OrchardCore.Payroll.Drivers.PayProfileDisplayDriver.SameTaxIdLabels(same, new PayrollScope(false, ["contoso"]), names, "another pay profile"));
        Assert.Equal(["Ana Diaz", "Ben Ode", "Cy Wu"], CrestApps.OrchardCore.Payroll.Drivers.PayProfileDisplayDriver.SameTaxIdLabels(same, PayrollScope.Everyone, names, "another pay profile"));
    }

    [Fact]
    public async Task SavedExportColumns_WaitQuietlyForDataPipelines_AndAreKeptUntilThen()
    {
        var features = new Moq.Mock<global::OrchardCore.Environment.Shell.IShellFeaturesManager>();

        features.Setup(manager => manager.GetEnabledFeaturesAsync()).ReturnsAsync([]);

        // Without Data Pipelines nothing is read or written: the saved columns stay for the next start.
        var session = new Moq.Mock<global::YesSql.ISession>(Moq.MockBehavior.Strict);
        var services = new ServiceCollection().AddSingleton(features.Object).BuildServiceProvider();
        var importer = new CrestApps.OrchardCore.Workforce.Recipes.PayrollCsvPipelineImporter(services, session.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<CrestApps.OrchardCore.Workforce.Recipes.PayrollCsvPipelineImporter>.Instance);

        await importer.RetrySavedColumnsAsync();

        session.VerifyNoOtherCalls();

        // The retry runs at each start of the site.
        Assert.Contains(typeof(CrestApps.OrchardCore.Workforce.Recipes.PayrollCsvPipelineImporter).Assembly.GetTypes(), type => type.Name == "PayrollCsvColumnsRetry" && typeof(global::OrchardCore.Modules.IModularTenantEvents).IsAssignableFrom(type));
    }

    [Fact]
    public void TimeClockUpgrade_GivesWhoSawPayRatesThePayCardPermission_NeverTheExportOfAllPayrollData()
    {
        var grants = (IReadOnlyDictionary<string, string[]>)typeof(CrestApps.OrchardCore.Workforce.Migrations.TimeClockMigrations)
            .GetField("_exportGrants", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .GetValue(null);

        Assert.Equal([CrestApps.OrchardCore.Staff.StaffPermissions.ManageStaffPay.Name], grants["ViewPayRates"]);
    }

    [Fact]
    public async Task GarnishmentsDataSet_ShowsTheCaseOnlyToWhoManagesTheEmployersOrders()
    {
        await using var harness = await PayRunTests.CreateHarnessAsync(services => services.AddGarnishmentsCore());
        var setup = await PayRunTests.SetUpAsync(harness);

        await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IGarnishmentOrderManager>();
            var order = await manager.NewAsync();

            order.ProfileId = "p";
            order.EmployerId = setup.EmployerId;
            order.StaffMemberId = setup.StaffMemberId;
            order.Type = GarnishmentType.Creditor;
            order.CaseNumber = "CS-4004";
            order.Agency = "Circuit court";
            await manager.CreateAsync(order);
        });

        var exporter = CalendarTestHarness.User("exporter", PayrollPermissions.ExportPayrollData.Name);
        var garnishmentManager = CalendarTestHarness.User("manager", PayrollPermissions.ExportPayrollData.Name, PayrollPermissionTemplates.For(PayrollPermissions.ManageGarnishments, setup.EmployerId).Name);

        async Task<Dictionary<string, object>> ReadAsync(ClaimsPrincipal user)
            => await harness.RunAsync(async services =>
            {
                var reader = new PayrollReportReader(services.GetRequiredService<IPayrollScopeService>(), services.GetRequiredService<PayStatementStore>(), services.GetRequiredService<PayRunStore>(), services.GetRequiredService<EmployerStore>(), new HttpContextAccessor());
                var dataSet = ActivatorUtilities.CreateInstance<GarnishmentsDataSet>(services, reader);
                var table = await ReportDesignerTestServices.ReadAsync(new PayrollDataSource([dataSet]), new DataSourceQuery { DataSet = "Garnishments", Fields = new HashSet<string>(StringComparer.Ordinal), MaxRows = 10, Context = new DataSourceContext { User = user } }, TestContext.Current.CancellationToken);

                return table.Fields.Select((field, index) => (field.Name, Value: table.Rows.Single()[index])).ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal);
            }, commit: false);

        var plain = await ReadAsync(exporter);
        var full = await ReadAsync(garnishmentManager);

        Assert.Null(plain["CaseNumber"]);
        Assert.Null(plain["Agency"]);
        Assert.Equal(("CS-4004", "Circuit court"), (full["CaseNumber"], full["Agency"]));
    }
}
