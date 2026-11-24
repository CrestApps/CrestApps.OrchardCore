using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.AI;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Portal;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// The time clock administration AI tools (batch 42): they refuse a caller who is not signed in or lacks the
/// permission, keep to the teams the caller manages, act through the same services as the Timesheets, Attendance and
/// worker time clock pages, and never return a kiosk PIN.
/// </summary>
public sealed class WorkforceTimeClockAdminAIToolTests
{
    private static readonly DateOnly _monday = TimeClockKit.Monday;

    internal static readonly (string Name, bool RequiresApproval)[] NewTools =
    [
        (ListTimesheetsTool.ToolName, false),
        (GetTimesheetTool.ToolName, false),
        (PrepareTimesheetsTool.ToolName, false),
        (DecideTimesheetTool.ToolName, true),
        (SaveTimesheetPunchTool.ToolName, false),
        (RemoveTimesheetPunchTool.ToolName, false),
        (AddTimesheetLineTool.ToolName, false),
        (RemoveTimesheetLineTool.ToolName, false),
        (DecidePunchCorrectionTool.ToolName, false),
        (ListAttendanceExceptionsTool.ToolName, false),
        (DecideAttendanceExceptionTool.ToolName, false),
        (GetWorkerTimeClockTool.ToolName, false),
    ];

