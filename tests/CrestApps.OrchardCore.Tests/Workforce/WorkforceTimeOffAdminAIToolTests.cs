using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.AI.Tooling;
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
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// The time-off administration AI tools (batch 41): they refuse a caller who is not signed in or lacks the permission,
/// keep to the teams the caller manages, and act through the same services as the admin pages.
/// </summary>
public sealed class WorkforceTimeOffAdminAIToolTests
{
    private static readonly DateOnly _tuesday = PlanningKit.Monday.AddDays(1);

    internal static readonly (string Name, bool RequiresApproval)[] NewTools =
    [
        (ListTimeOffRequestsTool.ToolName, false),
        (DecideTimeOffRequestTool.ToolName, true),
        (GetWorkerTimeOffTool.ToolName, false),
        (AdjustTimeOffBalanceTool.ToolName, true),
        (AssignTimeOffPolicyTool.ToolName, false),
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
        await using var harness = await CreateAsync();

        var result = await InvokeAsync(harness, toolName, new ClaimsPrincipal(new ClaimsIdentity()), new() { ["workerId"] = "x" });

        Assert.Equal(WorkforceAdminToolBase.SignInRequired, result);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutThePermission(string toolName)
    {
        await using var harness = await CreateAsync();

        var result = await InvokeAsync(harness, toolName, CalendarTestHarness.User("someone", WorkforcePermissions.RequestTimeOff.Name), new() { ["workerId"] = "x" });

        Assert.StartsWith("The user isn't allowed to do this", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListTimeOffRequests_ListsTheWaitingRequestsOfTheTeamsTheCallerManages()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);
        var manager = await AddManagerAsync(harness, setup.East);

        await TimeOffKit.SubmitAsync(harness, setup.Ana, setup.Vacation, _tuesday);
        await TimeOffKit.SubmitAsync(harness, setup.Ben, setup.Vacation, _tuesday);

        var result = await InvokeAsync(harness, ListTimeOffRequestsTool.ToolName, manager, new());
        var west = await InvokeAsync(harness, ListTimeOffRequestsTool.ToolName, manager, new() { ["teamName"] = "West" });
        var root = JsonElement.Parse(result);
        var item = Assert.Single(root.GetProperty("items").EnumerateArray());

        Assert.Equal("Ana", item.GetProperty("worker").GetString());
        Assert.Equal("Pending", item.GetProperty("status").GetString());
        Assert.Equal("2026-11-10", item.GetProperty("startDate").GetString());
        Assert.Equal(1, root.GetProperty("total").GetInt32());
        Assert.Contains("doesn't manage West", west, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecideTimeOffRequest_ApprovesARequest_AndReportsAnUnknownOne()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);
        var manager = await AddManagerAsync(harness, setup.East);
        var submitted = await TimeOffKit.SubmitAsync(harness, setup.Ana, setup.Vacation, _tuesday);

        var result = await InvokeAsync(harness, DecideTimeOffRequestTool.ToolName, manager, new()
        {
            ["requestIds"] = new[] { submitted.Value.ItemId, "missing" },
            ["decision"] = "Approve",
            ["note"] = "Enjoy",
        });
        var stored = await TimeOffKit.FindAsync(harness, submitted.Value.ItemId);
        var results = JsonElement.Parse(result).GetProperty("results").EnumerateArray().ToArray();

        Assert.Equal(TimeOffRequestStatus.Approved, stored.Status);
        Assert.True(results[0].GetProperty("changed").GetBoolean());
        Assert.False(results[1].GetProperty("changed").GetBoolean());
        Assert.Equal(1, JsonElement.Parse(result).GetProperty("decided").GetInt32());
    }

    [Fact]
    public async Task DecideTimeOffRequest_DoesNotDecideARequestOfATeamTheCallerDoesNotManage()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);
        var manager = await AddManagerAsync(harness, setup.East);
        var submitted = await TimeOffKit.SubmitAsync(harness, setup.Ben, setup.Vacation, _tuesday);

        var result = await InvokeAsync(harness, DecideTimeOffRequestTool.ToolName, manager, new() { ["requestIds"] = new[] { submitted.Value.ItemId }, ["decision"] = "Decline" });

        var error = Assert.Single(JsonElement.Parse(result).GetProperty("results").EnumerateArray()).GetProperty("error").GetString();

