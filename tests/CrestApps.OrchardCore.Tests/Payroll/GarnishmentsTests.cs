using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Payroll.Controllers;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Deployments;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Recipes;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Disbursements;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// Garnishments: the limits on disposable earnings by kind and their order, an order stopping at its total owed, a run
/// that withholds, sends a check to the payee and gives it back on a void, the order's checks, the pages per employer
/// and never one's own, the recipe and deployment steps.
/// </summary>
public sealed class GarnishmentsTests
{
    // A biweekly pay: 1000 of gross pay, 100 of the employee's taxes, so 900 of disposable earnings; two weeks leave
    // 2 x 217.50 = 435 to the person against a creditor.
    public static TheoryData<string, GarnishmentOrder[], decimal[]> LimitCases()
        => new()
        {
            // A creditor's 50% is held to 25% of 900 = 225 (900 - 435 = 465 is higher).
            { "Creditor held to 25%", [Order(GarnishmentType.Creditor, percent: 50)], [225m] },

            // Support first takes 300 of its 50% (450); the creditor's 25% (225) is already used up by it.
            { "Support then creditor", [Order(GarnishmentType.Creditor, amount: 200, received: 1), Order(GarnishmentType.Support, amount: 300, received: 2)], [300m] },

            // Support 100, then a federal levy takes what is left above its 600 exempt: 900 - 100 - 600 = 200.
            { "Support then federal levy", [Order(GarnishmentType.Support, amount: 100), Order(GarnishmentType.FederalTaxLevy, amount: 500, exempt: 600)], [100m, 200m] },

            // Two creditors share the 225: the first received takes 150, the second the 75 left.
            { "Two creditors by received date", [Order(GarnishmentType.Creditor, amount: 150, received: 1), Order(GarnishmentType.Creditor, amount: 150, received: 2)], [150m, 75m] },

            // A student loan's 15% of 900 is 135.
            { "Student loan at 15%", [Order(GarnishmentType.StudentLoan, amount: 500)], [135m] },

            // A total owed of 1000 with 950 withheld leaves 50.
            { "Stops at the total owed", [Order(GarnishmentType.Support, amount: 100, totalOwed: 1000, withheld: 950)], [50m] },

            // Bankruptcy takes its amount, with no percent limit.
            { "Bankruptcy has no percent limit", [Order(GarnishmentType.Bankruptcy, amount: 700)], [700m] },
        };

    [Theory]
    [MemberData(nameof(LimitCases))]
    public async Task Step_WithholdsWithinTheLimitOfEachKind_InOrder(string description, GarnishmentOrder[] orders, decimal[] expected)
    {
        Assert.NotNull(description);

        var context = Context(orders);

        await new GarnishmentsPayCalculationStep(new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<GarnishmentsPayCalculationStep>()).ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(expected, context.Lines.Where(line => line.GarnishmentOrderId is not null).Select(line => line.Amount));
    }

