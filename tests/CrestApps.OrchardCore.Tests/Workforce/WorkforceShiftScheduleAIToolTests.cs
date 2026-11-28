using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.AI;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// The shift schedule and labor rule AI tools: they refuse a caller who is not signed in or lacks the permission, keep
/// to the teams the caller manages, and act through the same services as the Shift Schedules, Rule Overrides and
/// Predictability Pay pages.
/// </summary>
public sealed class WorkforceShiftScheduleAIToolTests
{
    internal static readonly (string Name, bool RequiresApproval)[] ScheduleTools =
    [
        (ListShiftSchedulesTool.ToolName, false),
        (GetShiftScheduleTool.ToolName, false),
        (CreateShiftScheduleTool.ToolName, false),
        (SaveDraftShiftTool.ToolName, false),
        (DeleteDraftShiftTool.ToolName, false),
        (FillShiftScheduleTool.ToolName, false),
        (PublishShiftScheduleTool.ToolName, true),
        (ChangePublishedShiftTool.ToolName, true),
        (DeleteShiftScheduleTool.ToolName, true),
    ];

    internal static readonly (string Name, bool RequiresApproval)[] LaborRuleTools =
    [
        (ListLaborRuleOverridesTool.ToolName, false),
        (ListPredictabilityPayTool.ToolName, false),
        (DecidePredictabilityPayTool.ToolName, true),
    ];

