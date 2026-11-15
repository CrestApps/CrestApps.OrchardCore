using System.Security.Claims;
using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Approvals.Core;
using CrestApps.OrchardCore.Approvals.Core.Services;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Payroll.Core;
using CrestApps.OrchardCore.Payroll.Core.Indexes;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Tests.Approvals;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Workforce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// Pay runs: calculating the draft statements, two-person approval through Approvals, a change after approval
/// reopening the run, a file of bonuses saved with its bad rows refused with the reason, and the run's life.
/// </summary>
public sealed class PayRunTests
{
    [Fact]
    public async Task Run_IsApprovedByAnotherPerson_NeverByWhoPreparedIt()
    {
        await using var harness = await CreateHarnessAsync();
        var setup = await SetUpAsync(harness);
        var preparer = CalendarTestHarness.User("preparer", PayrollPermissionTemplates.For(PayrollPermissions.PreparePayRuns, setup.EmployerId).Name, PayrollPermissionTemplates.For(PayrollPermissions.ApprovePayRuns, setup.EmployerId).Name);
        var approver = CalendarTestHarness.User("approver", PayrollPermissionTemplates.For(PayrollPermissions.ApprovePayRuns, setup.EmployerId).Name);
        var outsider = CalendarTestHarness.User("outsider", PayrollPermissionTemplates.For(PayrollPermissions.ApprovePayRuns, "fabrikam").Name);

        var (calculated, submitted) = await harness.RunAsync(async services =>
        {
            var service = services.GetRequiredService<IPayRunService>();
            var run = await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId);
            var calculation = await service.CalculateAsync(run, preparer);

            return (calculation, await service.SubmitAsync(run, preparer));
        });

