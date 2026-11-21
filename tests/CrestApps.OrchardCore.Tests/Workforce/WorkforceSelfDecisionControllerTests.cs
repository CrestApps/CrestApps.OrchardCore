using System.Security.Claims;
using CrestApps.Core;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Controllers;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// SEC-P6, still open after the first round: the self-approval check covered timesheets, punches and time off requests,
/// but a manager could still add hours to their own time-off balance, assign themselves a policy, and approve their own
/// predictability pay. Each now needs Manage the whole workforce or Approve own records on the caller's own record.
/// </summary>
public sealed class WorkforceSelfDecisionControllerTests
{
    [Fact]
    public async Task AManager_CantAdjustTheirOwnBalance_ButAPeerManagerCan()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var (maya, _, policy) = await SetUpAsync(harness);
        var adjustment = new TimeOffAdjustViewModel { PolicyId = policy.ItemId, Kind = nameof(TimeOffLedgerKind.Adjustment), Amount = 40, EffectiveDate = new DateOnly(2026, 11, 2) };

        // Act
        var own = await harness.RunAsync(services => TimeOff(services, "user-maya", WorkforcePermissions.AdjustTimeOffBalances.Name).Adjust(maya.ItemId, adjustment));
        var peer = await harness.RunAsync(services => TimeOff(services, "user-ben", WorkforcePermissions.AdjustTimeOffBalances.Name).Adjust(maya.ItemId, adjustment));

        // Assert: only the peer's change is in Maya's ledger.
        Assert.IsType<ForbidResult>(own);
        Assert.IsType<RedirectToActionResult>(peer);
        Assert.Single(await TimeOffKit.LedgerAsync(harness, maya, policy));
    }

    [Fact]
    public async Task AManager_CantAssignThemselvesAPolicy_UnlessTheyMayApproveTheirOwnRecords()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var (maya, _, policy) = await SetUpAsync(harness);
        var assignment = new TimeOffAssignViewModel { PolicyId = policy.ItemId };

        // Act
        var own = await harness.RunAsync(services => TimeOff(services, "user-maya", WorkforcePermissions.ManageTimeOffPolicies.Name).Assign(maya.ItemId, assignment));
        var unassign = await harness.RunAsync(services => TimeOff(services, "user-maya", WorkforcePermissions.ManageTimeOffPolicies.Name).Unassign(maya.ItemId, policy.ItemId));
        var allowed = await harness.RunAsync(services => TimeOff(services, "user-maya", WorkforcePermissions.ManageTimeOffPolicies.Name, WorkforcePermissions.ApproveOwnRecords.Name).Assign(maya.ItemId, assignment));

        // Assert
        Assert.IsType<ForbidResult>(own);
        Assert.IsType<ForbidResult>(unassign);
        Assert.IsType<RedirectToActionResult>(allowed);
    }

    [Fact]
    public async Task AManager_CantApproveTheirOwnPredictabilityPay_ButAPeerManagerCan()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var (maya, team, _) = await SetUpAsync(harness);
        var entry = await harness.RunAsync(async services =>
        {
            var created = new PredictabilityPayEntry { ItemId = "pay-maya", WorkerId = maya.ItemId, TeamId = team.ItemId, Hours = 4 };

            await services.GetRequiredService<PredictabilityPayStore>().CreateAsync(created);

            return created;
        });

        // Act
        var own = await harness.RunAsync(services => Pay(services, "user-maya").Approve(entry.ItemId));
        var stillPending = await harness.RunAsync(async services => (await services.GetRequiredService<IPredictabilityPayService>().FindAsync(entry.ItemId)).Status, commit: false);
        var peer = await harness.RunAsync(services => Pay(services, "user-ben").Approve(entry.ItemId));

        // Assert
        Assert.IsType<ForbidResult>(own);
        Assert.Equal(PredictabilityPayStatus.Pending, stillPending);
        Assert.IsType<RedirectToActionResult>(peer);
    }

    private static async Task<(StaffMember Maya, Team Team, TimeOffPolicy Policy)> SetUpAsync(CalendarTestHarness harness)
    {
        var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var maya = await PlanningKit.AddMemberAsync(harness, "Maya", team, worker => worker.TeamMemberships[0].Role = TeamMemberRole.Manager);

        await PlanningKit.AddMemberAsync(harness, "Ben", team, worker => worker.TeamMemberships[0].Role = TeamMemberRole.Manager);

        var type = await TimeOffKit.AddTypeAsync(harness, "Vacation");
        var policy = await TimeOffKit.AddPolicyAsync(harness, type, team);

        return (maya, team, policy);
    }

    private static TimeOffController TimeOff(IServiceProvider services, string userId, params string[] permissions)
        => WithUser(
            ActivatorUtilities.CreateInstance<TimeOffController>(services, Mock.Of<INotifier>(), Mock.Of<IHtmlLocalizer<TimeOffController>>(), Mock.Of<ILocalClock>()),
            services,
            CalendarTestHarness.User(userId, permissions));

    private static PredictabilityPayController Pay(IServiceProvider services, string userId)
        => WithUser(
            ActivatorUtilities.CreateInstance<PredictabilityPayController>(services, Mock.Of<INotifier>(), Mock.Of<IHtmlLocalizer<PredictabilityPayController>>()),
            services,
            CalendarTestHarness.User(userId, WorkforcePermissions.ApprovePredictabilityPay.Name));

    private static TController WithUser<TController>(TController controller, IServiceProvider services, ClaimsPrincipal user)
        where TController : Controller
    {
        var httpContext = new DefaultHttpContext { User = user, RequestServices = services };

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.Url = Mock.Of<IUrlHelper>();
        controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        return controller;
    }

    private static Task<CalendarTestHarness> CreateAsync()
    {
        return TimeOffKit.CreateAsync(services => services.AddWorkforceLaborRulesCore());
    }
}