    public static TheoryData<string> ToolNames
    {
        get
        {
            var data = new TheoryData<string>();

            foreach (var (name, _) in ScheduleTools.Concat(LaborRuleTools))
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
        await using var harness = await CreateAsync();

        var result = await InvokeAsync(harness, toolName, new ClaimsPrincipal(new ClaimsIdentity()), new() { ["scheduleId"] = "x" });

        Assert.Equal(WorkforceAdminToolBase.SignInRequired, result);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutThePermission(string toolName)
    {
        await using var harness = await CreateAsync();

        var result = await InvokeAsync(harness, toolName, CalendarTestHarness.User("someone", WorkforcePermissions.UseWorkforcePortal.Name), new() { ["scheduleId"] = "x" });

        Assert.StartsWith("The user isn't allowed to do this", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListShiftSchedules_ListsTheSchedulesOfTheTeamsTheCallerManages()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);
        var manager = await AddManagerAsync(harness, setup.East);

        await PlanningKit.CreatePeriodAsync(harness, setup.West);

        var result = await InvokeAsync(harness, ListShiftSchedulesTool.ToolName, manager, new());
        var west = await InvokeAsync(harness, ListShiftSchedulesTool.ToolName, manager, new() { ["teamName"] = "West" });
        var root = JsonElement.Parse(result);
        var item = Assert.Single(root.GetProperty("items").EnumerateArray());

        Assert.Equal(setup.Period.ItemId, item.GetProperty("id").GetString());
        Assert.Equal("East", item.GetProperty("team").GetString());
        Assert.Equal("Draft", item.GetProperty("status").GetString());
        Assert.Equal("2026-11-09", item.GetProperty("startDate").GetString());
        Assert.Contains("doesn't manage West", west, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetShiftSchedule_GivesTheDraftShifts_AndRefusesAnotherTeamsSchedule()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);
        var manager = await AddManagerAsync(harness, setup.East);
        var westPeriod = await PlanningKit.CreatePeriodAsync(harness, setup.West);

        await PlanningKit.AddDraftAsync(harness, setup.Period, setup.Ana.ItemId, PlanningKit.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0));
        await PlanningKit.AddDraftAsync(harness, setup.Period, null, PlanningKit.Monday.AddDays(1), new TimeOnly(22, 0), new TimeOnly(6, 0));

        var result = await InvokeAsync(harness, GetShiftScheduleTool.ToolName, manager, new() { ["scheduleId"] = setup.Period.ItemId, ["includeChanges"] = true });
        var other = await InvokeAsync(harness, GetShiftScheduleTool.ToolName, manager, new() { ["scheduleId"] = westPeriod.ItemId });
        var missing = await InvokeAsync(harness, GetShiftScheduleTool.ToolName, manager, new() { ["scheduleId"] = "missing" });
        var root = JsonElement.Parse(result);
        var shifts = root.GetProperty("shifts").EnumerateArray().ToArray();

        Assert.Equal(2, shifts.Length);
        Assert.Equal("Ana", shifts[0].GetProperty("worker").GetString());
        Assert.Equal("09:00", shifts[0].GetProperty("start").GetString());
        Assert.True(shifts[1].GetProperty("open").GetBoolean());
        Assert.True(shifts[1].GetProperty("endsNextDay").GetBoolean());
        Assert.Equal(1, root.GetProperty("openShiftCount").GetInt32());
        Assert.Equal(0, root.GetProperty("changes").GetProperty("total").GetInt32());
        Assert.Contains("doesn't manage West", other, StringComparison.Ordinal);
        Assert.Contains("No schedule has the ID", missing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateShiftSchedule_StartsADraft_AndReportsTheServicesRefusal()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);
        var manager = await AddManagerAsync(harness, setup.East);

        var created = await InvokeAsync(harness, CreateShiftScheduleTool.ToolName, manager, new() { ["teamName"] = "East", ["startDate"] = "2026-11-23", ["weeks"] = 1, ["name"] = "Late November" });
        var tooLong = await InvokeAsync(harness, CreateShiftScheduleTool.ToolName, manager, new() { ["teamName"] = "East", ["startDate"] = "2026-12-07", ["weeks"] = 40 });
        var west = await InvokeAsync(harness, CreateShiftScheduleTool.ToolName, manager, new() { ["teamName"] = "West", ["startDate"] = "2026-11-23" });
        var root = JsonElement.Parse(created);

        Assert.True(root.GetProperty("created").GetBoolean());
        Assert.Equal("2026-11-29", root.GetProperty("endDate").GetString());
        Assert.Equal("Draft", root.GetProperty("status").GetString());
        Assert.StartsWith("The schedule was not created", tooLong, StringComparison.Ordinal);
        Assert.Contains("doesn't manage West", west, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveDraftShift_AddsAShift_ChangesOnlyWhatIsGiven_AndRefusesAnUnknownBreak()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);
        var manager = await AddManagerAsync(harness, setup.East);

        var added = await InvokeAsync(harness, SaveDraftShiftTool.ToolName, manager, new()
        {
            ["scheduleId"] = setup.Period.ItemId,
            ["workerId"] = setup.Ana.ItemId,
            ["date"] = "2026-11-10",
            ["start"] = "08:00",
            ["end"] = "16:00",
            ["notes"] = "Front desk",
        });
        var shiftId = JsonElement.Parse(added).GetProperty("shift").GetProperty("id").GetString();
        var changed = await InvokeAsync(harness, SaveDraftShiftTool.ToolName, manager, new() { ["scheduleId"] = setup.Period.ItemId, ["shiftId"] = shiftId, ["end"] = "14:00" });
        var badBreak = await InvokeAsync(harness, SaveDraftShiftTool.ToolName, manager, new() { ["scheduleId"] = setup.Period.ItemId, ["shiftId"] = shiftId, ["breaks"] = new[] { "12:00-12:30 Siesta" } });
        var stored = Assert.Single(await PlanningKit.ListDraftsAsync(harness, setup.Period));
        var shift = JsonElement.Parse(changed).GetProperty("shift");

        Assert.True(JsonElement.Parse(added).GetProperty("created").GetBoolean());
        Assert.Equal("08:00", shift.GetProperty("start").GetString());
        Assert.Equal("14:00", shift.GetProperty("end").GetString());
        Assert.Equal("Front desk", stored.Notes);
        Assert.Equal(setup.Ana.ItemId, stored.WorkerId);
        Assert.Equal(new DateTime(2026, 11, 10, 14, 0, 0), stored.End);
        Assert.Contains("aren't understood", badBreak, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteDraftShift_RemovesTheShift_AndReportsAnUnknownOne()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);
        var manager = await AddManagerAsync(harness, setup.East);
        var draft = await PlanningKit.AddDraftAsync(harness, setup.Period, setup.Ana.ItemId, PlanningKit.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0));

        var deleted = await InvokeAsync(harness, DeleteDraftShiftTool.ToolName, manager, new() { ["scheduleId"] = setup.Period.ItemId, ["shiftId"] = draft.ItemId });
        var missing = await InvokeAsync(harness, DeleteDraftShiftTool.ToolName, manager, new() { ["scheduleId"] = setup.Period.ItemId, ["shiftId"] = draft.ItemId });

        Assert.True(JsonElement.Parse(deleted).GetProperty("deleted").GetBoolean());
        Assert.Empty(await PlanningKit.ListDraftsAsync(harness, setup.Period));
        Assert.Contains("has no draft shift", missing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FillShiftSchedule_CopiesAnotherScheduleOfTheTeam_AndRefusesAMissingSource()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);
        var manager = await AddManagerAsync(harness, setup.East);

        await PlanningKit.AddDraftAsync(harness, setup.Period, setup.Ana.ItemId, PlanningKit.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0));