        var (preparerMay, approverMay, outsiderMay) = await harness.RunAsync(async services =>
        {
            var approvals = services.GetRequiredService<IApprovalService>();
            var request = await services.GetRequiredService<IApprovalRequestManager>().FindByIdAsync((await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId)).ApprovalRequestId);

            return (
                (await approvals.GetEligibilityAsync(request, preparer)).CanDecide,
                (await approvals.GetEligibilityAsync(request, approver)).CanDecide,
                (await approvals.GetEligibilityAsync(request, outsider)).CanDecide);
        }, commit: false);

        await harness.RunAsync(async services =>
        {
            var request = await services.GetRequiredService<IApprovalRequestManager>().FindByIdAsync((await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId)).ApprovalRequestId);
            var decision = await services.GetRequiredService<IApprovalService>().DecideAsync(request, approver, ApprovalDecisionKind.Approve, "Looks right");

            Assert.True(decision.Succeeded, string.Join(" ", decision.Errors));
        });

        var (run, statements) = await harness.RunAsync(async services =>
        {
            var found = await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId);

            return (found, await services.GetRequiredService<IPayRunService>().ListStatementsAsync(found));
        }, commit: false);

        Assert.True(calculated.Succeeded, string.Join(" ", calculated.Errors));
        Assert.True(submitted.Succeeded, string.Join(" ", submitted.Errors));
        Assert.False(preparerMay);
        Assert.True(approverMay);
        Assert.False(outsiderMay);
        Assert.Equal(PayRunStatus.Approved, run.Status);
        Assert.Equal((1, 1600m, 1500.80m), (run.Totals.Statements, run.Totals.Gross, run.Totals.Net));
        Assert.Equal("preparer", run.PreparedBy);
        Assert.Equal(PayStatementStatus.Draft, Assert.Single(statements).Status);
    }

    [Fact]
    public async Task ChangeAfterApproval_ReopensTheRun_AndCancelsTheApproval()
    {
        await using var harness = await CreateHarnessAsync();
        var setup = await SetUpAsync(harness);
        var preparer = CalendarTestHarness.User("preparer", PayrollPermissions.PreparePayRuns.Name);
        var approver = CalendarTestHarness.User("approver", PayrollPermissions.ApprovePayRuns.Name);

        await harness.RunAsync(async services =>
        {
            var service = services.GetRequiredService<IPayRunService>();
            var run = await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId);

            await service.CalculateAsync(run, preparer);
            await service.SubmitAsync(run, preparer);
        });

        var requestId = await harness.RunAsync(async services =>
        {
            var run = await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId);
            var request = await services.GetRequiredService<IApprovalRequestManager>().FindByIdAsync(run.ApprovalRequestId);

            await services.GetRequiredService<IApprovalService>().DecideAsync(request, approver, ApprovalDecisionKind.Approve, null);

            return request.ItemId;
        });

        // A bonus typed after the approval: the person is calculated again, and the approval no longer holds.
        var reopened = await harness.RunAsync(async services =>
        {
            var inputs = services.GetRequiredService<IPayRunInputManager>();
            var input = await inputs.NewAsync();

            input.RunId = setup.RunId;
            input.StaffMemberId = setup.StaffMemberId;
            input.PayCodeId = setup.BonusCodeId;
            input.Amount = 250;
            await inputs.CreateAsync(input);

            var run = await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId);

            Assert.Equal(PayRunStatus.Approved, run.Status);

            await services.GetRequiredService<IPayRunService>().CalculateAsync(run, preparer, [setup.StaffMemberId]);

            return run;
        });

        var request = await harness.RunAsync(services => services.GetRequiredService<IApprovalRequestManager>().FindByIdAsync(requestId).AsTask(), commit: false);

        Assert.Equal(PayRunStatus.Calculated, reopened.Status);
        Assert.Null(reopened.ApprovalRequestId);
        Assert.Equal(1850m, reopened.Totals.Gross);
        Assert.Equal(ApprovalStatus.Approved, request.Status);
    }

    [Fact]
    public async Task BonusFile_IsSaved_AndItsBadRowsAreRefusedWithTheReason()
    {
        await using var harness = await CreateHarnessAsync();
        var setup = await SetUpAsync(harness);
        var preparer = CalendarTestHarness.User("preparer", PayrollPermissionTemplates.For(PayrollPermissions.PreparePayRuns, setup.EmployerId).Name);
        var stranger = CalendarTestHarness.User("stranger", PayrollPermissionTemplates.For(PayrollPermissions.PreparePayRuns, "fabrikam").Name);

        PayRunInputRow Row(string employeeNumber, string payCode, string amount)
            => new() { PayRun = setup.RunNumber, EmployeeNumber = employeeNumber, PayCode = payCode, Amount = amount, Note = "November bonus" };

        var errors = await harness.RunAsync(async services =>
        {
            var importer = services.GetRequiredService<PayRunInputImporter>();
            var results = new List<string>
            {
                await importer.SaveAsync(Row("1001", "bonus", "500"), preparer, "Test file"),
                await importer.SaveAsync(Row("9999", "BONUS", "500"), preparer, "Test file"),
                await importer.SaveAsync(Row("1001", "NOPE", "500"), preparer, "Test file"),
                await importer.SaveAsync(Row("1001", "BONUS", "5,00"), preparer, "Test file"),
                await importer.SaveAsync(new PayRunInputRow { EmployeeNumber = "1001", PayCode = "BONUS", Amount = "1" }, preparer, "Test file"),
            };

            await importer.RecalculateAsync(preparer);

            return results;
        });

        var strangerError = await harness.RunAsync(services => services.GetRequiredService<PayRunInputImporter>().SaveAsync(Row("1001", "BONUS", "1"), stranger, "Test file"), commit: false);
        var inputs = (await harness.RunAsync(services => services.GetRequiredService<IPayRunInputManager>().ListByRunAsync(setup.RunId), commit: false)).Where(input => input.Amount is not null).ToList();

        Assert.Null(errors[0]);
        Assert.Contains("employee number 9999", errors[1], StringComparison.Ordinal);
        Assert.Contains("pay code NOPE", errors[2], StringComparison.Ordinal);
        Assert.Contains("dot for decimals", errors[3], StringComparison.Ordinal);
        Assert.Contains("no pay run", errors[4], StringComparison.Ordinal);
        Assert.Contains("no pay run " + setup.RunNumber, strangerError, StringComparison.Ordinal);
        Assert.Equal((500m, "November bonus", "Test file"), (Assert.Single(inputs).Amount, inputs[0].Note, inputs[0].Origin));
    }

    [Fact]
    public async Task SavePayInputs_RefusesARowOfOnesOwnPay_AndARowSavedAgainUpdatesItsInput()
    {
        await using var harness = await CreateHarnessAsync();
        var setup = await SetUpAsync(harness);
        var lee = CalendarTestHarness.User("lee", PayrollPermissions.PreparePayRuns.Name);
        var preparer = CalendarTestHarness.User("preparer", PayrollPermissions.PreparePayRuns.Name);

        // Lee prepares pay runs and is employee 1001 of the run.
        await harness.RunAsync(async services =>
        {
            var staff = services.GetRequiredService<CrestApps.OrchardCore.Staff.Services.IStaffMemberManager>();
            var member = await staff.FindByIdAsync(setup.StaffMemberId);

            member.UserId = lee.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            await staff.UpdateAsync(member);
        });

        PayRunInputRow Row(string amount, string key = null)
            => new() { PayRun = setup.RunNumber, EmployeeNumber = "1001", PayCode = "BONUS", Amount = amount, Key = key };

        var (own, first, again, keyed, rekeyed) = await harness.RunAsync(async services =>
        {
            var importer = services.GetRequiredService<PayRunInputImporter>();

            return (
                await importer.SaveAsync(Row("5000"), lee, "Pipeline bonuses"),
                await importer.SaveAsync(Row("500"), preparer, "Pipeline bonuses"),
                await importer.SaveAsync(Row("500"), preparer, "Pipeline bonuses"),
                await importer.SaveAsync(Row("200", "Pipeline p1:row-7"), preparer, "Pipeline bonuses"),
                await importer.SaveAsync(Row("250", "Pipeline p1:row-7"), preparer, "Pipeline bonuses"));
        });

        var amounts = (await harness.RunAsync(services => services.GetRequiredService<IPayRunInputManager>().ListByRunAsync(setup.RunId), commit: false))
            .Where(input => input.Amount is not null)
            .Select(input => input.Amount.Value)
            .Order()
            .ToArray();

        Assert.Contains("their own pay", own, StringComparison.Ordinal);
        Assert.Null(first);
        Assert.Null(again);
        Assert.Null(keyed);
        Assert.Null(rekeyed);

        // The same row twice is one input; the keyed row was updated from 200 to 250.
        Assert.Equal([250m, 500m], amounts);
    }

    [Fact]
    public async Task Approval_LeavesOutWhoEnteredInputsAndChangedPayItems_EvenWhenATimerCalculated()
    {
        await using var harness = await CreateHarnessAsync();
        var setup = await SetUpAsync(harness);
        var clerk = CalendarTestHarness.User("clerk", PayrollPermissions.PreparePayRuns.Name);
        var itemUser = CalendarTestHarness.User("item-user", PayrollPermissions.ManagePayProfiles.Name);

        // A bonus from a file saved by the clerk, and a pay item changed by someone else on its page.
        await harness.RunAsync(async services =>
        {
            Assert.Null(await services.GetRequiredService<PayRunInputImporter>().SaveAsync(new PayRunInputRow { PayRun = setup.RunNumber, EmployeeNumber = "1001", PayCode = "BONUS", Amount = "100" }, clerk, "Pipeline bonuses"));

            var accessor = services.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();

            if (accessor is not null)
            {
                accessor.HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = itemUser };
            }

            var items = services.GetRequiredService<IPayItemManager>();
            var item = await items.NewAsync();

            item.ProfileId = (await services.GetRequiredService<IPayProfileManager>().ListByEmployersAsync([setup.EmployerId])).Single().ItemId;
            item.PayCodeId = setup.BonusCodeId;
            item.Amount = 10;
            item.StartDate = new DateOnly(2026, 1, 1);
            await items.CreateAsync(item);

            if (accessor is not null)
            {
                accessor.HttpContext = null;
            }
        });

        // A timer calculates and submits: nobody is the preparer.
        var request = await harness.RunAsync(async services =>
        {
            var service = services.GetRequiredService<IPayRunService>();
            var run = await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId);

            await service.CalculateAsync(run, null);
            run = await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId);

            var submitted = await service.SubmitAsync(run, CalendarTestHarness.User("submitter", PayrollPermissions.PreparePayRuns.Name));

            Assert.True(submitted.Succeeded, string.Join(" ", submitted.Errors));

            return await services.GetRequiredService<IApprovalRequestManager>().FindByIdAsync((await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId)).ApprovalRequestId);
        });

        Assert.Contains(clerk.FindFirstValue(ClaimTypes.NameIdentifier), request.SubjectUserIds);
        Assert.Contains(itemUser.FindFirstValue(ClaimTypes.NameIdentifier), request.SubjectUserIds);
    }

    [Fact]
    public async Task PayItems_AreForWhoManagesTheEmployersProfiles_AndNeverOnesOwn()
    {
        await using var harness = await CreateHarnessAsync();
        var setup = await SetUpAsync(harness);
        var contoso = CalendarTestHarness.User("contoso", PayrollPermissionTemplates.For(PayrollPermissions.ManagePayProfiles, setup.EmployerId).Name, PayrollPermissionTemplates.For(PayrollPermissions.ViewPayProfiles, setup.EmployerId).Name);
        var fabrikam = CalendarTestHarness.User("fabrikam", PayrollPermissionTemplates.For(PayrollPermissions.ManagePayProfiles, "fabrikam").Name, PayrollPermissionTemplates.For(PayrollPermissions.ViewPayProfiles, "fabrikam").Name);
        var lee = CalendarTestHarness.User("lee", PayrollPermissions.ManagePayProfiles.Name, PayrollPermissions.ViewPayProfiles.Name);

        var results = await harness.RunAsync(async services =>
        {
            var profileId = (await services.GetRequiredService<IPayProfileManager>().ListByEmployersAsync([setup.EmployerId])).Single().ItemId;
            var staff = services.GetRequiredService<CrestApps.OrchardCore.Staff.Services.IStaffMemberManager>();
            var member = await staff.FindByIdAsync(setup.StaffMemberId);

            member.UserId = lee.FindFirstValue(ClaimTypes.NameIdentifier);
            await staff.UpdateAsync(member);

            CrestApps.OrchardCore.Payroll.Controllers.PayItemsController Controller(ClaimsPrincipal user)
                => new(
                    services.GetRequiredService<IPayItemManager>(),
                    services.GetRequiredService<IPayProfileManager>(),
                    services.GetRequiredService<IPayProfileService>(),
                    services.GetRequiredService<IPayCodeManager>(),
                    services.GetRequiredService<IPayrollScopeService>(),
                    services.GetRequiredService<CrestApps.OrchardCore.Payroll.Services.PayrollNames>(),
                    Moq.Mock.Of<global::OrchardCore.DisplayManagement.ModelBinding.IUpdateModelAccessor>(accessor => accessor.ModelUpdater == new CrestApps.OrchardCore.Tests.Taxation.Fakes.FakeUpdateModel(new object())),
                    Moq.Mock.Of<global::OrchardCore.DisplayManagement.IDisplayManager<EmployeePayItem>>(),
                    Moq.Mock.Of<global::OrchardCore.DisplayManagement.Notify.INotifier>(),
                    new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughHtmlLocalizer<CrestApps.OrchardCore.Payroll.Controllers.PayItemsController>(),
                    new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<CrestApps.OrchardCore.Payroll.Controllers.PayItemsController>())
                {
                    ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
                    {
                        HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                        {
                            User = user,
                            RequestServices = new ServiceCollection()
                                .AddSingleton(Moq.Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory>())
                                .AddSingleton<Microsoft.AspNetCore.Mvc.ModelBinding.IModelMetadataProvider, Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider>()
                                .BuildServiceProvider(),
                        },
                    },
                    Url = Moq.Mock.Of<Microsoft.AspNetCore.Mvc.IUrlHelper>(),
                };

            return (
                await Controller(contoso).Create(profileId),
                await Controller(fabrikam).Create(profileId),
                await Controller(fabrikam).Index(profileId),
                await Controller(lee).Create(profileId));
        });

        Assert.IsNotType<Microsoft.AspNetCore.Mvc.ForbidResult>(results.Item1);
        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(results.Item2);
        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(results.Item3);
        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(results.Item4);
    }

    [Fact]
    public async Task Run_WithBlockers_CantBeSubmitted_AndACancelledRunTakesNoMoreWork()
    {
        await using var harness = await CreateHarnessAsync();
        var setup = await SetUpAsync(harness, withElection: false);
        var preparer = CalendarTestHarness.User("preparer", PayrollPermissions.PreparePayRuns.Name);

        var (submit, cancel, recalculate, run, statements) = await harness.RunAsync(async services =>
        {
            var service = services.GetRequiredService<IPayRunService>();
            var found = await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId);

            await service.CalculateAsync(found, preparer);

            var submitted = await service.SubmitAsync(found, preparer);
            var blockers = found.Issues.Where(issue => issue.IsBlocker).Select(issue => issue.Message).ToList();
            var cancelled = await service.CancelAsync(found, "Test");
            var again = await service.CalculateAsync(found, preparer);

            Assert.Contains(blockers, message => message.Contains("election", StringComparison.OrdinalIgnoreCase));

            // Each step commits under the run's lock: read the run as it is stored now.
            var stored = await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId);

            return (submitted, cancelled, again, stored, await service.ListStatementsAsync(stored));
        });

        Assert.False(submit.Succeeded);
        Assert.True(cancel.Succeeded);
        Assert.False(recalculate.Succeeded);
        Assert.Equal(PayRunStatus.Canceled, run.Status);
        Assert.Empty(statements);
    }

    [Fact]
    public void StatusTable_NeverMovesAProcessedRunBack()
    {
        Assert.True(PayRunStatuses.Transitions.CanMove(PayRunStatus.Approved, PayRunStatus.Calculated));
        Assert.True(PayRunStatuses.Transitions.CanMove(PayRunStatus.Approved, PayRunStatus.Processing));
        Assert.False(PayRunStatuses.Transitions.CanMove(PayRunStatus.Calculated, PayRunStatus.Processing));
        Assert.False(PayRunStatuses.Transitions.CanMove(PayRunStatus.Processing, PayRunStatus.Calculated));
        Assert.False(PayRunStatuses.Transitions.CanMove(PayRunStatus.Paid, PayRunStatus.Canceled));
        Assert.True(PayRunStatuses.Transitions.IsTerminal(PayRunStatus.Canceled));
    }

    [Fact]
    public async Task SourceAccess_LimitsPayRunApprovalsToTheEmployersAUserMaySee()
    {
        await using var harness = await CreateHarnessAsync();
        var setup = await SetUpAsync(harness);

        var (clerk, admin, nobody) = await harness.RunAsync(async services =>
        {
            var access = services.GetServices<CrestApps.OrchardCore.SourceAccess.ISourceAccess>().Single(source => source.SourceKind == PayrollConstants.PayRunSubjectKind);

            return (
                await access.GetScopeAsync(CalendarTestHarness.User("clerk", PayrollPermissionTemplates.For(PayrollPermissions.ViewPayRuns, setup.EmployerId).Name), CrestApps.OrchardCore.SourceAccess.SourceAccessLevel.View),
                await access.GetScopeAsync(CalendarTestHarness.User("admin", PayrollPermissions.ApprovePayRuns.Name), CrestApps.OrchardCore.SourceAccess.SourceAccessLevel.Manage),
                await access.GetScopeAsync(CalendarTestHarness.User("nobody", PayrollPermissions.ViewPayRuns.Name), CrestApps.OrchardCore.SourceAccess.SourceAccessLevel.Manage));
        }, commit: false);

        Assert.Equal([setup.EmployerId], clerk.Scopes);
        Assert.True(admin.IsAll);
        Assert.False(nobody.IsAll);
        Assert.Empty(nobody.Scopes);
    }

    [Fact]
    public async Task RunPages_FollowTheEmployersScope_AndNobodyEntersTheirOwnPay()
    {
        await using var harness = await CreateHarnessAsync();
        var setup = await SetUpAsync(harness);
        var viewer = CalendarTestHarness.User("viewer", PayrollPermissionTemplates.For(PayrollPermissions.ViewPayRuns, setup.EmployerId).Name);
        var stranger = CalendarTestHarness.User("stranger", PayrollPermissionTemplates.For(PayrollPermissions.PreparePayRuns, "fabrikam").Name, PayrollPermissionTemplates.For(PayrollPermissions.ViewPayRuns, "fabrikam").Name);
        var self = CalendarTestHarness.User("lee-user", PayrollPermissions.PreparePayRuns.Name);

        await harness.RunAsync(async services =>
        {
            var staff = services.GetRequiredService<CrestApps.OrchardCore.Staff.Services.IStaffMemberManager>();
            var lee = await staff.FindByIdAsync(setup.StaffMemberId);

            lee.UserId = "lee-user";
            await staff.UpdateAsync(lee);
        });

        var (viewerDetails, viewerCalculate, strangerDetails, strangerIndex, selfInput) = await harness.RunAsync(async services => (
            await Controller(services, viewer).Details(setup.RunId),
            await Controller(services, viewer).Calculate(setup.RunId),
            await Controller(services, stranger).Details(setup.RunId),
            await Controller(services, stranger).Index(null),
            await Controller(services, self).AddInput(setup.RunId, new CrestApps.OrchardCore.Payroll.ViewModels.PayRunInputViewModel { StaffMemberId = setup.StaffMemberId, PayCodeId = setup.BonusCodeId, Amount = "1000" })), commit: false);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(viewerDetails);
        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(viewerCalculate);
        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(strangerDetails);
        // Another employer's clerk sees no employer the run list could show.
        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(strangerIndex);
        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(selfInput);
    }

    internal static CrestApps.OrchardCore.Payroll.Controllers.PayRunsController Controller(IServiceProvider services, ClaimsPrincipal user)
    {
        var shapes = new Moq.Mock<global::OrchardCore.DisplayManagement.IShapeFactory>();

        shapes
            .Setup(factory => factory.CreateAsync(Moq.It.IsAny<string>(), Moq.It.IsAny<Func<ValueTask<global::OrchardCore.DisplayManagement.IShape>>>(), Moq.It.IsAny<Action<global::OrchardCore.DisplayManagement.Implementation.ShapeCreatingContext>>(), Moq.It.IsAny<Action<global::OrchardCore.DisplayManagement.Implementation.ShapeCreatedContext>>()))
            .ReturnsAsync(Moq.Mock.Of<global::OrchardCore.DisplayManagement.IShape>());

        var controller = new CrestApps.OrchardCore.Payroll.Controllers.PayRunsController(
            services.GetRequiredService<IPayRunManager>(),
            services.GetRequiredService<IPayRunService>(),
            services.GetRequiredService<IPayRunInputManager>(),
            services.GetRequiredService<IPayrollScopeService>(),
            services.GetRequiredService<IPayProfileService>(),
            services.GetRequiredService<EmployerStore>(),
            services.GetRequiredService<IPayScheduleManager>(),
            services.GetRequiredService<IPayCodeManager>(),
            services.GetRequiredService<IPayProfileManager>(),
            services.GetRequiredService<CrestApps.OrchardCore.Payroll.Services.PayrollNames>(),
            Moq.Mock.Of<global::OrchardCore.DisplayManagement.ModelBinding.IUpdateModelAccessor>(),
            Moq.Mock.Of<global::OrchardCore.DisplayManagement.IDisplayManager<PayRun>>(),
            shapes.Object,
            Moq.Mock.Of<global::OrchardCore.DisplayManagement.Notify.INotifier>(),
            new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughHtmlLocalizer<CrestApps.OrchardCore.Payroll.Controllers.PayRunsController>(),
            new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<CrestApps.OrchardCore.Payroll.Controllers.PayRunsController>());

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

    internal sealed record Setup(string EmployerId, string StaffMemberId, string RunId, string RunNumber, string BonusCodeId);

    internal static async Task<Setup> SetUpAsync(CalendarTestHarness harness, bool withElection = true, Action<Employer> employerSetup = null, Func<EmployeePayProfile, Task> profileSetup = null)
    {
        var lee = await WorkforceTests.AddWorkerAsync(harness, "Lee Park", worker =>
        {
            worker.EmployeeNumber = "1001";
            worker.Put(new StaffPayRates { Rates = [new StaffPayRate { EffectiveFrom = new DateOnly(2020, 1, 1), HourlyRate = 20 }] });
        });

        await harness.RunAsync(async services =>
        {
            await services.GetRequiredService<PayCodeSeeder>().SeedAsync();
            await services.GetRequiredService<PayrollPayCodeSeeder>().SeedAsync();

            var policies = services.GetRequiredService<INamedCatalogManager<ApprovalPolicy>>();
            var policy = await policies.NewAsync();

            policy.Name = PayrollConstants.PayRunApprovalPolicy;
            policy.Title = "Pay run approval";
            policy.Approvers = [new ApproverRule { Kind = ApproverKind.Permission, Value = PayrollPermissions.ApprovePayRuns.Name }];
            policy.RequiredApprovals = 1;
            await policies.CreateAsync(policy);
        });

        return await harness.RunAsync(async services =>
        {
            var employers = services.GetRequiredService<IEmployerManager>();
            var employer = await employers.NewAsync();

            employer.Name = "Contoso";
            employer.Currency = "USD";
            employer.Registrations = [new EmployerTaxRegistration { Jurisdiction = "US" }];
            employerSetup?.Invoke(employer);
            await employers.CreateAsync(employer);

            var schedules = services.GetRequiredService<IPayScheduleManager>();
            var schedule = await schedules.NewAsync();

            schedule.Name = "Every two weeks";
            await schedules.CreateAsync(schedule);

            var rules = services.GetRequiredService<IPayrollTaxRuleManager>();
            var rule = await rules.NewAsync();

            rule.Name = "Social insurance";
            rule.TaxCode = "US-SS";
            rule.Jurisdiction = "US";
            rule.Category = PayrollTaxCategories.SocialInsurance;
            rule.Method = "Percentage";
            rule.Rate = 6.2m;
            rule.EffectiveFrom = new DateOnly(2026, 1, 1);
            await rules.CreateAsync(rule);

            var profiles = services.GetRequiredService<IPayProfileManager>();
            var profile = await profiles.NewAsync();

            profile.StaffMemberId = lee.ItemId;
            profile.EmployerId = employer.ItemId;
            profile.PayScheduleId = schedule.ItemId;
            profile.Distribution = [new NetPayRow { Method = NetPayMethod.Check, Split = NetPaySplit.Remainder }];

            if (withElection)
            {
                profile.Elections = [new TaxElection { Jurisdiction = "US", EffectiveFrom = new DateOnly(2020, 1, 1) }];
            }

            if (profileSetup is not null)
            {
                await profileSetup(profile);
            }

            await services.GetRequiredService<IPayProfileService>().SetTaxIdAsync(profile, "123456789");
            await profiles.CreateAsync(profile);

            var codes = services.GetRequiredService<IPayCodeManager>();
            var runs = services.GetRequiredService<IPayRunManager>();
            var run = await runs.NewAsync();

            run.EmployerId = employer.ItemId;
            run.PayScheduleId = schedule.ItemId;
            run.PeriodStart = new DateOnly(2026, 11, 1);
            run.PeriodEnd = new DateOnly(2026, 11, 14);
            run.PayDate = new DateOnly(2026, 11, 20);
            await runs.CreateAsync(run);

            var inputs = services.GetRequiredService<IPayRunInputManager>();
            var hours = await inputs.NewAsync();

            hours.RunId = run.ItemId;
            hours.StaffMemberId = lee.ItemId;
            hours.PayCodeId = (await codes.FindByCodeAsync("REG")).ItemId;
            hours.Hours = 80;
            await inputs.CreateAsync(hours);

            return new Setup(employer.ItemId, lee.ItemId, run.ItemId, run.Number, (await codes.FindByCodeAsync("BONUS")).ItemId);
        });
    }

    internal static Task<CalendarTestHarness> CreateHarnessAsync(Action<IServiceCollection> configure = null)
        => WorkforceTests.CreateAsync(
            services =>
            {
                services.AddDataProtection();
                services.AddTaxTablesCore();
                services.AddApprovalsCore();
                services.AddApprovalsHandlers();
                services.AddPayrollCore();
                services.AddPayrollTaxTablesCore();
                services.AddPayrollHandlers();
                services.AddSingleton<INamedCatalog<CrestApps.OrchardCore.Taxation.Models.TaxTable>>(new InMemoryNamedCatalog<CrestApps.OrchardCore.Taxation.Models.TaxTable>());
                services.AddSingleton<INamedCatalog<ApprovalPolicy>>(new InMemoryNamedCatalog<ApprovalPolicy>());
                services.AddSingleton<IApprovalRequestStore>(new InMemoryApprovalRequestStore());
                // Approvals commits the session as it does on a site: records read before must not be saved twice.
                services.AddScoped<IApprovalCommitter, YesSqlApprovalCommitter>();
                services.AddScoped<IApproverDirectory, PermissionApproverDirectory>();
                services.AddSingleton<IDistributedLock, KeyedDistributedLock>();
                services.AddSingleton<ILocalClock>(new FixedOffsetLocalClock(() => DateTime.UtcNow, TimeSpan.Zero));
                services.AddScoped<CrestApps.OrchardCore.Payroll.Services.PayrollNames>();
                configure?.Invoke(services);
            },
            async schemaBuilder =>
            {
                await PayrollIndexSchema.CreateAsync(schemaBuilder);
                await PayrollTaxRuleIndexSchema.CreateAsync(schemaBuilder);
                await GarnishmentOrderIndexSchema.CreateAsync(schemaBuilder);
                await PayrollFilingIndexSchema.CreateAsync(schemaBuilder);
            },
            [.. PayrollIndexSchema.Providers(), new PayrollTaxRuleIndexProvider(), new GarnishmentOrderIndexProvider(), new PayrollFilingIndexProvider(), new PayrollFilingRecordIndexProvider()]);

    /// <summary>
    /// Names approvers by the permission a rule gives, through the harness's claim authorization.
    /// </summary>
    private sealed class PermissionApproverDirectory : IApproverDirectory
    {
        private readonly IAuthorizationService _authorizationService;

        public PermissionApproverDirectory(IAuthorizationService authorizationService)
        {
            _authorizationService = authorizationService;
        }

        public async Task<bool> IsNamedAsync(ApproverRule rule, ApprovalRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
            => rule.Kind == ApproverKind.Permission && user?.Identity?.IsAuthenticated == true && await _authorizationService.AuthorizeAsync(user, new Permission(rule.Value));

        public Task<IReadOnlyList<string>> GetUserIdsAsync(ApproverRule rule, ApprovalRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);
    }
}