    public static TheoryData<string> ToolNames
    {
        get
        {
            var data = new TheoryData<string>();

            foreach (var (name, _) in NewTools)
            {
                data.Add(name);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        await using var kit = await CreateAsync();

        var result = await InvokeAsync(kit, toolName, new ClaimsPrincipal(new ClaimsIdentity()), new() { ["timesheetId"] = "x" });

        Assert.Equal(WorkforceAdminToolBase.SignInRequired, result);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutThePermission(string toolName)
    {
        await using var kit = await CreateAsync();

        var result = await InvokeAsync(kit, toolName, CalendarTestHarness.User("someone", WorkforcePermissions.PunchTime.Name), new() { ["timesheetId"] = "x" });

        Assert.StartsWith("The user isn't allowed to do this", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SaveTimesheetPunchTool.ToolName)]
    [InlineData(RemoveTimesheetPunchTool.ToolName)]
    public async Task PunchTools_AlsoNeedEditPunches(string toolName)
    {
        await using var kit = await CreateAsync();

        var result = await InvokeAsync(kit, toolName, CalendarTestHarness.User("someone", WorkforcePermissions.ApproveTimesheets.Name), new() { ["timesheetId"] = "x" });

        Assert.Contains(WorkforcePermissions.EditPunches.Name, result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListTimesheets_ListsTheTimesheetsOfTheTeamsTheCallerManages_AndRefusesAnUnknownStatus()
    {
        await using var kit = await CreateAsync();
        var setup = await SetUpAsync(kit);

        await kit.WorkAsync(setup.Ana, _monday, 9, 17);
        await kit.WorkAsync(setup.Ben, _monday, 9, 17);
        await kit.TimesheetAsync(setup.Ana);
        await kit.TimesheetAsync(setup.Ben);

        var result = await InvokeAsync(kit, ListTimesheetsTool.ToolName, setup.Manager, new());
        var invalid = await InvokeAsync(kit, ListTimesheetsTool.ToolName, setup.Manager, new() { ["status"] = "Paid" });
        var item = Assert.Single(JsonElement.Parse(result).GetProperty("items").EnumerateArray());

        Assert.Equal("Ana", item.GetProperty("worker").GetString());
        Assert.Equal(8m, item.GetProperty("totalHours").GetDecimal());
        Assert.StartsWith("The status must be one of", invalid, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTimesheet_GivesTheDaysPunchesAndTotals_ByWorkerAndDate_AndRefusesAnUnknownId()
    {
        await using var kit = await CreateAsync();
        var setup = await SetUpAsync(kit);

        await kit.WorkAsync(setup.Ana, _monday, 9, 17);
        await kit.TimesheetAsync(setup.Ana);

        var result = await InvokeAsync(kit, GetTimesheetTool.ToolName, setup.Manager, new() { ["workerName"] = "Ana", ["date"] = "2026-11-10" });
        var missing = await InvokeAsync(kit, GetTimesheetTool.ToolName, setup.Manager, new() { ["timesheetId"] = "missing" });
        var root = JsonElement.Parse(result);
        var day = Assert.Single(root.GetProperty("days").EnumerateArray());

        Assert.Equal("2026-11-09", day.GetProperty("date").GetString());
        Assert.Equal(["In", "Out"], day.GetProperty("punches").EnumerateArray().Select(punch => punch.GetProperty("kind").GetString()));
        Assert.Equal("2026-11-09T09:00", day.GetProperty("punches")[0].GetProperty("time").GetString());
        Assert.Contains(root.GetProperty("totals").EnumerateArray(), total => total.GetProperty("code").GetString() == "REG" && total.GetProperty("hours").GetDecimal() == 8m);
        Assert.Contains(root.GetProperty("payCodes").EnumerateArray(), code => code.GetProperty("code").GetString() == "HOL");
        Assert.Equal("No timesheet has the ID 'missing'.", missing);
    }

    [Fact]
    public async Task PrepareTimesheets_CreatesTheMissingTimesheets_AndRefusesATeamTheCallerDoesNotManage()
    {
        await using var kit = await CreateAsync();
        var setup = await SetUpAsync(kit);

        kit.Now = TimeClockKit.Local(_monday.AddDays(1), 12);

        var result = await InvokeAsync(kit, PrepareTimesheetsTool.ToolName, setup.Manager, new() { ["date"] = "2026-11-10" });
        var other = await InvokeAsync(kit, PrepareTimesheetsTool.ToolName, setup.Manager, new() { ["teamName"] = "Warehouse" });
        var made = await kit.Harness.RunAsync(async services => (await services.GetRequiredService<ITimesheetService>().PageAsync(new TimesheetQuery { WorkerId = setup.Ana.ItemId }, 0, 10)).Items, commit: false);

        Assert.True(JsonElement.Parse(result).GetProperty("ready").GetInt32() >= 1);
        Assert.Contains(made, timesheet => timesheet.PeriodStart <= new DateOnly(2026, 11, 10) && new DateOnly(2026, 11, 10) <= timesheet.PeriodEnd);
        Assert.Contains("doesn't manage Warehouse", other, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecideTimesheet_ApprovesATimesheet_AndRefusesToSendOneBackWithoutAReason()
    {
        await using var kit = await CreateAsync();
        var setup = await SetUpAsync(kit);

        await kit.WorkAsync(setup.Ana, _monday, 9, 17);

        var timesheet = await kit.TimesheetAsync(setup.Ana);
        var approved = await InvokeAsync(kit, DecideTimesheetTool.ToolName, setup.Manager, new() { ["timesheetIds"] = new[] { timesheet.ItemId }, ["decision"] = "Approve" });
        var noReason = await InvokeAsync(kit, DecideTimesheetTool.ToolName, setup.Manager, new() { ["timesheetIds"] = new[] { timesheet.ItemId }, ["decision"] = "Reject" });
        var stored = await kit.Harness.RunAsync(services => services.GetRequiredService<ITimesheetService>().FindAsync(timesheet.ItemId), commit: false);

        Assert.Equal(1, JsonElement.Parse(approved).GetProperty("decided").GetInt32());
        Assert.Equal(TimesheetStatus.Approved, stored.Status);
        Assert.StartsWith("Say what the worker must fix", noReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecideTimesheet_DoesNotDecideATimesheetOfATeamTheCallerDoesNotManage()
    {
        await using var kit = await CreateAsync();
        var setup = await SetUpAsync(kit);

        await kit.WorkAsync(setup.Ben, _monday, 9, 17);

        var timesheet = await kit.TimesheetAsync(setup.Ben);
        var result = await InvokeAsync(kit, DecideTimesheetTool.ToolName, setup.Manager, new() { ["timesheetIds"] = new[] { timesheet.ItemId }, ["decision"] = "Approve" });
        var stored = await kit.Harness.RunAsync(services => services.GetRequiredService<ITimesheetService>().FindAsync(timesheet.ItemId), commit: false);
        var error = Assert.Single(JsonElement.Parse(result).GetProperty("results").EnumerateArray()).GetProperty("error").GetString();

        Assert.Contains("doesn't manage", error, StringComparison.Ordinal);
        Assert.NotEqual(TimesheetStatus.Approved, stored.Status);
    }

    [Fact]
    public async Task SaveTimesheetPunch_AddsAForgottenClockOut_InTheWorkersTimeZone()
    {
        await using var kit = await CreateAsync();
        var setup = await SetUpAsync(kit);

        await kit.PunchAtAsync(setup.Ana, TimePunchKind.In, _monday, 9);
        kit.Now = TimeClockKit.Local(_monday.AddDays(1), 9);

        var timesheet = await kit.TimesheetAsync(setup.Ana);
        var result = await InvokeAsync(kit, SaveTimesheetPunchTool.ToolName, setup.Manager, new()
        {
            ["timesheetId"] = timesheet.ItemId,
            ["kind"] = "Out",
            ["date"] = "2026-11-09",
            ["time"] = "17:30",
            ["reason"] = "Forgot to clock out",
        });
        var punches = await PunchesAsync(kit, setup.Ana);
        var after = await kit.TimesheetAsync(setup.Ana);

        Assert.Equal("Added", JsonElement.Parse(result).GetProperty("action").GetString());
        Assert.Contains(punches, punch => punch.Kind == TimePunchKind.Out && punch.Utc == TimeClockKit.Local(_monday, 17, 30) && punch.Reason == "Forgot to clock out");
        Assert.Equal(8.5m, kit.Hours(after, "REG") + kit.Hours(after, "OT"));
    }

    [Fact]
    public async Task SaveTimesheetPunch_RefusesToCorrectAPunchOfAnotherWorker()
    {
        await using var kit = await CreateAsync();
        var setup = await SetUpAsync(kit);
        var otherPunch = await kit.PunchAtAsync(setup.Cara, TimePunchKind.In, _monday, 9);

        await kit.PunchAtAsync(setup.Ana, TimePunchKind.In, _monday, 9);

        var timesheet = await kit.TimesheetAsync(setup.Ana);
        var result = await InvokeAsync(kit, SaveTimesheetPunchTool.ToolName, setup.Manager, new()
        {
            ["timesheetId"] = timesheet.ItemId,
            ["punchId"] = otherPunch.ItemId,
            ["kind"] = "In",
            ["date"] = "2026-11-09",
            ["time"] = "08:00",
            ["reason"] = "Wrong time",
        });

        Assert.StartsWith("The timesheet's worker has no punch with the ID", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemoveTimesheetPunch_RemovesAPunch_AndNeedsAReason()
    {
        await using var kit = await CreateAsync();
        var setup = await SetUpAsync(kit);

        await kit.WorkAsync(setup.Ana, _monday, 9, 17);

        var punches = await PunchesAsync(kit, setup.Ana);
        var timesheet = await kit.TimesheetAsync(setup.Ana);
        var outPunch = punches.Single(punch => punch.Kind == TimePunchKind.Out);
        var noReason = await InvokeAsync(kit, RemoveTimesheetPunchTool.ToolName, setup.Manager, new() { ["timesheetId"] = timesheet.ItemId, ["punchId"] = outPunch.ItemId });
        var result = await InvokeAsync(kit, RemoveTimesheetPunchTool.ToolName, setup.Manager, new() { ["timesheetId"] = timesheet.ItemId, ["punchId"] = outPunch.ItemId, ["reason"] = "Punched by mistake" });
        var after = await PunchesAsync(kit, setup.Ana);

        Assert.StartsWith("Say why the punch is removed", noReason, StringComparison.Ordinal);
        Assert.Equal(outPunch.ItemId, JsonElement.Parse(result).GetProperty("removedPunchId").GetString());
        Assert.DoesNotContain(after, punch => punch.Kind == TimePunchKind.Out);
    }

    [Fact]
    public async Task TimesheetLines_AreAddedAndRemoved_AndAnUnknownLineIsRefused()
    {
        await using var kit = await CreateAsync();
        var setup = await SetUpAsync(kit);

        await kit.WorkAsync(setup.Ana, _monday, 9, 17);

        var timesheet = await kit.TimesheetAsync(setup.Ana);
        var added = await InvokeAsync(kit, AddTimesheetLineTool.ToolName, setup.Manager, new()
        {
            ["timesheetId"] = timesheet.ItemId,
            ["date"] = "2026-11-10",
            ["payCodeId"] = kit.Codes["HOL"].ItemId,
            ["hours"] = 4,
            ["note"] = "Holiday cover",
        });
        var lineId = JsonElement.Parse(added).GetProperty("lineId").GetString();
        var withLine = await kit.TimesheetAsync(setup.Ana);
        var unknown = await InvokeAsync(kit, RemoveTimesheetLineTool.ToolName, setup.Manager, new() { ["timesheetId"] = timesheet.ItemId, ["lineId"] = "missing" });
        var removed = await InvokeAsync(kit, RemoveTimesheetLineTool.ToolName, setup.Manager, new() { ["timesheetId"] = timesheet.ItemId, ["lineId"] = lineId });
        var withoutLine = await kit.TimesheetAsync(setup.Ana);

        Assert.False(string.IsNullOrEmpty(lineId));
        Assert.Equal(4m, kit.Hours(withLine, "HOL"));
        Assert.StartsWith("The line wasn't removed", unknown, StringComparison.Ordinal);
        Assert.Equal(lineId, JsonElement.Parse(removed).GetProperty("removedLineId").GetString());
        Assert.Equal(0m, kit.Hours(withoutLine, "HOL"));
    }

    [Fact]
    public async Task DecidePunchCorrection_ApprovesAWorkersCorrection_AndRefusesAnUnknownOne()
    {
        await using var kit = await CreateAsync();
        var setup = await SetUpAsync(kit);

        await kit.PunchAtAsync(setup.Ana, TimePunchKind.In, _monday, 9);
        kit.Now = TimeClockKit.Local(_monday.AddDays(2), 9);

        var correction = await kit.Harness.RunAsync(services => services.GetRequiredService<IAttendanceService>().RequestCorrectionAsync(setup.Ana, TimePunchKind.Out, TimeClockKit.Local(_monday, 17), "Forgot to clock out."));
        var unknown = await InvokeAsync(kit, DecidePunchCorrectionTool.ToolName, setup.Manager, new() { ["correctionId"] = "missing", ["decision"] = "Approve" });
        var result = await InvokeAsync(kit, DecidePunchCorrectionTool.ToolName, setup.Manager, new() { ["correctionId"] = correction.Value.ItemId, ["decision"] = "Approve" });
        var after = await kit.TimesheetAsync(setup.Ana);

        Assert.StartsWith("No waiting punch correction", unknown, StringComparison.Ordinal);
        Assert.Equal("Approve", JsonElement.Parse(result).GetProperty("decision").GetString());
        Assert.Equal(8m, kit.Hours(after, "REG"));
    }

    [Fact]
    public async Task AttendanceExceptions_AreListedWithPoints_AndExcused()
    {
        await using var kit = await CreateAsync();
        var setup = await SetUpAsync(kit);

        await kit.PunchAtAsync(setup.Ana, TimePunchKind.In, _monday, 9);
        kit.Now = TimeClockKit.Local(_monday.AddDays(2), 9);
        await kit.TimesheetAsync(setup.Ana);

        var listed = await InvokeAsync(kit, ListAttendanceExceptionsTool.ToolName, setup.Manager, new());
        var item = Assert.Single(JsonElement.Parse(listed).GetProperty("items").EnumerateArray());
        var unknownKind = await InvokeAsync(kit, ListAttendanceExceptionsTool.ToolName, setup.Manager, new() { ["kind"] = "Sleepy" });
        var unknownId = await InvokeAsync(kit, DecideAttendanceExceptionTool.ToolName, setup.Manager, new() { ["exceptionIds"] = new[] { "missing" }, ["decision"] = "Confirm" });
        var excused = await InvokeAsync(kit, DecideAttendanceExceptionTool.ToolName, setup.Manager, new() { ["exceptionIds"] = new[] { item.GetProperty("id").GetString() }, ["decision"] = "Excuse", ["note"] = "Doctor" });
        var open = await kit.ExceptionsAsync(setup.Ana);

        Assert.Equal("MissedPunch", item.GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Number, item.GetProperty("workerPoints").ValueKind);
        Assert.StartsWith("The kind must be one of", unknownKind, StringComparison.Ordinal);
        Assert.False(JsonElement.Parse(unknownId).GetProperty("results")[0].GetProperty("changed").GetBoolean());
        Assert.Equal(1, JsonElement.Parse(excused).GetProperty("decided").GetInt32());
        Assert.Equal(AttendanceExceptionStatus.Excused, Assert.Single(open).Status);
    }

    [Fact]
    public async Task GetWorkerTimeClock_SaysWhetherThereIsAPin_WithoutReturningIt()
    {
        await using var kit = await CreateAsync();
        var setup = await SetUpAsync(kit);
        var set = await kit.Harness.RunAsync(services => services.GetRequiredService<ITimeClockService>().SetPinAsync(setup.Ana, "2468"));
        var stored = await kit.Harness.RunAsync(services => services.GetRequiredService<IStaffMemberManager>().FindByIdAsync(setup.Ana.ItemId).AsTask(), commit: false);

        var result = await InvokeAsync(kit, GetWorkerTimeClockTool.ToolName, setup.Manager, new() { ["workerId"] = setup.Ana.ItemId });
        var missing = await InvokeAsync(kit, GetWorkerTimeClockTool.ToolName, setup.Manager, new() { ["workerId"] = "missing" });
        var root = JsonElement.Parse(result);

        Assert.True(set.Succeeded, string.Join(" ", set.Errors));
        Assert.True(root.GetProperty("hasPin").GetBoolean());
        Assert.Equal(kit.Policy.Name, root.GetProperty("policy").GetProperty("name").GetString());
        Assert.DoesNotContain("2468", result, StringComparison.Ordinal);
        Assert.True(stored.TryGet<StaffBadge>(out var badge));
        Assert.DoesNotContain(badge.PinHash, result, StringComparison.Ordinal);
        Assert.Contains("No worker has the ID", missing, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public void Tool_IsRegisteredWithItsTextsAndApproval(string toolName)
    {
        var tools = WorkforceTimeOffAdminAIToolTests.GetTools(typeof(TimeClockAdminAIToolsStartup));
        var requiresApproval = NewTools.Single(tool => tool.Name == toolName).RequiresApproval;

        Assert.True(tools.TryGetValue(toolName, out var tool), $"The tool '{toolName}' is not registered.");
        Assert.Equal(toolName, tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Equal("Workforce", tool.Category);
        Assert.True(tool.IsSelectable());
        Assert.Equal(requiresApproval, tool.RequiresApproval);
        Assert.False(string.IsNullOrWhiteSpace(Create(toolName).Description));
        Assert.Equal(toolName, Create(toolName).Name);
    }

    [Fact]
    public void NewStartup_RegistersTwelveToolsWithOneApproval_OnTheTimeClockFeature_AndNoDroppedTool()
    {
        var tools = WorkforceTimeOffAdminAIToolTests.GetTools(typeof(TimeClockAdminAIToolsStartup));
        var required = typeof(TimeClockAdminAIToolsStartup).GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false).Cast<RequireFeaturesAttribute>().Single();
        var feature = typeof(TimeClockAdminAIToolsStartup).GetCustomAttributes(typeof(FeatureAttribute), inherit: false).Cast<FeatureAttribute>().Single();

        Assert.Equal(12, tools.Count);
        Assert.Equal([DecideTimesheetTool.ToolName], tools.Values.Where(tool => tool.RequiresApproval).Select(tool => tool.Name));
        Assert.Contains(AIFeatureIds.Area, required.RequiredFeatureNames);
        Assert.Equal(WorkforceConstants.Features.TimeClock, feature.FeatureName);
        Assert.Empty(tools.Keys.Intersect(["lockPayPeriod", "runPayrollExport", "listPayrollExports", "setWorkerTimeClockPin", "renewTimeClockKioskAddress", "saveWorkerPayRate", "removeWorkerPayRate"]));
    }

    private static AIFunction Create(string toolName)
        => toolName switch
        {
            ListTimesheetsTool.ToolName => new ListTimesheetsTool(),
            GetTimesheetTool.ToolName => new GetTimesheetTool(),
            PrepareTimesheetsTool.ToolName => new PrepareTimesheetsTool(),
            DecideTimesheetTool.ToolName => new DecideTimesheetTool(),
            SaveTimesheetPunchTool.ToolName => new SaveTimesheetPunchTool(),
            RemoveTimesheetPunchTool.ToolName => new RemoveTimesheetPunchTool(),
            AddTimesheetLineTool.ToolName => new AddTimesheetLineTool(),
            RemoveTimesheetLineTool.ToolName => new RemoveTimesheetLineTool(),
            DecidePunchCorrectionTool.ToolName => new DecidePunchCorrectionTool(),
            ListAttendanceExceptionsTool.ToolName => new ListAttendanceExceptionsTool(),
            DecideAttendanceExceptionTool.ToolName => new DecideAttendanceExceptionTool(),
            GetWorkerTimeClockTool.ToolName => new GetWorkerTimeClockTool(),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, "Unknown tool."),
        };

    private static Task<string> InvokeAsync(TimeClockKit kit, string toolName, ClaimsPrincipal user, Dictionary<string, object> arguments)
        => WorkforceTimeOffAdminAIToolTests.InvokeAsync(kit.Harness, Create(toolName), user, arguments);

    private static async Task<TimeClockKit> CreateAsync()
    {
        TimeClockKit kit = null;

        kit = await TimeClockKit.CreateAsync(services =>
        {
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton(WorkforceTimeOffAdminAIToolTests.LocalClock(() => kit?.Now ?? CalendarTestHarness.Now));
            services.AddScoped<TimesheetViews>();
        });

        return kit;
    }

    private static Task<IReadOnlyList<TimePunch>> PunchesAsync(TimeClockKit kit, StaffMember worker)
        => kit.Harness.RunAsync(services => services.GetRequiredService<ITimeClockService>().ListAsync(worker.ItemId, TimeClockKit.Local(_monday, 0), TimeClockKit.Local(_monday.AddDays(7), 0)), commit: false);

    private static async Task<Setup> SetUpAsync(TimeClockKit kit)
    {
        var team = await kit.AddTeamAsync("Front desk");
        var warehouse = await WorkforceTests.AddTeamAsync(kit.Harness, "Warehouse", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(kit.Harness, "Ana", team, worker => worker.EmployeeNumber = "4410");
        var cara = await PlanningKit.AddMemberAsync(kit.Harness, "Cara", team);
        var ben = await PlanningKit.AddMemberAsync(kit.Harness, "Ben", warehouse);

        // Ben's team has the same time clock policy, so he has timesheets too, in a team the manager doesn't manage.
        await PlanningKit.UpdateTeamAsync(kit.Harness, warehouse, stored => stored.GetWorkforceSettings().TimeClockPolicyId = kit.Policy.ItemId);
        await WorkforceTests.AddWorkerAsync(kit.Harness, "Maya", worker =>
        {
            worker.UserId = "maya";
            worker.TeamMemberships.Add(new TeamMembership { TeamId = team.ItemId, Role = TeamMemberRole.Manager });
        });

        var manager = CalendarTestHarness.User(
            "maya",
            WorkforcePermissions.ApproveTimesheets.Name,
            WorkforcePermissions.EditPunches.Name,
            WorkforcePermissions.ManageWorkers.Name);

        return new Setup(team, ana, ben, cara, manager);
    }

    private sealed record Setup(Team Team, StaffMember Ana, StaffMember Ben, StaffMember Cara, ClaimsPrincipal Manager);
}
