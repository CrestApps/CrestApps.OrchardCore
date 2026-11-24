using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.Dispatch;
using CrestApps.OrchardCore.Dispatch.AI;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Workforce;
using CrestApps.OrchardCore.WorkOrders;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Dispatch;

/// <summary>
/// The dispatch administration AI tools: they refuse a caller who is not signed in or lacks the admin page's permission,
/// and act through the same dispatch services as the Visits page, the board and the KPIs page.
/// </summary>
public sealed class DispatchAdminAIToolTests
{
    internal static readonly (string Name, bool RequiresApproval)[] NewTools =
    [
        (ListVisitsTool.ToolName, false),
        (GetVisitTool.ToolName, false),
        (CreateVisitsTool.ToolName, false),
        (UpdateVisitTool.ToolName, false),
        (SetVisitStatusTool.ToolName, true),
        (CloseVisitTool.ToolName, true),
        (GetDispatchBoardTool.ToolName, false),
        (GetFieldServiceKpisTool.ToolName, false),
    ];

    private static readonly ClaimsPrincipal _dispatcher = CalendarTestHarness.User("dispatcher", DispatchPermissions.ManageDispatch.Name);

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
        await using var kit = await CreateKitAsync();

        var result = await InvokeAsync(kit, toolName, new ClaimsPrincipal(new ClaimsIdentity()), new() { ["visit"] = "V-000001" });