    [Fact]
    public async Task Step_LeavesThePayLeftEachWeek_AndSaysWhenALimitHeldAnOrderBack()
    {
        // 500 of disposable earnings: 25% would be 125, but only 500 - 435 = 65 is above the pay left.
        var context = Context([Order(GarnishmentType.Creditor, amount: 200)], gross: 600);

        await new GarnishmentsPayCalculationStep(new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<GarnishmentsPayCalculationStep>()).ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(65m, Assert.Single(context.Lines, line => line.GarnishmentOrderId is not null).Amount);
        Assert.Contains(context.Warnings, warning => warning.Contains("limited to 65.00", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Run_WithholdsTheOrder_SendsACheckToThePayee_AndAVoidGivesItBackAndStopsTheCheck()
    {
        var payments = new DisbursementsTestContext();
        var origin = await payments.OriginAsync();
        var observer = new RecordingGarnishmentObserver();
        await using var harness = await PayRunTests.CreateHarnessAsync(services =>
        {
            PayRunProcessingTests.AddPayments(services, payments, deposits: false, checks: true);
            services.AddGarnishmentsCore();
            services.AddSingleton<IGarnishmentObserver>(observer);
        });
        var setup = await PayRunTests.SetUpAsync(harness, employerSetup: employer => employer.FundingBankAccountId = origin.ItemId);
        var orderId = await harness.RunAsync(async services =>
        {
            await services.GetRequiredService<GarnishmentPayCodeSeeder>().SeedAsync();

            var profile = (await services.GetRequiredService<IPayProfileManager>().ListByEmployersAsync([setup.EmployerId])).Single();
            var manager = services.GetRequiredService<IGarnishmentOrderManager>();
            var order = await manager.NewAsync();

            order.ProfileId = profile.ItemId;
            order.Type = GarnishmentType.Support;
            order.CaseNumber = "CS-1001";
            order.PayCodeId = (await services.GetRequiredService<IPayCodeManager>().FindByCodeAsync(GarnishmentPayCodeSeeder.Code)).ItemId;
            order.Amount = 400;
            order.TotalOwed = 400;
            order.StartDate = new DateOnly(2026, 1, 1);
            order.PayeeName = "State disbursement unit";
            order.PayeeAddress = new CrestApps.OrchardCore.Addresses.Models.Address { AddressLine1 = "1 Main St", City = "Springfield", Region = "IL", PostalCode = "62701", Country = "US" };
            order.PaymentMethod = GarnishmentPaymentMethod.Check;

            var validation = await manager.ValidateAsync(order);

            Assert.True(validation.Succeeded, string.Join(" ", validation.Errors.Select(error => error.ErrorMessage)));
            await manager.CreateAsync(order);

            return order.ItemId;
        });

        await PayRunProcessingTests.ApproveAsync(harness, setup);

        var processor = CalendarTestHarness.User("processor", PayrollPermissionTemplates.For(PayrollPermissions.ProcessPayRuns, setup.EmployerId).Name);
        var processed = await harness.RunAsync(async services =>
            await services.GetRequiredService<IPayRunService>().ProcessAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId), processor));

        Assert.True(processed.Succeeded, string.Join(" ", processed.Errors));

        var (statement, order) = await harness.RunAsync(async services =>
        {
            var run = await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId);

            return (Assert.Single(await services.GetRequiredService<IPayRunService>().ListStatementsAsync(run)), await services.GetRequiredService<IGarnishmentOrderManager>().FindByIdAsync(orderId));
        }, commit: false);

        // 1600 gross less 99.20 of tax: 400 withheld for the order and net pay 1100.80.
        var line = Assert.Single(statement.Lines, candidate => candidate.GarnishmentOrderId == orderId);
        Assert.Equal((400m, "Garnishment CS-1001"), (line.Amount, line.Description));
        Assert.Equal(1100.80m, statement.Net);
        Assert.Equal((400m, GarnishmentStatus.Completed), (order.Withheld, order.Status));

        var remittance = Assert.Single(order.Remittances);
        Assert.Null(remittance.Problem);
        Assert.StartsWith("Check ", remittance.Reference, StringComparison.Ordinal);

        var check = await payments.Checks.Manager.FindByIdAsync(remittance.ReferenceId, TestContext.Current.CancellationToken);
        Assert.Equal(("State disbursement unit", 400m, GarnishmentSources.Kind, GarnishmentSources.SourceId(orderId, statement.ItemId), setup.EmployerId), (check.PayeeName, check.Amount, check.SourceKind, check.SourceId, check.AccessScope));

        var voider = CalendarTestHarness.User("voider", PayrollPermissions.VoidPayStatements.Name);
        var voided = await harness.RunAsync(async services =>
            await services.GetRequiredService<IPayRunService>().VoidStatementAsync(await services.GetRequiredService<PayStatementStore>().FindByIdAsync(statement.ItemId), voider, "Wrong hours"));

        Assert.True(voided.Succeeded, string.Join(" ", voided.Errors));

        var after = await harness.RunAsync(async services => await services.GetRequiredService<IGarnishmentOrderManager>().FindByIdAsync(orderId), commit: false);
        var voidedCheck = await payments.Checks.Manager.FindByIdAsync(remittance.ReferenceId, TestContext.Current.CancellationToken);

        Assert.Equal((0m, GarnishmentStatus.Active), (after.Withheld, after.Status));
        Assert.True(Assert.Single(after.Remittances).Voided);
        Assert.Equal(CrestApps.OrchardCore.Banking.Checks.Models.CheckStatus.Voided, voidedCheck.Status);

        // The workflow events: withheld with the check sent, the order completed, then the remittance voided.
        Assert.Equal([GarnishmentChangeKind.Withheld, GarnishmentChangeKind.Completed, GarnishmentChangeKind.RemittanceVoided], observer.Changes.Select(change => change.Kind));
        Assert.Equal((remittance.ReferenceId, 400m, statement.ItemId), (observer.Changes[0].Remittance.ReferenceId, observer.Changes[0].Remittance.Amount, observer.Changes[0].Statement.ItemId));
        Assert.True(observer.Changes[2].Remittance.Voided);
    }