        Assert.Contains("doesn't manage", error, StringComparison.Ordinal);
        Assert.Equal(TimeOffRequestStatus.Pending, (await TimeOffKit.FindAsync(harness, submitted.Value.ItemId)).Status);
    }

    [Fact]
    public async Task GetWorkerTimeOff_GivesTheBalancesAndLedger_AndRefusesAnUnknownWorker()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);
        var manager = await AddManagerAsync(harness, setup.East);

        var result = await InvokeAsync(harness, GetWorkerTimeOffTool.ToolName, manager, new() { ["workerName"] = "Ana", ["date"] = "2026-11-02" });
        var missing = await InvokeAsync(harness, GetWorkerTimeOffTool.ToolName, manager, new() { ["workerId"] = "missing" });
        var root = JsonElement.Parse(result);
        var balance = Assert.Single(root.GetProperty("balances").EnumerateArray());

        Assert.Equal("Vacation", balance.GetProperty("policy").GetString());
        Assert.Equal(40m, balance.GetProperty("earned").GetDecimal());
        Assert.Equal(1, root.GetProperty("ledger").GetProperty("total").GetInt32());
        Assert.Contains("No worker has the ID", missing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdjustTimeOffBalance_WritesTheLedgerOnce_WhenTheCallIsRetried()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);
        var manager = await AddManagerAsync(harness, setup.East);
        var arguments = new Dictionary<string, object>
        {
            ["workerId"] = setup.Ana.ItemId,
            ["policy"] = "Vacation",
            ["amount"] = 16,
            ["effectiveDate"] = "2026-11-02",
            ["note"] = "Worked the holiday",
        };

        var first = await InvokeAsync(harness, AdjustTimeOffBalanceTool.ToolName, manager, arguments);
        var retried = await InvokeAsync(harness, AdjustTimeOffBalanceTool.ToolName, manager, arguments);
        var ledger = await TimeOffKit.LedgerAsync(harness, setup.Ana, setup.Policy);

        Assert.Equal(JsonElement.Parse(first).GetProperty("ledgerEntryId").GetString(), JsonElement.Parse(retried).GetProperty("ledgerEntryId").GetString());
        Assert.Equal(2, ledger.Count);
        Assert.Equal(56m, ledger.Sum(entry => entry.Amount));
    }

    [Fact]
    public async Task AdjustTimeOffBalance_RefusesAnUnknownPolicy()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);
        var manager = await AddManagerAsync(harness, setup.East);

        var result = await InvokeAsync(harness, AdjustTimeOffBalanceTool.ToolName, manager, new() { ["workerId"] = setup.Ana.ItemId, ["policy"] = "Sabbatical", ["amount"] = 8, ["note"] = "Gift" });

        Assert.Contains("No time-off policy is called 'Sabbatical'", result, StringComparison.Ordinal);
        Assert.Single(await TimeOffKit.LedgerAsync(harness, setup.Ana, setup.Policy));
    }

    [Fact]
    public async Task AssignTimeOffPolicy_AssignsThePolicyToTheWorkerAlone_AndTakesItAway()
    {
        await using var harness = await CreateAsync();
        var setup = await SetUpAsync(harness);
        var manager = await AddManagerAsync(harness, setup.East);

        var assigned = await InvokeAsync(harness, AssignTimeOffPolicyTool.ToolName, manager, new() { ["workerId"] = setup.Ana.ItemId, ["policy"] = "Vacation", ["action"] = "Assign", ["effectiveFrom"] = "2026-12-01" });
        var afterAssign = await PolicyAsync(harness, setup.Policy.ItemId);
        var unassigned = await InvokeAsync(harness, AssignTimeOffPolicyTool.ToolName, manager, new() { ["workerId"] = setup.Ana.ItemId, ["policy"] = "Vacation", ["action"] = "Unassign" });
        var afterUnassign = await PolicyAsync(harness, setup.Policy.ItemId);
        var invalid = await InvokeAsync(harness, AssignTimeOffPolicyTool.ToolName, manager, new() { ["workerId"] = setup.Ana.ItemId, ["policy"] = "Vacation", ["action"] = "Swap" });

        var assignment = Assert.Single(afterAssign.Assignments, item => item.WorkerId == setup.Ana.ItemId);
        Assert.Equal(new DateOnly(2026, 12, 1), assignment.EffectiveFrom);
        Assert.Equal("2026-12-01", JsonElement.Parse(assigned).GetProperty("effectiveFrom").GetString());
        Assert.Equal(1, JsonElement.Parse(unassigned).GetProperty("removedAssignments").GetInt32());
        Assert.DoesNotContain(afterUnassign.Assignments, item => item.WorkerId == setup.Ana.ItemId);
        Assert.Contains(afterUnassign.Assignments, item => item.TeamId == setup.East.ItemId);
        Assert.Equal("The action must be Assign or Unassign.", invalid);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public void Tool_IsRegisteredWithItsTextsAndApproval(string toolName)
    {
        var tools = GetTools(typeof(TimeOffAdminAIToolsStartup));
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
    public void NewStartup_RegistersFiveToolsWithTwoApprovals_OnTheTimeOffFeature_AndLeavesTheExistingToolsAlone()
    {
        var tools = GetTools(typeof(TimeOffAdminAIToolsStartup));
        var existing = GetTools(typeof(TimeOffAIToolsStartup));
        var required = typeof(TimeOffAdminAIToolsStartup).GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false).Cast<RequireFeaturesAttribute>().Single();
        var feature = typeof(TimeOffAdminAIToolsStartup).GetCustomAttributes(typeof(FeatureAttribute), inherit: false).Cast<FeatureAttribute>().Single();

        Assert.Equal(5, tools.Count);
        Assert.Equal(2, tools.Values.Count(tool => tool.RequiresApproval));
        Assert.Contains(AIFeatureIds.Area, required.RequiredFeatureNames);
        Assert.Equal(WorkforceConstants.Features.TimeOff, feature.FeatureName);
        Assert.Equal([RequestTimeOffTool.ToolName, TimeOffBalanceTool.ToolName], existing.Keys.Order(StringComparer.Ordinal));
        Assert.Empty(tools.Keys.Intersect(existing.Keys));
    }

    internal static AIFunction Create(string toolName)
        => toolName switch
        {
            ListTimeOffRequestsTool.ToolName => new ListTimeOffRequestsTool(),
            DecideTimeOffRequestTool.ToolName => new DecideTimeOffRequestTool(),
            GetWorkerTimeOffTool.ToolName => new GetWorkerTimeOffTool(),
            AdjustTimeOffBalanceTool.ToolName => new AdjustTimeOffBalanceTool(),
            AssignTimeOffPolicyTool.ToolName => new AssignTimeOffPolicyTool(),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, "Unknown tool."),
        };

    internal static IReadOnlyDictionary<string, AIToolDefinitionEntry> GetTools(Type startupType)
    {
        var services = new ServiceCollection();
        var localizer = Activator.CreateInstance(typeof(PassThroughLocalizer<>).MakeGenericType(startupType));
        var startup = (StartupBase)Activator.CreateInstance(startupType, localizer);

        startup.ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value.Tools;
    }

    internal static ILocalClock LocalClock(Func<DateTime> utcNow)
    {
        var localClock = new Mock<ILocalClock>();

        localClock.Setup(clock => clock.GetLocalNowAsync()).ReturnsAsync(() => new DateTimeOffset(utcNow(), TimeSpan.Zero));
        localClock.Setup(clock => clock.ConvertToLocalAsync(It.IsAny<DateTimeOffset>())).ReturnsAsync((DateTimeOffset value) => value);

        return localClock.Object;
    }

    internal static Task<string> InvokeAsync(CalendarTestHarness harness, AIFunction tool, ClaimsPrincipal user, Dictionary<string, object> arguments)
    {
        return harness.RunAsync(async services =>
        {
            services.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { User = user, RequestServices = services };

            var result = await tool.InvokeAsync(new AIFunctionArguments(arguments.ToDictionary(pair => pair.Key, pair => (object)JsonSerializer.SerializeToElement(pair.Value))) { Services = services });

            return result?.ToString();
        });
    }

    private static Task<string> InvokeAsync(CalendarTestHarness harness, string toolName, ClaimsPrincipal user, Dictionary<string, object> arguments)
        => InvokeAsync(harness, Create(toolName), user, arguments);

    private static Task<CalendarTestHarness> CreateAsync()
    {
        return TimeOffKit.CreateAsync(services =>
        {
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton(LocalClock(() => CalendarTestHarness.Now));
        });
    }

    private static async Task<ClaimsPrincipal> AddManagerAsync(CalendarTestHarness harness, Team team)
    {
        await WorkforceTests.AddWorkerAsync(harness, "Maya", worker =>
        {
            worker.UserId = "maya";
            worker.TeamMemberships.Add(new TeamMembership { TeamId = team.ItemId, Role = TeamMemberRole.Manager });
        });

        return CalendarTestHarness.User(
            "maya",
            WorkforcePermissions.ApproveTimeOff.Name,
            WorkforcePermissions.AdjustTimeOffBalances.Name,
            WorkforcePermissions.ManageTimeOffPolicies.Name);
    }

    private static Task<TimeOffPolicy> PolicyAsync(CalendarTestHarness harness, string id)
        => harness.RunAsync(async services => await services.GetRequiredService<ITimeOffPolicyManager>().FindByIdAsync(id), commit: false);

    private static async Task<Setup> SetUpAsync(CalendarTestHarness harness)
    {
        var east = await WorkforceTests.AddTeamAsync(harness, "East", TeamKind.Team);
        var west = await WorkforceTests.AddTeamAsync(harness, "West", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", east);
        var ben = await PlanningKit.AddMemberAsync(harness, "Ben", west);
        var vacation = await TimeOffKit.AddTypeAsync(harness, "Vacation");
        var policy = await TimeOffKit.AddPolicyAsync(harness, vacation, east, policy => policy.Assignments.Add(new TimeOffPolicyAssignment { TeamId = west.ItemId }));

        await TimeOffKit.OpenBalanceAsync(harness, ana, policy, 40);
        await TimeOffKit.OpenBalanceAsync(harness, ben, policy, 40);

        return new Setup(east, west, ana, ben, vacation, policy);
    }

    private sealed record Setup(Team East, Team West, StaffMember Ana, StaffMember Ben, TimeOffType Vacation, TimeOffPolicy Policy);

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