        var next = await harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().CreatePeriodAsync(setup.East.ItemId, PlanningKit.Monday.AddDays(14), 1, "Later", "tester"));
        var copied = await InvokeAsync(harness, FillShiftScheduleTool.ToolName, manager, new() { ["scheduleId"] = next.Value.ItemId, ["method"] = "Copy", ["sourceScheduleId"] = setup.Period.ItemId });
        var noSource = await InvokeAsync(harness, FillShiftScheduleTool.ToolName, manager, new() { ["scheduleId"] = next.Value.ItemId, ["method"] = "Copy" });
        var badMethod = await InvokeAsync(harness, FillShiftScheduleTool.ToolName, manager, new() { ["scheduleId"] = next.Value.ItemId, ["method"] = "Guess" });

        Assert.Equal(1, JsonElement.Parse(copied).GetProperty("shiftsAdded").GetInt32());
        Assert.Equal(PlanningKit.Monday.AddDays(14), Assert.Single(await PlanningKit.ListDraftsAsync(harness, next.Value)).Date);
        Assert.StartsWith("Give another schedule of the same team", noSource, StringComparison.Ordinal);
        Assert.Equal("The method must be Rotations, Copy or AutoSchedule.", badMethod);
    }

    [Fact]
    public async Task PublishShiftSchedule_PublishesTheDraft_AndNeedsTheOverridePermissionForAReason()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);
        var manager = await AddManagerAsync(harness, setup.East, WorkforcePermissions.PublishSchedules.Name);

        await PlanningKit.AddDraftAsync(harness, setup.Period, setup.Ana.ItemId, PlanningKit.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0));

        var withReason = await InvokeAsync(harness, PublishShiftScheduleTool.ToolName, manager, new() { ["scheduleId"] = setup.Period.ItemId, ["overrideReason"] = "Short staffed" });
        var published = await InvokeAsync(harness, PublishShiftScheduleTool.ToolName, manager, new() { ["scheduleId"] = setup.Period.ItemId });
        var root = JsonElement.Parse(published);

        Assert.StartsWith("The user isn't allowed to do this: it needs the 'OverrideLaborRules' permission.", withReason, StringComparison.Ordinal);
        Assert.True(root.GetProperty("published").GetBoolean());
        Assert.Equal(1, root.GetProperty("added").GetInt32());
        Assert.Single(await PlanningKit.ListShiftsAsync(harness, setup.East));
    }

    [Fact]
    public async Task ChangePublishedShift_TakesAPublishedShiftOff_AndRefusesADraftOnlyShift()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);
        var manager = await AddManagerAsync(harness, setup.East);
        var draft = await PlanningKit.AddDraftAsync(harness, setup.Period, setup.Ana.ItemId, PlanningKit.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0));

        await PlanningKit.PublishAsync(harness, setup.Period);

        var later = await PlanningKit.AddDraftAsync(harness, setup.Period, setup.Ana.ItemId, PlanningKit.Monday.AddDays(2), new TimeOnly(9, 0), new TimeOnly(17, 0));
        var draftOnly = await InvokeAsync(harness, ChangePublishedShiftTool.ToolName, manager, new() { ["scheduleId"] = setup.Period.ItemId, ["shiftId"] = later.ItemId, ["operation"] = "Remove" });
        var removed = await InvokeAsync(harness, ChangePublishedShiftTool.ToolName, manager, new() { ["scheduleId"] = setup.Period.ItemId, ["shiftId"] = draft.ItemId, ["operation"] = "Remove", ["reason"] = "Closed for the holiday" });

        Assert.StartsWith("This shift isn't on the published schedule", draftOnly, StringComparison.Ordinal);
        Assert.True(JsonElement.Parse(removed).GetProperty("changed").GetBoolean());
        Assert.Empty(await PlanningKit.ListShiftsAsync(harness, setup.East));
    }

    [Fact]
    public async Task DeleteShiftSchedule_DeletesADraftSchedule_ButNotAPublishedOne()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);
        var manager = await AddManagerAsync(harness, setup.East);
        var draftPeriod = await harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().CreatePeriodAsync(setup.East.ItemId, PlanningKit.Monday.AddDays(14), 1, "Later", "tester"));

        await PlanningKit.AddDraftAsync(harness, setup.Period, setup.Ana.ItemId, PlanningKit.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0));
        await PlanningKit.PublishAsync(harness, setup.Period);

        var deleted = await InvokeAsync(harness, DeleteShiftScheduleTool.ToolName, manager, new() { ["scheduleId"] = draftPeriod.Value.ItemId });
        var refused = await InvokeAsync(harness, DeleteShiftScheduleTool.ToolName, manager, new() { ["scheduleId"] = setup.Period.ItemId });
        var gone = await harness.RunAsync(services => services.GetRequiredService<ISchedulePlanningService>().FindPeriodAsync(draftPeriod.Value.ItemId), commit: false);

        Assert.True(JsonElement.Parse(deleted).GetProperty("deleted").GetBoolean());
        Assert.Null(gone);
        Assert.StartsWith("A published schedule can't be deleted", refused, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListLaborRuleOverrides_ListsTheOverridesOfTheCallersTeams()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);
        var manager = await AddManagerAsync(harness, setup.East);

        await RecordOverrideAsync(harness, setup.East, setup.Ana, "Short staffed");
        await RecordOverrideAsync(harness, setup.West, setup.Ben, "Other team");

        var result = await InvokeAsync(harness, ListLaborRuleOverridesTool.ToolName, manager, new());
        var item = Assert.Single(JsonElement.Parse(result).GetProperty("items").EnumerateArray());

        Assert.Equal("Short staffed", item.GetProperty("reason").GetString());
        Assert.Equal("Ana", item.GetProperty("worker").GetString());
        Assert.Equal(LaborRuleKeys.MinRest, item.GetProperty("ruleKey").GetString());
    }

    [Fact]
    public async Task PredictabilityPay_IsListedAndDecided_InTheCallersTeamsOnly()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);
        var manager = await AddManagerAsync(harness, setup.East);

        await AddPayAsync(harness, "pay-ana", setup.East, setup.Ana);
        await AddPayAsync(harness, "pay-ben", setup.West, setup.Ben);

        var listed = await InvokeAsync(harness, ListPredictabilityPayTool.ToolName, manager, new());
        var noReason = await InvokeAsync(harness, DecidePredictabilityPayTool.ToolName, manager, new() { ["entryIds"] = new[] { "pay-ana" }, ["decision"] = "Waive" });
        var decided = await InvokeAsync(harness, DecidePredictabilityPayTool.ToolName, manager, new() { ["entryIds"] = new[] { "pay-ana", "pay-ben", "missing" }, ["decision"] = "Approve" });
        var item = Assert.Single(JsonElement.Parse(listed).GetProperty("items").EnumerateArray());
        var results = JsonElement.Parse(decided).GetProperty("results").EnumerateArray().ToArray();
        var ana = await harness.RunAsync(services => services.GetRequiredService<IPredictabilityPayService>().FindAsync("pay-ana"), commit: false);
        var ben = await harness.RunAsync(services => services.GetRequiredService<IPredictabilityPayService>().FindAsync("pay-ben"), commit: false);

        Assert.Equal("pay-ana", item.GetProperty("id").GetString());
        Assert.Equal(4m, item.GetProperty("hoursOwed").GetDecimal());
        Assert.StartsWith("Give the reason", noReason, StringComparison.Ordinal);
        Assert.True(results[0].GetProperty("changed").GetBoolean());
        Assert.Contains("doesn't manage", results[1].GetProperty("error").GetString(), StringComparison.Ordinal);
        Assert.False(results[2].GetProperty("changed").GetBoolean());
        Assert.Equal(1, JsonElement.Parse(decided).GetProperty("decided").GetInt32());
        Assert.Equal(PredictabilityPayStatus.Approved, ana.Status);
        Assert.Equal(PredictabilityPayStatus.Pending, ben.Status);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public void Tool_IsRegisteredWithItsTextsAndApproval(string toolName)
    {
        var isSchedule = ScheduleTools.Any(tool => tool.Name == toolName);
        var tools = GetTools(isSchedule ? typeof(ShiftScheduleAIToolsStartup) : typeof(LaborRulesAIToolsStartup));
        var requiresApproval = ScheduleTools.Concat(LaborRuleTools).Single(tool => tool.Name == toolName).RequiresApproval;

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
    public void Startups_RegisterTheirToolsOnTheirFeatures_WithTheCatalogsApprovals_AndLeaveTheExistingToolsAlone()
    {
        var schedule = GetTools(typeof(ShiftScheduleAIToolsStartup));
        var laborRules = GetTools(typeof(LaborRulesAIToolsStartup));
        var existing = GetTools(typeof(ShiftPlanningAIToolsStartup));

        Assert.Equal(9, schedule.Count);
        Assert.Equal(3, schedule.Values.Count(tool => tool.RequiresApproval));
        Assert.Equal(3, laborRules.Count);
        Assert.Equal(1, laborRules.Values.Count(tool => tool.RequiresApproval));
        Assert.Equal(WorkforceConstants.Features.ShiftPlanning, FeatureOf(typeof(ShiftScheduleAIToolsStartup)));
        Assert.Equal(WorkforceConstants.Features.LaborRules, FeatureOf(typeof(LaborRulesAIToolsStartup)));
        Assert.Contains(AIFeatureIds.Area, RequiredFeaturesOf(typeof(ShiftScheduleAIToolsStartup)));
        Assert.Contains(AIFeatureIds.Area, RequiredFeaturesOf(typeof(LaborRulesAIToolsStartup)));
        Assert.Equal([ExplainViolationsTool.ToolName, FindCoverTool.ToolName], existing.Keys.Order(StringComparer.Ordinal));
        Assert.Empty(schedule.Keys.Intersect(existing.Keys));
    }

    [Fact]
    public void LaborRuleSetKind_IsRegisteredReadOnly_WithTheLaborRulesPermission_AndProjectsNamedFields()
    {
        var kinds = GetKinds(typeof(LaborRulesAIToolsStartup));
        var kind = Assert.IsType<AIRecordKind<LaborRuleSet>>(kinds["laborRuleSet"]);
        var set = new LaborRuleSet
        {
            ItemId = "set-1",
            Name = "California",
            Rules = [new LaborRuleInstance { Id = "r1", RuleKey = LaborRuleKeys.MinRest, Enforcement = LaborRuleEnforcement.Block, Parameters = new Dictionary<string, string> { ["Hours"] = "10" } }],
        };
        var projected = JsonSerializer.SerializeToElement(kind.Projection(set));

        Assert.False(kind.CanWrite);
        Assert.Equal(WorkforcePermissions.ManageLaborRules.Name, kind.Permission.Name);
        Assert.Equal("California", projected.GetProperty("name").GetString());
        Assert.Equal("Block", projected.GetProperty("rules")[0].GetProperty("enforcement").GetString());
        Assert.Equal("10", projected.GetProperty("rules")[0].GetProperty("parameters").GetProperty("Hours").GetString());
    }

    internal static AIFunction Create(string toolName)
        => toolName switch
        {
            ListShiftSchedulesTool.ToolName => new ListShiftSchedulesTool(),
            GetShiftScheduleTool.ToolName => new GetShiftScheduleTool(),
            CreateShiftScheduleTool.ToolName => new CreateShiftScheduleTool(),
            SaveDraftShiftTool.ToolName => new SaveDraftShiftTool(),
            DeleteDraftShiftTool.ToolName => new DeleteDraftShiftTool(),
            FillShiftScheduleTool.ToolName => new FillShiftScheduleTool(),
            PublishShiftScheduleTool.ToolName => new PublishShiftScheduleTool(),
            ChangePublishedShiftTool.ToolName => new ChangePublishedShiftTool(),
            DeleteShiftScheduleTool.ToolName => new DeleteShiftScheduleTool(),
            ListLaborRuleOverridesTool.ToolName => new ListLaborRuleOverridesTool(),
            ListPredictabilityPayTool.ToolName => new ListPredictabilityPayTool(),
            DecidePredictabilityPayTool.ToolName => new DecidePredictabilityPayTool(),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, "Unknown tool."),
        };

    private static IReadOnlyDictionary<string, AIToolDefinitionEntry> GetTools(Type startupType)
    {
        using var provider = Configure(startupType);

        return provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value.Tools;
    }

    private static IReadOnlyDictionary<string, AIRecordKind> GetKinds(Type startupType)
    {
        using var provider = Configure(startupType);

        return provider.GetRequiredService<IOptions<AIRecordKindOptions>>().Value.Kinds;
    }

    private static ServiceProvider Configure(Type startupType)
    {
        var services = new ServiceCollection();
        var localizer = Activator.CreateInstance(typeof(PassThroughLocalizer<>).MakeGenericType(startupType));
        var startup = (StartupBase)Activator.CreateInstance(startupType, localizer);

        startup.ConfigureServices(services);

        return services.BuildServiceProvider();
    }

    private static string FeatureOf(Type startupType)
        => startupType.GetCustomAttributes(typeof(FeatureAttribute), inherit: false).Cast<FeatureAttribute>().Single().FeatureName;

    private static string[] RequiredFeaturesOf(Type startupType)
        => startupType.GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false).Cast<RequireFeaturesAttribute>().Single().RequiredFeatureNames.ToArray();

    private static Task<string> InvokeAsync(CalendarTestHarness harness, string toolName, ClaimsPrincipal user, Dictionary<string, object> arguments)
        => WorkforceTimeOffAdminAIToolTests.InvokeAsync(harness, Create(toolName), user, arguments);

    private static Task<CalendarTestHarness> CreateAsync()
    {
        return WorkforceTests.CreateAsync(services =>
        {
            services.AddWorkforceTimeOffCore();
            services.AddWorkforceLaborRulesCore();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton(WorkforceTimeOffAdminAIToolTests.LocalClock(() => CalendarTestHarness.Now));
        });
    }

    private static async Task<ClaimsPrincipal> AddManagerAsync(CalendarTestHarness harness, Team team, params string[] extraPermissions)
    {
        await WorkforceTests.AddWorkerAsync(harness, "Maya", worker =>
        {
            worker.UserId = "maya";
            worker.TeamMemberships.Add(new TeamMembership { TeamId = team.ItemId, Role = TeamMemberRole.Manager });
        });

        return CalendarTestHarness.User(
            "maya",
            new[]
            {
                WorkforcePermissions.ManageSchedules.Name,
                WorkforcePermissions.ManageLaborRules.Name,
                WorkforcePermissions.ApprovePredictabilityPay.Name,
            }.Concat(extraPermissions).ToArray());
    }

    private static Task<IReadOnlyList<LaborRuleOverride>> RecordOverrideAsync(CalendarTestHarness harness, Team team, StaffMember worker, string reason)
    {
        return harness.RunAsync(services => services.GetRequiredService<ILaborRuleOverrideService>().RecordAsync(
            [new LaborRuleViolation { RuleKey = LaborRuleKeys.MinRest, RuleName = "Minimum rest", WorkerId = worker.ItemId, Enforcement = LaborRuleEnforcement.RequireOverride, Message = "Only 8 hours between shifts." }],
            reason,
            "Schedule",
            null,
            team.ItemId,
            "maya"));
    }

    private static Task AddPayAsync(CalendarTestHarness harness, string id, Team team, StaffMember worker)
    {
        return harness.RunAsync(services => services.GetRequiredService<PredictabilityPayStore>().CreateAsync(new PredictabilityPayEntry
        {
            ItemId = id,
            WorkerId = worker.ItemId,
            TeamId = team.ItemId,
            ChangeKind = ScheduleChangeKind.Removed,
            ShiftStartUtc = PlanningKit.Utc(PlanningKit.Monday, new TimeOnly(9, 0)),
            NoticeHours = 20,
            Hours = 4,
            CreatedUtc = CalendarTestHarness.Now,
        }).AsTask());
    }

    private static async Task<Setup> SetUpAsync(CalendarTestHarness harness)
    {
        var east = await WorkforceTests.AddTeamAsync(harness, "East", TeamKind.Team);
        var west = await WorkforceTests.AddTeamAsync(harness, "West", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", east);
        var ben = await PlanningKit.AddMemberAsync(harness, "Ben", west);
        var period = await PlanningKit.CreatePeriodAsync(harness, east);

        return new Setup(east, west, ana, ben, period);
    }

    private sealed record Setup(Team East, Team West, StaffMember Ana, StaffMember Ben, SchedulePeriod Period);

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
