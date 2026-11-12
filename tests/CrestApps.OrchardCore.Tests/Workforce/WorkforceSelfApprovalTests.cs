using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Api;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// SEC-P6: a manager's scope is the teams they manage, and nothing excluded their own worker record, so a manager with
/// Approve timesheets, Edit punches or Approve time off approved their own timesheet (which goes to payroll), corrected
/// their own punches and approved their own leave. A decision on one's own record now needs Manage the whole workforce or
/// the new Approve own records permission.
/// </summary>
public sealed class WorkforceSelfApprovalTests
{
    private static readonly DateOnly _monday = TimeClockKit.Monday;

    [Fact]
    public async Task AManager_CantApproveTheirOwnTimesheet_ButAPeerManagerCan()
    {
        // Arrange
        await using var kit = await TimeClockKit.CreateAsync();
        var team = await kit.AddTeamAsync("Front desk");
        var maya = await PlanningKit.AddMemberAsync(kit.Harness, "Maya", team, worker => worker.TeamMemberships[0].Role = TeamMemberRole.Manager);
        await PlanningKit.AddMemberAsync(kit.Harness, "Ben", team, worker => worker.TeamMemberships[0].Role = TeamMemberRole.Manager);
        await kit.WorkAsync(maya, _monday, 9, 17);
        var timesheet = await kit.TimesheetAsync(maya);

        // Act
        var own = await kit.Harness.RunAsync(services => WorkforceApiEndpoints.ApproveTimesheetAsync(timesheet.ItemId, Context(services, CalendarTestHarness.User("user-maya", WorkforcePermissions.ApproveTimesheets.Name)), services));
        var peer = await kit.Harness.RunAsync(services => WorkforceApiEndpoints.ApproveTimesheetAsync(timesheet.ItemId, Context(services, CalendarTestHarness.User("user-ben", WorkforcePermissions.ApproveTimesheets.Name)), services));

        // Assert
        Assert.Equal(403, Status(own));
        Assert.Equal(200, Status(peer));
    }

    [Theory]
    [InlineData("ManageAllWorkforce")]
    [InlineData("ApproveOwnWorkforceRecords")]
    public async Task AManagerWithTheRightPermission_ApprovesTheirOwnTimesheet(string permission)
    {
        // Arrange
        await using var kit = await TimeClockKit.CreateAsync();
        var team = await kit.AddTeamAsync("Front desk");
        var maya = await PlanningKit.AddMemberAsync(kit.Harness, "Maya", team, worker => worker.TeamMemberships[0].Role = TeamMemberRole.Manager);
        await kit.WorkAsync(maya, _monday, 9, 17);
        var timesheet = await kit.TimesheetAsync(maya);

        // Act
        var own = await kit.Harness.RunAsync(services => WorkforceApiEndpoints.ApproveTimesheetAsync(timesheet.ItemId, Context(services, CalendarTestHarness.User("user-maya", WorkforcePermissions.ApproveTimesheets.Name, permission)), services));

        // Assert
        Assert.Equal(200, Status(own));
    }

    [Fact]
    public async Task CanDecideForWorker_RefusesOnlyTheCallersOwnRecord()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var team = await WorkforceTests.AddTeamAsync(harness, "East", TeamKind.Team);
        var maya = await PlanningKit.AddMemberAsync(harness, "Maya", team, worker => worker.TeamMemberships[0].Role = TeamMemberRole.Manager);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);

        // Act
        var (own, other, ownWithPermission, ownAsAdministrator) = await harness.RunAsync(async services =>
        {
            var scope = services.GetRequiredService<IWorkforceScopeService>();

            return (
                await scope.CanDecideForWorkerAsync(CalendarTestHarness.User("user-maya"), maya.ItemId),
                await scope.CanDecideForWorkerAsync(CalendarTestHarness.User("user-maya"), ana.ItemId),
                await scope.CanDecideForWorkerAsync(CalendarTestHarness.User("user-maya", WorkforcePermissions.ApproveOwnRecords.Name), maya.ItemId),
                await scope.CanDecideForWorkerAsync(CalendarTestHarness.User("user-maya", WorkforcePermissions.ManageAllWorkforce.Name), maya.ItemId));
        }, commit: false);

        // Assert
        Assert.False(own);
        Assert.True(other);
        Assert.True(ownWithPermission);
        Assert.True(ownAsAdministrator);
    }

    private static DefaultHttpContext Context(IServiceProvider services, System.Security.Claims.ClaimsPrincipal user)
        => new() { User = user, RequestServices = services };

    private static int Status(IResult result)
        => result switch
        {
            IStatusCodeHttpResult { StatusCode: { } code } => code,
            ForbidHttpResult => 403,
            _ => 0,
        };
}