        Assert.Equal(DispatchAdminToolBase.SignInRequired, result);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutThePermission(string toolName)
    {
        await using var kit = await CreateKitAsync();

        var result = await InvokeAsync(kit, toolName, CalendarTestHarness.User("viewer", WorkOrdersPermissions.ViewWorkOrders.Name), new() { ["visit"] = "V-000001" });

        Assert.StartsWith("The user isn't allowed to do this", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListVisits_ListsTheOpenVisits_AndRefusesAnUnknownStatus()
    {
        await using var kit = await CreateKitAsync();
        var (workOrder, visit) = await kit.AddJobAsync("No heat", DispatchKit.Depot);

        var result = await InvokeAsync(kit, ListVisitsTool.ToolName, _dispatcher, new());
        var invalid = await InvokeAsync(kit, ListVisitsTool.ToolName, _dispatcher, new() { ["status"] = "Lost" });
        var root = JsonElement.Parse(result);
        var item = Assert.Single(root.GetProperty("items").EnumerateArray());

        Assert.Equal(visit.Number, item.GetProperty("number").GetString());
        Assert.Equal(workOrder.Number, item.GetProperty("workOrderNumber").GetString());
        Assert.Equal("Unscheduled", item.GetProperty("status").GetString());
        Assert.Equal(1, root.GetProperty("total").GetInt32());
        Assert.Equal(0, root.GetProperty("workOrdersWithoutVisits").GetInt32());
        Assert.StartsWith("'Lost' is not a visit status", invalid, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetVisit_GivesTheVisit_AndSaysWhenThereIsNone()
    {
        await using var kit = await CreateKitAsync();
        var (workOrder, visit) = await kit.AddJobAsync("No heat", DispatchKit.Depot);

        var result = await InvokeAsync(kit, GetVisitTool.ToolName, _dispatcher, new() { ["visit"] = workOrder.Number });
        var missing = await InvokeAsync(kit, GetVisitTool.ToolName, _dispatcher, new() { ["visit"] = "V-999999" });
        var root = JsonElement.Parse(result);

        Assert.Equal(visit.Number, root.GetProperty("number").GetString());
        Assert.Contains("Canceled", root.GetProperty("nextStatuses").EnumerateArray().Select(status => status.GetString()));
        Assert.False(root.TryGetProperty("trackingUrl", out _));
        Assert.StartsWith("No visit or work order is numbered", missing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateVisits_AddsAVisitToAWorkOrder_AndRefusesBothChoices()
    {
        await using var kit = await CreateKitAsync();
        var (workOrder, _) = await kit.AddJobAsync("No heat", DispatchKit.Depot);

        var result = await InvokeAsync(kit, CreateVisitsTool.ToolName, _dispatcher, new() { ["workOrderNumber"] = workOrder.Number });
        var both = await InvokeAsync(kit, CreateVisitsTool.ToolName, _dispatcher, new() { ["workOrderNumber"] = workOrder.Number, ["allMissing"] = true });

        Assert.Equal(1, JsonElement.Parse(result).GetProperty("created").GetInt32());
        Assert.Equal(2, (await kit.VisitsOfAsync(workOrder.ItemId)).Count);
        Assert.StartsWith("Give either", both, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateVisit_Pins_RefusesToUnassignAPinnedVisit_ThenUnpinsAndUnassigns()
    {
        await using var kit = await CreateKitAsync();
        var ana = await kit.AddTechnicianAsync("Ana Field");

        await kit.PublishShiftsAsync(ana);

        var (workOrder, visit) = await kit.AddJobAsync("No heat", DispatchKit.Depot);

        Assert.True((await kit.AssignAsync(visit, DispatchKit.Monday(10), ana)).Succeeded);

        var pinned = await InvokeAsync(kit, UpdateVisitTool.ToolName, _dispatcher, new() { ["visit"] = visit.Number, ["pinned"] = true });
        var refused = await InvokeAsync(kit, UpdateVisitTool.ToolName, _dispatcher, new() { ["visit"] = visit.Number, ["unassign"] = true });
        var unassigned = await InvokeAsync(kit, UpdateVisitTool.ToolName, _dispatcher, new() { ["visit"] = visit.Number, ["pinned"] = false, ["unassign"] = true });
        var after = Assert.Single(await kit.VisitsOfAsync(workOrder.ItemId));

        Assert.True(JsonElement.Parse(pinned).GetProperty("visit").GetProperty("pinned").GetBoolean());
        Assert.True(JsonElement.Parse(refused).TryGetProperty("refused", out _));
        Assert.Equal(["unpinned", "unassigned"], JsonElement.Parse(unassigned).GetProperty("changed").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(VisitStatus.Unscheduled, after.Status);
        Assert.False(after.Pinned);
    }

    [Fact]
    public async Task SetVisitStatus_DispatchesAScheduledVisit_AndRefusesAClosingStatus()
    {
        await using var kit = await CreateKitAsync();
        var ana = await kit.AddTechnicianAsync("Ana Field");

        await kit.PublishShiftsAsync(ana);

        var (workOrder, visit) = await kit.AddJobAsync("No heat", DispatchKit.Depot);

        Assert.True((await kit.AssignAsync(visit, DispatchKit.Monday(10), ana)).Succeeded);

        var result = await InvokeAsync(kit, SetVisitStatusTool.ToolName, _dispatcher, new() { ["visit"] = visit.Number, ["status"] = "Dispatched", ["note"] = "Parts loaded" });
        var closing = await InvokeAsync(kit, SetVisitStatusTool.ToolName, _dispatcher, new() { ["visit"] = visit.Number, ["status"] = "Completed" });

        Assert.Equal("Scheduled", JsonElement.Parse(result).GetProperty("previousStatus").GetString());
        Assert.Equal(VisitStatus.Dispatched, Assert.Single(await kit.VisitsOfAsync(workOrder.ItemId)).Status);
        Assert.StartsWith("The status must be one of", closing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CloseVisit_NeedsAReasonToCancel_ThenCancels()
    {
        await using var kit = await CreateKitAsync();
        var (workOrder, visit) = await kit.AddJobAsync("No heat", DispatchKit.Depot);

        var refused = await InvokeAsync(kit, CloseVisitTool.ToolName, _dispatcher, new() { ["visit"] = visit.Number, ["status"] = "Canceled" });
        var canceled = await InvokeAsync(kit, CloseVisitTool.ToolName, _dispatcher, new() { ["visit"] = visit.Number, ["status"] = "Canceled", ["note"] = "Customer fixed it" });

        Assert.StartsWith("Nothing was changed: say why", refused, StringComparison.Ordinal);
        Assert.Equal("Canceled", JsonElement.Parse(canceled).GetProperty("visit").GetProperty("status").GetString());
        Assert.Equal(VisitStatus.Canceled, Assert.Single(await kit.VisitsOfAsync(workOrder.ItemId)).Status);
    }

    [Fact]
    public async Task GetDispatchBoard_GivesTheTechniciansDay_WithoutPositions_AndRefusesABadDay()
    {
        await using var kit = await CreateKitAsync();
        var ana = await kit.AddTechnicianAsync("Ana Field");

        await kit.PublishShiftsAsync(ana);

        var (_, visit) = await kit.AddJobAsync("No heat", DispatchKit.Depot);
        var (_, waiting) = await kit.AddJobAsync("Leak", DispatchKit.South);

        Assert.True((await kit.AssignAsync(visit, DispatchKit.Monday(10), ana)).Succeeded);

        var result = await InvokeAsync(kit, GetDispatchBoardTool.ToolName, _dispatcher, new() { ["day"] = PlanningKit.Monday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
        var invalid = await InvokeAsync(kit, GetDispatchBoardTool.ToolName, _dispatcher, new() { ["day"] = "next week" });
        var root = JsonElement.Parse(result);
        var technician = Assert.Single(root.GetProperty("technicians").EnumerateArray());

        Assert.Equal("Ana Field", technician.GetProperty("name").GetString());
        Assert.Equal(visit.Number, Assert.Single(technician.GetProperty("visits").EnumerateArray()).GetProperty("number").GetString());
        Assert.Contains(waiting.Number, root.GetProperty("unscheduled").EnumerateArray().Select(item => item.GetProperty("number").GetString()));
        Assert.False(root.TryGetProperty("positions", out _));
        Assert.Equal("Give the day as yyyy-MM-dd.", invalid);
    }

    [Fact]
    public async Task GetFieldServiceKpis_GivesTheRange_AndRefusesABadDate()
    {
        await using var kit = await CreateKitAsync();

        var result = await InvokeAsync(kit, GetFieldServiceKpisTool.ToolName, _dispatcher, new() { ["from"] = "2026-11-01", ["to"] = "2026-11-09" });
        var invalid = await InvokeAsync(kit, GetFieldServiceKpisTool.ToolName, _dispatcher, new() { ["from"] = "2026-11-10", ["to"] = "2026-11-09" });
        var root = JsonElement.Parse(result);

        Assert.Equal("2026-11-01", root.GetProperty("from").GetString());
        Assert.Equal(0, root.GetProperty("finishedVisits").GetInt32());
        Assert.Equal("'from' must not be after 'to'.", invalid);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public void Tool_IsRegisteredWithItsTextsAndApproval(string toolName)
    {
        var tools = GetTools();
        var requiresApproval = NewTools.Single(tool => tool.Name == toolName).RequiresApproval;

        Assert.True(tools.TryGetValue(toolName, out var tool), $"The tool '{toolName}' is not registered.");
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Equal("Field Service", tool.Category);
        Assert.True(tool.IsSelectable());
        Assert.Equal(requiresApproval, tool.RequiresApproval);
        Assert.Equal(toolName, Create(toolName).Name);
        Assert.False(string.IsNullOrWhiteSpace(Create(toolName).Description));
    }

    [Fact]
    public void NewStartup_RegistersEightToolsWithTwoApprovals_OnTheDispatchFeature()
    {
        var tools = GetTools();
        var required = typeof(DispatchAdminAIToolsStartup).GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false).Cast<RequireFeaturesAttribute>().Single();
        var feature = typeof(DispatchAdminAIToolsStartup).GetCustomAttributes(typeof(FeatureAttribute), inherit: false).Cast<FeatureAttribute>().Single();

        Assert.Equal(NewTools.Length, tools.Count);
        Assert.Equal(2, tools.Values.Count(tool => tool.RequiresApproval));
        Assert.Contains(AIFeatureIds.Area, required.RequiredFeatureNames);
        Assert.Equal(DispatchConstants.Features.Dispatch, feature.FeatureName);
        Assert.DoesNotContain("pinVisit", tools.Keys);
        Assert.DoesNotContain("unassignVisit", tools.Keys);
    }

    private static AIFunction Create(string toolName)
        => toolName switch
        {
            ListVisitsTool.ToolName => new ListVisitsTool(),
            GetVisitTool.ToolName => new GetVisitTool(),
            CreateVisitsTool.ToolName => new CreateVisitsTool(),
            UpdateVisitTool.ToolName => new UpdateVisitTool(),
            SetVisitStatusTool.ToolName => new SetVisitStatusTool(),
            CloseVisitTool.ToolName => new CloseVisitTool(),
            GetDispatchBoardTool.ToolName => new GetDispatchBoardTool(),
            GetFieldServiceKpisTool.ToolName => new GetFieldServiceKpisTool(),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, "Unknown tool."),
        };

    private static IReadOnlyDictionary<string, AIToolDefinitionEntry> GetTools()
    {
        var services = new ServiceCollection();

        new DispatchAdminAIToolsStartup(new PassThroughLocalizer<DispatchAdminAIToolsStartup>()).ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value.Tools;
    }

    private static async Task<DispatchKit> CreateKitAsync()
    {
        var kit = await DispatchKit.CreateAsync();

        // The Visits page, the board and the KPIs show times in the dispatch settings' zone.
        kit.Settings.TimeZoneId = PlanningKit.Zone;

        return kit;
    }

    private static Task<string> InvokeAsync(DispatchKit kit, string toolName, ClaimsPrincipal user, Dictionary<string, object> arguments)
        => kit.Harness.RunAsync(async services =>
        {
            services.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { User = user, RequestServices = services };

            var result = await Create(toolName).InvokeAsync(new AIFunctionArguments(arguments.ToDictionary(pair => pair.Key, pair => (object)JsonSerializer.SerializeToElement(pair.Value))) { Services = services });

            return result?.ToString();
        });

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
