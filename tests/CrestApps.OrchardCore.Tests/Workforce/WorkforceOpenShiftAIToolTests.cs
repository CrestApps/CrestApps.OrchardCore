using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.AI;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// The open shift and shift trade AI tools for managers, and the worker's own open shift tools: they refuse a caller who
/// is not signed in or lacks the permission, keep managers to the teams they manage and workers to their own account, and
/// act through the same services as the admin and My Work pages.
/// </summary>
public sealed class WorkforceOpenShiftAIToolTests
{
    private static readonly DateOnly _monday = PlanningKit.Monday;

    internal static readonly (string Name, bool RequiresApproval)[] ManagerTools =
    [
        (ListOpenShiftsTool.ToolName, false),
        (PostOpenShiftTool.ToolName, true),
        (DecideOpenShiftClaimTool.ToolName, true),
        (CancelOpenShiftTool.ToolName, false),
        (ListShiftTradesTool.ToolName, false),
        (CreateShiftTradeTool.ToolName, true),
        (DecideShiftTradeTool.ToolName, true),
    ];

    internal static readonly (string Name, bool RequiresApproval)[] SelfServiceTools =
    [
        (ListMyOpenShiftsTool.ToolName, false),
        (ClaimOpenShiftTool.ToolName, false),
    ];

    public static TheoryData<string> ManagerToolNames => Names(ManagerTools);

    public static TheoryData<string> SelfServiceToolNames => Names(SelfServiceTools);

    [Theory]
    [MemberData(nameof(ManagerToolNames))]
    public async Task ManagerTool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        await using var harness = await CreateAsync();

        var result = await InvokeAsync(harness, toolName, new ClaimsPrincipal(new ClaimsIdentity()), new() { ["openShiftId"] = "x" });