    [Fact]
    public async Task Order_NeedsAProfileADeductionCodeAnAmountAndAWayToPayThePayee()
    {
        await using var harness = await PayRunTests.CreateHarnessAsync(services => services.AddGarnishmentsCore());
        var setup = await PayRunTests.SetUpAsync(harness);

        var (empty, earningCode, bankWithoutAccount, valid) = await harness.RunAsync(async services =>
        {
            await services.GetRequiredService<GarnishmentPayCodeSeeder>().SeedAsync();

            var manager = services.GetRequiredService<IGarnishmentOrderManager>();
            var codes = services.GetRequiredService<IPayCodeManager>();
            var profile = (await services.GetRequiredService<IPayProfileManager>().ListByEmployersAsync([setup.EmployerId])).Single();

            GarnishmentOrder Valid(string payCodeId)
                => new()
                {
                    ProfileId = profile.ItemId,
                    Type = GarnishmentType.Creditor,
                    PayCodeId = payCodeId,
                    Percent = 10,
                    StartDate = new DateOnly(2026, 1, 1),
                    PayeeName = "Fabrikam Collections",
                    PaymentMethod = GarnishmentPaymentMethod.Outside,
                };

            var garnishment = (await codes.FindByCodeAsync(GarnishmentPayCodeSeeder.Code)).ItemId;
            var bank = Valid(garnishment);

            bank.PaymentMethod = GarnishmentPaymentMethod.BankAccount;

            var good = Valid(garnishment);
            var result = await manager.ValidateAsync(good);

            return (
                await manager.ValidateAsync(new GarnishmentOrder()),
                await manager.ValidateAsync(Valid(setup.BonusCodeId)),
                await manager.ValidateAsync(bank),
                (result, good));
        }, commit: false);

        Assert.False(empty.Succeeded);
        Assert.Contains(empty.Errors, error => error.ErrorMessage.Contains("pay profile", StringComparison.Ordinal));
        Assert.Contains(empty.Errors, error => error.ErrorMessage.Contains("an amount each pay", StringComparison.Ordinal));
        Assert.False(earningCode.Succeeded);
        Assert.False(bankWithoutAccount.Succeeded);
        Assert.Contains(bankWithoutAccount.Errors, error => error.ErrorMessage.Contains("payee's bank account", StringComparison.Ordinal));
        Assert.True(valid.result.Succeeded, string.Join(" ", valid.result.Errors.Select(error => error.ErrorMessage)));
        Assert.Equal(setup.EmployerId, valid.good.EmployerId);
        Assert.Equal(setup.StaffMemberId, valid.good.StaffMemberId);

        // The kind's defaults fill what an order leaves empty.
        var order = new GarnishmentOrder { Type = GarnishmentType.Creditor };

        GarnishmentOrderHandler.Defaults(order);

        Assert.Equal((60, 25m, 217.50m), (order.Priority, order.LimitPercent.Value, order.ProtectedWeeklyAmount.Value));
    }

    [Fact]
    public async Task Pages_FollowTheEmployersScope_NobodyManagesTheirOwnOrders_AndAnOrderThatWithheldIsKept()
    {
        await using var harness = await PayRunTests.CreateHarnessAsync(services => services.AddGarnishmentsCore());
        var setup = await PayRunTests.SetUpAsync(harness);
        var orderId = await AddOrderAsync(harness, setup, withheld: true);

        var contosoClerk = CalendarTestHarness.User("clerk", PayrollPermissionTemplates.For(PayrollPermissions.ManageGarnishments, setup.EmployerId).Name);
        var otherClerk = CalendarTestHarness.User("other", PayrollPermissionTemplates.For(PayrollPermissions.ManageGarnishments, "fabrikam").Name);
        var nobody = CalendarTestHarness.User("nobody");

        var (clerkIndex, otherIndex, nobodyIndex, clerkEdit, otherEdit) = await harness.RunAsync(async services =>
            (await Controller(services, contosoClerk).Index(), await Controller(services, otherClerk).Index(), await Controller(services, nobody).Index(),
             await Controller(services, contosoClerk).Edit(orderId), await Controller(services, otherClerk).Edit(orderId)), commit: false);

        var list = Assert.IsType<ViewResult>(clerkIndex);
        Assert.Single(Assert.IsType<CrestApps.OrchardCore.Core.Models.ListCatalogEntryViewModel<CrestApps.OrchardCore.Core.Models.CatalogEntryViewModel<GarnishmentOrder>>>(list.Model).Models);

        // A clerk of another employer manages none of this one's orders: no list at all.
        Assert.IsType<ForbidResult>(otherIndex);
        Assert.IsType<ForbidResult>(nobodyIndex);
        Assert.IsType<ViewResult>(clerkEdit);
        Assert.IsType<ForbidResult>(otherEdit);

        // The person themself, even with the permission, may not manage their own order.
        var lee = CalendarTestHarness.User("lee", PayrollPermissions.ManageGarnishments.Name);
        var ownEdit = await harness.RunAsync(async services =>
        {
            var member = await services.GetRequiredService<CrestApps.OrchardCore.Staff.Services.IStaffMemberManager>().FindByIdAsync(setup.StaffMemberId);

            await LinkAsync(services, member, lee);

            return await Controller(services, lee).Edit(orderId);
        });

        Assert.IsType<ForbidResult>(ownEdit);

        // Rule C: an order that withheld pay is released, never deleted.
        await harness.RunAsync(async services => await Controller(services, contosoClerk).Delete(orderId));

        Assert.NotNull(await harness.RunAsync(async services => await services.GetRequiredService<IGarnishmentOrderManager>().FindByIdAsync(orderId), commit: false));

        await harness.RunAsync(async services => await Controller(services, contosoClerk).Release(orderId));

        Assert.Equal(GarnishmentStatus.Released, (await harness.RunAsync(async services => await services.GetRequiredService<IGarnishmentOrderManager>().FindByIdAsync(orderId), commit: false)).Status);
    }

    [Fact]
    public async Task Deployment_FailsClosedWithoutAUser_LeavesCaseNumbersOut_AndTheRecipeBringsTheOrderBack()
    {
        await using var harness = await PayRunTests.CreateHarnessAsync(services => services.AddGarnishmentsCore());
        var setup = await PayRunTests.SetUpAsync(harness);
        var orderId = await AddOrderAsync(harness, setup, withheld: false);
        var exporter = CalendarTestHarness.User("exporter", PayrollPermissions.ExportPayrollData.Name);

        async Task<JsonObject> ExportAsync(ClaimsPrincipal user, bool sensitive)
            => await harness.RunAsync(async services =>
            {
                var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = user ?? new ClaimsPrincipal(new ClaimsIdentity()) } };
                var source = new GarnishmentsDeploymentSource(services.GetRequiredService<IGarnishmentOrderManager>(), services.GetRequiredService<IPayrollScopeService>(), accessor, Mock.Of<INotifier>(), new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughHtmlLocalizer<GarnishmentsDeploymentSource>());
                var result = new DeploymentPlanResult(Mock.Of<IFileBuilder>(), new global::OrchardCore.Recipes.Models.RecipeDescriptor());

                await source.ProcessDeploymentStepAsync(new GarnishmentsDeploymentStep { IncludeSensitiveData = sensitive }, result);

                return result.Steps.OfType<JsonObject>().SingleOrDefault();
            }, commit: false);

        Assert.Null(await ExportAsync(null, sensitive: true));

        var plain = await ExportAsync(exporter, sensitive: false);
        var full = await ExportAsync(exporter, sensitive: true);
        var plainOrder = plain![GarnishmentsRecipeStep.EntriesName]!.AsArray().Single()!.AsObject();

        Assert.False(plainOrder.ContainsKey(nameof(GarnishmentOrder.CaseNumber)));
        Assert.Equal("CS-2002", full![GarnishmentsRecipeStep.EntriesName]![0]![nameof(GarnishmentOrder.CaseNumber)]!.GetValue<string>());

        // Imported again over a deleted order, it comes back with its values.
        await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IGarnishmentOrderManager>();

            await manager.DeleteAsync(await manager.FindByIdAsync(orderId));
        });

        var errors = await harness.RunAsync(async services =>
        {
            var context = new RecipeExecutionContext { Name = PayrollConstants.GarnishmentsRecipeStepName, Step = full.DeepClone().AsObject() };

            await ActivatorUtilities.CreateInstance<GarnishmentsRecipeStep>(services).ExecuteAsync(context);

            return context.Errors.ToList();
        });

        var imported = await harness.RunAsync(async services => await services.GetRequiredService<IGarnishmentOrderManager>().FindByIdAsync(orderId), commit: false);

        Assert.Empty(errors);
        Assert.Equal(("CS-2002", 150m, GarnishmentType.Creditor), (imported.CaseNumber, imported.Amount.Value, imported.Type));
    }

    private static async Task<string> AddOrderAsync(CalendarTestHarness harness, PayRunTests.Setup setup, bool withheld)
        => await harness.RunAsync(async services =>
        {
            await services.GetRequiredService<GarnishmentPayCodeSeeder>().SeedAsync();

            var profile = (await services.GetRequiredService<IPayProfileManager>().ListByEmployersAsync([setup.EmployerId])).Single();
            var manager = services.GetRequiredService<IGarnishmentOrderManager>();
            var order = await manager.NewAsync();

            order.ProfileId = profile.ItemId;
            order.Type = GarnishmentType.Creditor;
            order.CaseNumber = "CS-2002";
            order.PayCodeId = (await services.GetRequiredService<IPayCodeManager>().FindByCodeAsync(GarnishmentPayCodeSeeder.Code)).ItemId;
            order.Amount = 150;
            order.StartDate = new DateOnly(2026, 1, 1);
            order.PayeeName = "Fabrikam Collections";
            order.PaymentMethod = GarnishmentPaymentMethod.Outside;

            if (withheld)
            {
                order.Withheld = 150;
                order.Remittances = [new GarnishmentRemittance { StatementId = "s1", Amount = 150, PayDate = new DateOnly(2026, 11, 6), Method = GarnishmentPaymentMethod.Outside }];
            }

            await manager.CreateAsync(order);

            return order.ItemId;
        });

    private static async Task<bool> LinkAsync(IServiceProvider services, CrestApps.OrchardCore.Staff.Models.StaffMember member, ClaimsPrincipal user)
    {
        member.UserId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        await services.GetRequiredService<CrestApps.OrchardCore.Staff.Services.IStaffMemberManager>().UpdateAsync(member);

        return true;
    }

    private static GarnishmentsController Controller(IServiceProvider services, ClaimsPrincipal user)
    {
        var displayManager = new Mock<IDisplayManager<GarnishmentOrder>>();
        var controller = new GarnishmentsController(
            services.GetRequiredService<IGarnishmentOrderManager>(),
            services.GetRequiredService<IPayProfileManager>(),
            services.GetRequiredService<IPayProfileService>(),
            services.GetRequiredService<IPayrollScopeService>(),
            services.GetRequiredService<PayrollNames>(),
            Mock.Of<IUpdateModelAccessor>(accessor => accessor.ModelUpdater == new CrestApps.OrchardCore.Tests.Taxation.Fakes.FakeUpdateModel(new object())),
            displayManager.Object,
            Mock.Of<INotifier>(),
            services.GetRequiredService<ILocalClock>(),
            new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughHtmlLocalizer<GarnishmentsController>(),
            new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<GarnishmentsController>());

        controller.ControllerContext = new ControllerContext
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
        controller.Url = Mock.Of<IUrlHelper>();

        return controller;
    }

    private static GarnishmentOrder Order(GarnishmentType type, decimal? amount = null, decimal? percent = null, decimal? exempt = null, decimal? totalOwed = null, decimal withheld = 0, int received = 1)
    {
        var order = new GarnishmentOrder
        {
            ItemId = Guid.NewGuid().ToString("n")[..26],
            Type = type,
            PayCodeId = "garn",
            Amount = amount,
            Percent = percent,
            ExemptAmount = exempt,
            TotalOwed = totalOwed,
            Withheld = withheld,
            ReceivedDate = new DateOnly(2026, 1, received),
            StartDate = new DateOnly(2026, 1, 1),
        };

        GarnishmentOrderHandler.Defaults(order);

        return order;
    }

    private static PayCalculationContext Context(IEnumerable<GarnishmentOrder> orders, decimal gross = 1000)
    {
        var earning = new PayCode { ItemId = "reg", Code = "REG", Class = PayCodeClass.Earning, Kind = PayCodeKinds.Regular };
        var garnishment = new PayCode { ItemId = "garn", Code = "GARN", Name = "Garnishment", Class = PayCodeClass.Deduction, Kind = PayCodeKinds.Garnishment };
        var context = new PayCalculationContext
        {
            Employer = new Employer { Currency = "USD" },
            Schedule = new PaySchedule { Frequency = PayFrequency.Biweekly },
            PayDate = new DateOnly(2026, 11, 20),
            Codes = new Dictionary<string, PayCode> { ["reg"] = earning, ["garn"] = garnishment },
            Garnishments = [.. orders],
        };

        context.Lines.Add(new PayStatementLine { Type = PayLineType.Earning, PayCodeId = "reg", Amount = gross });
        context.Lines.Add(new PayStatementLine { Type = PayLineType.Tax, TaxCode = "T", Payer = PayLinePayer.Employee, Amount = 100 });

        return context;
    }
}