        Assert.Equal(WorkforceAdminToolBase.SignInRequired, result);
    }

    [Theory]
    [MemberData(nameof(ManagerToolNames))]
    public async Task ManagerTool_RefusesACallerWithoutThePermission(string toolName)
    {
        await using var harness = await CreateAsync();

        var result = await InvokeAsync(harness, toolName, CalendarTestHarness.User("someone", WorkforcePermissions.UseWorkforcePortal.Name), new() { ["openShiftId"] = "x" });

        Assert.StartsWith("The user isn't allowed to do this", result, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(SelfServiceToolNames))]
    public async Task SelfServiceTool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        await using var harness = await CreateAsync();

        var result = await InvokeAsync(harness, toolName, new ClaimsPrincipal(new ClaimsIdentity()), new() { ["openShiftId"] = "x" });

        Assert.Equal(WorkforceSelfServiceToolBase.SignInRequired, result);
    }

    [Theory]
    [MemberData(nameof(SelfServiceToolNames))]
    public async Task SelfServiceTool_RefusesACallerWithoutTheMyWorkPermission(string toolName)
    {
        await using var harness = await CreateAsync();

        var result = await InvokeAsync(harness, toolName, CalendarTestHarness.User("user-ana", WorkforcePermissions.ManageOpenShifts.Name), new() { ["openShiftId"] = "x" });

        Assert.Equal(WorkforceSelfServiceToolBase.NotAllowed, result);
    }

    [Theory]
    [MemberData(nameof(SelfServiceToolNames))]
    public async Task SelfServiceTool_RefusesACallerWithNoWorkerRecord(string toolName)
    {
        await using var harness = await CreateAsync();

        var result = await InvokeAsync(harness, toolName, CalendarTestHarness.User("nobody", WorkforcePermissions.UseWorkforcePortal.Name), new() { ["openShiftId"] = "x" });

        Assert.Equal(WorkforceSelfServiceToolBase.NoWorker, result);
    }

    [Fact]
    public async Task PostOpenShift_PostsAShiftForAManagedTeam_AndRefusesATeamTheCallerDoesNotManage()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);

        var posted = await InvokeAsync(harness, PostOpenShiftTool.ToolName, setup.Manager, new()
        {
            ["teamName"] = "Clinic",
            ["date"] = "2026-11-10",
            ["start"] = "08:00",
            ["end"] = "16:00",
            ["notes"] = "Cover for Sam",
        });
        var other = await InvokeAsync(harness, PostOpenShiftTool.ToolName, setup.Manager, new() { ["teamName"] = "Lab", ["date"] = "2026-11-10", ["start"] = "08:00", ["end"] = "16:00" });
        var shift = JsonElement.Parse(posted).GetProperty("openShift");
        var stored = await ListAsync(harness, setup.Clinic);

        Assert.True(JsonElement.Parse(posted).GetProperty("posted").GetBoolean());
        Assert.Equal("2026-11-10", shift.GetProperty("date").GetString());
        Assert.Equal("2026-11-10T08:00", shift.GetProperty("start").GetString());
        Assert.Equal("Cover for Sam", Assert.Single(stored).Notes);
        Assert.Contains("doesn't manage Lab", other, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostOpenShift_RefusesMissingTimes()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);

        var result = await InvokeAsync(harness, PostOpenShiftTool.ToolName, setup.Manager, new() { ["teamName"] = "Clinic", ["date"] = "2026-11-10" });

        Assert.StartsWith("Give when the shift starts and ends", result, StringComparison.Ordinal);
        Assert.Empty(await ListAsync(harness, setup.Clinic));
    }

    [Fact]
    public async Task ListOpenShifts_ListsTheManagedTeamsShifts_AndRefusesAnotherTeam()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);

        await PostAsync(harness, setup.Clinic, _monday);
        await PostAsync(harness, setup.Lab, _monday);

        var result = await InvokeAsync(harness, ListOpenShiftsTool.ToolName, setup.Manager, new());
        var lab = await InvokeAsync(harness, ListOpenShiftsTool.ToolName, setup.Manager, new() { ["teamName"] = "Lab" });
        var root = JsonElement.Parse(result);
        var item = Assert.Single(root.GetProperty("items").EnumerateArray());

        Assert.Equal("Clinic", item.GetProperty("team").GetString());
        Assert.Equal("Open", item.GetProperty("status").GetString());
        Assert.Equal(1, root.GetProperty("total").GetInt32());
        Assert.Contains("doesn't manage Lab", lab, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecideOpenShiftClaim_GivesTheShiftToAWorker_AndReportsAnUnknownShift()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);
        var shift = await PostAsync(harness, setup.Clinic, _monday);

        var result = await InvokeAsync(harness, DecideOpenShiftClaimTool.ToolName, setup.Manager, new() { ["openShiftId"] = shift.ItemId, ["decision"] = "Assign", ["workerId"] = setup.Ana.ItemId });
        var missing = await InvokeAsync(harness, DecideOpenShiftClaimTool.ToolName, setup.Manager, new() { ["openShiftId"] = "missing", ["decision"] = "Assign", ["workerId"] = setup.Ana.ItemId });
        var stored = await FindAsync(harness, shift.ItemId);

        Assert.Equal(OpenShiftStatus.Filled, stored.Status);
        Assert.Equal(setup.Ana.ItemId, stored.FilledWorkerId);
        Assert.Equal("Assign", JsonElement.Parse(result).GetProperty("decision").GetString());
        Assert.Equal("No open shift has the ID 'missing'.", missing);
    }

    [Fact]
    public async Task CancelOpenShift_CancelsAnOpenShift_AndRefusesItTwice()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);
        var shift = await PostAsync(harness, setup.Clinic, _monday);

        var result = await InvokeAsync(harness, CancelOpenShiftTool.ToolName, setup.Manager, new() { ["openShiftId"] = shift.ItemId });
        var again = await InvokeAsync(harness, CancelOpenShiftTool.ToolName, setup.Manager, new() { ["openShiftId"] = shift.ItemId });

        Assert.True(JsonElement.Parse(result).GetProperty("canceled").GetBoolean());
        Assert.Equal(OpenShiftStatus.Canceled, (await FindAsync(harness, shift.ItemId)).Status);
        Assert.Contains("can't be canceled", again, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancelOpenShift_DoesNotCancelAShiftOfATeamTheCallerDoesNotManage()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);
        var shift = await PostAsync(harness, setup.Lab, _monday);

        var result = await InvokeAsync(harness, CancelOpenShiftTool.ToolName, setup.Manager, new() { ["openShiftId"] = shift.ItemId });

        Assert.Equal("The user doesn't manage this open shift's team.", result);
        Assert.Equal(OpenShiftStatus.Open, (await FindAsync(harness, shift.ItemId)).Status);
    }

    [Fact]
    public async Task ClaimOpenShift_ClaimsForTheCallersOwnWorker_AndListMyOpenShiftsShowsTheClaim()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);
        var shift = await PostAsync(harness, setup.Clinic, _monday);
        var ana = CalendarTestHarness.User(setup.Ana.UserId, WorkforcePermissions.UseWorkforcePortal.Name);

        var before = await InvokeAsync(harness, ListMyOpenShiftsTool.ToolName, ana, new());
        var claimed = await InvokeAsync(harness, ClaimOpenShiftTool.ToolName, ana, new() { ["openShiftId"] = shift.ItemId });
        var after = await InvokeAsync(harness, ListMyOpenShiftsTool.ToolName, ana, new());
        var stored = await FindAsync(harness, shift.ItemId);

        Assert.True(Assert.Single(JsonElement.Parse(before).GetProperty("items").EnumerateArray()).GetProperty("canClaim").GetBoolean());
        Assert.Equal("Claim", JsonElement.Parse(claimed).GetProperty("action").GetString());
        Assert.Contains(stored.Claims, claim => claim.WorkerId == setup.Ana.ItemId && claim.Status == OpenShiftClaimStatus.Pending);
        Assert.True(Assert.Single(JsonElement.Parse(after).GetProperty("items").EnumerateArray()).GetProperty("claimedByMe").GetBoolean());
    }

    [Fact]
    public async Task ClaimOpenShift_ReportsAnUnknownShift_AndListsNothingForAWorkerOfAnotherTeam()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);

        await PostAsync(harness, setup.Clinic, _monday);

        var lee = CalendarTestHarness.User(setup.Lee.UserId, WorkforcePermissions.UseWorkforcePortal.Name);
        var missing = await InvokeAsync(harness, ClaimOpenShiftTool.ToolName, lee, new() { ["openShiftId"] = "missing" });
        var list = await InvokeAsync(harness, ListMyOpenShiftsTool.ToolName, lee, new());

        Assert.StartsWith("Nothing changed", missing, StringComparison.Ordinal);
        Assert.Empty(JsonElement.Parse(list).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task CreateShiftTrade_ListsTheWorkersShifts_ThenOffersOne_AndDecideShiftTradeCancelsIt()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);
        var anaShift = await PublishAnaShiftAsync(harness, setup);

        var choices = await InvokeAsync(harness, CreateShiftTradeTool.ToolName, setup.Manager, new() { ["workerId"] = setup.Ana.ItemId });
        var offered = await InvokeAsync(harness, CreateShiftTradeTool.ToolName, setup.Manager, new()
        {
            ["workerId"] = setup.Ana.ItemId,
            ["shiftId"] = anaShift.ItemId,
            ["targetWorkerId"] = setup.Ben.ItemId,
            ["note"] = "Dentist",
        });
        var tradeId = JsonElement.Parse(offered).GetProperty("trade").GetProperty("id").GetString();
        var listed = await InvokeAsync(harness, ListShiftTradesTool.ToolName, setup.Manager, new() { ["status"] = "Offered" });
        var canceled = await InvokeAsync(harness, DecideShiftTradeTool.ToolName, setup.Manager, new() { ["tradeId"] = tradeId, ["decision"] = "Cancel" });

        Assert.Equal(anaShift.ItemId, Assert.Single(JsonElement.Parse(choices).GetProperty("shifts").EnumerateArray()).GetProperty("shiftId").GetString());
        Assert.True(JsonElement.Parse(offered).GetProperty("offered").GetBoolean());
        Assert.Equal(tradeId, Assert.Single(JsonElement.Parse(listed).GetProperty("items").EnumerateArray()).GetProperty("id").GetString());
        Assert.Equal("Canceled", JsonElement.Parse(canceled).GetProperty("trade").GetProperty("status").GetString());
    }

    [Fact]
    public async Task CreateShiftTrade_RefusesAShiftThatIsNotTheWorkers()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);

        await PublishAnaShiftAsync(harness, setup);

        var result = await InvokeAsync(harness, CreateShiftTradeTool.ToolName, setup.Manager, new() { ["workerId"] = setup.Ana.ItemId, ["shiftId"] = "someone-elses" });

        Assert.False(JsonElement.Parse(result).GetProperty("offered").GetBoolean());
        Assert.Contains("isn't one of", JsonElement.Parse(result).GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecideShiftTrade_RefusesAnUnknownDecisionAndTrade()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);

        var decision = await InvokeAsync(harness, DecideShiftTradeTool.ToolName, setup.Manager, new() { ["tradeId"] = "x", ["decision"] = "Swap" });
        var missing = await InvokeAsync(harness, DecideShiftTradeTool.ToolName, setup.Manager, new() { ["tradeId"] = "missing", ["decision"] = "Approve" });

        Assert.Equal("The decision must be Approve, Decline, Cancel, Accept or Reject.", decision);
        Assert.Equal("No shift trade has the ID 'missing'.", missing);
    }

    [Theory]
    [MemberData(nameof(ManagerToolNames))]
    [MemberData(nameof(SelfServiceToolNames))]
    public void Tool_IsRegisteredWithItsTextsAndApproval(string toolName)
    {
        var tools = WorkforceTimeOffAdminAIToolTests.GetTools(typeof(OpenShiftAIToolsStartup))
            .Concat(WorkforceTimeOffAdminAIToolTests.GetTools(typeof(MyOpenShiftsAIToolsStartup)))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        var requiresApproval = ManagerTools.Concat(SelfServiceTools).Single(tool => tool.Name == toolName).RequiresApproval;

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
    public void Startups_RegisterSevenManagerToolsWithFourApprovals_AndTwoSelfServiceToolsBehindMyWork()
    {
        var manager = WorkforceTimeOffAdminAIToolTests.GetTools(typeof(OpenShiftAIToolsStartup));
        var selfService = WorkforceTimeOffAdminAIToolTests.GetTools(typeof(MyOpenShiftsAIToolsStartup));
        var required = typeof(MyOpenShiftsAIToolsStartup).GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false).Cast<RequireFeaturesAttribute>().Single();
        var feature = typeof(OpenShiftAIToolsStartup).GetCustomAttributes(typeof(FeatureAttribute), inherit: false).Cast<FeatureAttribute>().Single();

        Assert.Equal(7, manager.Count);
        Assert.Equal(4, manager.Values.Count(tool => tool.RequiresApproval));
        Assert.Equal(2, selfService.Count);
        Assert.DoesNotContain(selfService.Values, tool => tool.RequiresApproval);
        Assert.Contains(AIFeatureIds.Area, required.RequiredFeatureNames);
        Assert.Contains(WorkforceConstants.Features.Portal, required.RequiredFeatureNames);
        Assert.Equal(WorkforceConstants.Features.ShiftPlanning, feature.FeatureName);
        Assert.Empty(manager.Keys.Intersect(WorkforceTimeOffAdminAIToolTests.GetTools(typeof(ShiftPlanningAIToolsStartup)).Keys));
        Assert.Empty(selfService.Keys.Intersect(WorkforceTimeOffAdminAIToolTests.GetTools(typeof(CrestApps.OrchardCore.Workforce.AIToolsStartup)).Keys));
    }

    private static TheoryData<string> Names((string Name, bool RequiresApproval)[] tools)
    {
        var data = new TheoryData<string>();

        foreach (var (name, _) in tools)
        {
            data.Add(name);
        }

        return data;
    }

    private static AIFunction Create(string toolName)
        => toolName switch
        {
            ListOpenShiftsTool.ToolName => new ListOpenShiftsTool(),
            PostOpenShiftTool.ToolName => new PostOpenShiftTool(),
            DecideOpenShiftClaimTool.ToolName => new DecideOpenShiftClaimTool(),
            CancelOpenShiftTool.ToolName => new CancelOpenShiftTool(),
            ListShiftTradesTool.ToolName => new ListShiftTradesTool(),
            CreateShiftTradeTool.ToolName => new CreateShiftTradeTool(),
            DecideShiftTradeTool.ToolName => new DecideShiftTradeTool(),
            ListMyOpenShiftsTool.ToolName => new ListMyOpenShiftsTool(),
            ClaimOpenShiftTool.ToolName => new ClaimOpenShiftTool(),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, "Unknown tool."),
        };

    private static Task<string> InvokeAsync(CalendarTestHarness harness, string toolName, ClaimsPrincipal user, Dictionary<string, object> arguments)
        => WorkforceTimeOffAdminAIToolTests.InvokeAsync(harness, Create(toolName), user, arguments);

    private static Task<CalendarTestHarness> CreateAsync()
    {
        return WorkforceTests.CreateAsync(services =>
        {
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton(WorkforceTimeOffAdminAIToolTests.LocalClock(() => CalendarTestHarness.Now));
        });
    }

    private static async Task<Setup> SetUpAsync(CalendarTestHarness harness)
    {
        var clinic = await WorkforceTests.AddTeamAsync(harness, "Clinic", TeamKind.Team);
        var lab = await WorkforceTests.AddTeamAsync(harness, "Lab", TeamKind.Team);

        clinic = await PlanningKit.UpdateTeamAsync(harness, clinic, stored => stored.GetWorkforceSettings().OpenShiftClaimPolicy = OpenShiftClaimPolicy.ManagerApproves);
        lab = await PlanningKit.UpdateTeamAsync(harness, lab, stored => stored.GetWorkforceSettings().OpenShiftClaimPolicy = OpenShiftClaimPolicy.ManagerApproves);

        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", clinic);
        var ben = await PlanningKit.AddMemberAsync(harness, "Ben", clinic);
        var lee = await PlanningKit.AddMemberAsync(harness, "Lee", lab);

        await WorkforceTests.AddWorkerAsync(harness, "Maya", worker =>
        {
            worker.UserId = "maya";
            worker.TeamMemberships.Add(new TeamMembership { TeamId = clinic.ItemId, Role = TeamMemberRole.Manager });
        });

        var manager = CalendarTestHarness.User("maya", WorkforcePermissions.ManageOpenShifts.Name, WorkforcePermissions.ApproveShiftTrades.Name);

        return new Setup(clinic, lab, ana, ben, lee, manager);
    }

    private static Task<OpenShift> PostAsync(CalendarTestHarness harness, Team team, DateOnly date)
    {
        return harness.RunAsync(async services =>
        {
            var result = await services.GetRequiredService<IOpenShiftService>().PostAsync(new OpenShift
            {
                TeamId = team.ItemId,
                Date = date,
                Start = date.ToDateTime(new TimeOnly(9, 0)),
                End = date.ToDateTime(new TimeOnly(17, 0)),
            }, "tester");

            Assert.True(result.Succeeded, string.Join(" ", result.Errors));

            return result.Value;
        });
    }

    private static async Task<CalendarEvent> PublishAnaShiftAsync(CalendarTestHarness harness, Setup setup)
    {
        var template = await PlanningKit.AddTemplateAsync(harness, "Day", new TimeOnly(9, 0));
        var period = await PlanningKit.CreatePeriodAsync(harness, setup.Clinic);

        await PlanningKit.AddDraftAsync(harness, period, setup.Ana.ItemId, _monday, new TimeOnly(9, 0), new TimeOnly(17, 0), template.ItemId);
        await PlanningKit.PublishAsync(harness, period);

        return Assert.Single(await PlanningKit.ListShiftsAsync(harness, setup.Clinic), shift => PlanningKit.WorkerOf(shift) == setup.Ana.ItemId);
    }

    private static Task<IReadOnlyList<OpenShift>> ListAsync(CalendarTestHarness harness, Team team)
        => harness.RunAsync(async services => (await services.GetRequiredService<IOpenShiftService>().PageAsync(new OpenShiftQuery { TeamIds = [team.ItemId] }, 0, 50)).Items, commit: false);

    private static Task<OpenShift> FindAsync(CalendarTestHarness harness, string id)
        => harness.RunAsync(services => services.GetRequiredService<IOpenShiftService>().FindAsync(id), commit: false);

    private sealed record Setup(Team Clinic, Team Lab, StaffMember Ana, StaffMember Ben, StaffMember Lee, ClaimsPrincipal Manager);
}
