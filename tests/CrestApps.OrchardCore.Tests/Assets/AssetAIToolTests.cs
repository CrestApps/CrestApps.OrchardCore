using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Assets;
using CrestApps.OrchardCore.Assets.AI;
using CrestApps.OrchardCore.Assets.Core.Services;
using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Workforce;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using Asset = CrestApps.OrchardCore.Assets.Models.Asset;

namespace CrestApps.OrchardCore.Tests.Assets;

/// <summary>
/// The asset AI tools for field service: they refuse a caller who is not signed in or lacks the Assets permission, list
/// and read assets as the Assets pages do, and record meter readings through the asset service. Asset types are a
/// read-only record kind.
/// </summary>
public sealed class AssetAIToolTests
{
    internal static readonly string[] NewTools = [ListAssetsTool.ToolName, GetAssetTool.ToolName, RecordAssetMeterReadingTool.ToolName];

    public static TheoryData<string> ToolNames
    {
        get
        {
            var data = new TheoryData<string>();

            foreach (var name in NewTools)
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

        var result = await InvokeAsync(harness, toolName, new ClaimsPrincipal(new ClaimsIdentity()), new() { ["asset"] = "x" });

        Assert.Equal(AssetToolBase.SignInRequired, result);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutThePermission(string toolName)
    {
        await using var harness = await CreateAsync();

        var result = await InvokeAsync(harness, toolName, CalendarTestHarness.User("ana", "SomethingElse"), new() { ["asset"] = "x" });

        Assert.StartsWith("The user isn't allowed to do this", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecordAssetMeterReading_NeedsManageAssets()
    {
        await using var harness = await CreateAsync();

        var result = await InvokeAsync(harness, RecordAssetMeterReadingTool.ToolName, Viewer(), new() { ["asset"] = "x", ["meter"] = "Hours run", ["value"] = 1 });

        Assert.Equal(AssetToolBase.NotAllowed(AssetsPermissions.ManageAssets), result);
    }

    [Fact]
    public async Task ListAssets_FiltersByStatusAndSearch_AndRefusesAnUnknownType()
    {
        await using var harness = await CreateAsync();
        var type = await AssetsTests.AddTypeAsync(harness);

        await AddAssetAsync(harness, type, "Boiler", asset => asset.SerialNumber = "SN-1");
        await AddAssetAsync(harness, type, "Old pump", asset => asset.Status = AssetStatus.Retired);

        var retired = JsonElement.Parse(await InvokeAsync(harness, ListAssetsTool.ToolName, Viewer(), new() { ["status"] = "Retired" }));
        var bySerial = JsonElement.Parse(await InvokeAsync(harness, ListAssetsTool.ToolName, Viewer(), new() { ["search"] = "SN-1" }));
        var unknown = await InvokeAsync(harness, ListAssetsTool.ToolName, Viewer(), new() { ["type"] = "Rocket" });

        Assert.Equal("Old pump", Assert.Single(retired.GetProperty("items").EnumerateArray()).GetProperty("name").GetString());
        Assert.Equal("Boiler", Assert.Single(bySerial.GetProperty("items").EnumerateArray()).GetProperty("name").GetString());
        Assert.Equal("Equipment", bySerial.GetProperty("items")[0].GetProperty("type").GetString());
        Assert.StartsWith("No asset type is called 'Rocket'.", unknown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsset_GivesTheWarrantyHierarchyAndReadings_AndReportsAnUnknownAsset()
    {
        await using var harness = await CreateAsync();
        var type = await AssetsTests.AddTypeAsync(harness);
        var boiler = await AddAssetAsync(harness, type, "Boiler", asset => asset.Warranty = new AssetWarranty { StartDate = new DateOnly(2025, 6, 1), EndDate = new DateOnly(2030, 5, 31), Provider = "Carrier" });

        await AddAssetAsync(harness, type, "Pump", asset => asset.ParentAssetId = boiler.ItemId);
        await harness.RunAsync(services => services.GetRequiredService<AssetService>().RecordReadingAsync(new AssetMeterReading { AssetId = boiler.ItemId, Meter = "Hours run", Value = 42, Utc = CalendarTestHarness.Now.AddHours(-1) }));

        var result = JsonElement.Parse(await InvokeAsync(harness, GetAssetTool.ToolName, Viewer(), new() { ["asset"] = boiler.Number }));
        var unknown = await InvokeAsync(harness, GetAssetTool.ToolName, Viewer(), new() { ["asset"] = "A-99999" });

        Assert.Equal(boiler.ItemId, result.GetProperty("id").GetString());
        Assert.True(result.GetProperty("warranty").GetProperty("coversToday").GetBoolean());
        Assert.Equal("2030-05-31", result.GetProperty("warranty").GetProperty("end").GetString());
        Assert.Equal("Pump", Assert.Single(result.GetProperty("contains").EnumerateArray()).GetProperty("name").GetString());
        Assert.Equal(42m, Assert.Single(result.GetProperty("latestReadings").EnumerateArray()).GetProperty("value").GetDecimal());
        Assert.Equal("No asset has the ID or number 'A-99999'. Use listAssets to find it.", unknown);
    }

    [Fact]
    public async Task RecordAssetMeterReading_RecordsAReading_AndACumulativeMeterCannotGoDown()
    {
        await using var harness = await CreateAsync();
        var type = await AssetsTests.AddTypeAsync(harness);
        var forklift = await AddAssetAsync(harness, type, "Forklift");

        var recorded = JsonElement.Parse(await InvokeAsync(harness, RecordAssetMeterReadingTool.ToolName, Manager(), new() { ["asset"] = forklift.ItemId, ["meter"] = "hours run", ["value"] = 120, ["at"] = "2026-11-02T08:00" }));
        var lower = await InvokeAsync(harness, RecordAssetMeterReadingTool.ToolName, Manager(), new() { ["asset"] = forklift.ItemId, ["meter"] = "Hours run", ["value"] = 110 });
        var readings = await harness.RunAsync(services => services.GetRequiredService<AssetService>().ReadingsAsync(forklift.ItemId), commit: false);

        Assert.True(recorded.GetProperty("recorded").GetBoolean());
        Assert.Equal("Hours run", recorded.GetProperty("meter").GetString());
        Assert.Equal("Nothing was recorded: Hours run only goes up: the last reading was 120.", lower);
        Assert.Equal(120m, Assert.Single(readings).Value);
        Assert.Equal("admin", Assert.Single(readings).RecordedBy);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public void Tool_IsRegisteredWithItsTextsAndNoApproval(string toolName)
    {
        var tools = WorkforceTimeOffAdminAIToolTests.GetTools(typeof(AIToolsStartup));

        Assert.True(tools.TryGetValue(toolName, out var tool), $"The tool '{toolName}' is not registered.");
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Equal("Field Service", tool.Category);
        Assert.True(tool.IsSelectable());
        Assert.False(tool.RequiresApproval);
        Assert.Equal(toolName, Create(toolName).Name);
    }

    [Fact]
    public void Startup_RegistersThreeTools_AndAReadOnlyAssetTypeKind_OnTheAssetsFeature()
    {
        var tools = WorkforceTimeOffAdminAIToolTests.GetTools(typeof(AIToolsStartup));
        var services = new ServiceCollection();

        new AIToolsStartup(new PassThroughStringLocalizer<AIToolsStartup>()).ConfigureServices(services);

        using var provider = services.BuildServiceProvider();
        var kinds = provider.GetRequiredService<IOptions<AIRecordKindOptions>>().Value.Kinds;
        var kind = Assert.Single(kinds.Values);
        var required = typeof(AIToolsStartup).GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false).Cast<RequireFeaturesAttribute>().Single();
        var feature = typeof(AIToolsStartup).GetCustomAttributes(typeof(FeatureAttribute), inherit: false).Cast<FeatureAttribute>().Single();

        Assert.Equal(3, tools.Count);
        Assert.Equal("assetType", kind.Name);
        Assert.False(kind.CanWrite);
        Assert.Equal(AssetsPermissions.ManageAssets, kind.Permission);
        Assert.Contains(AIFeatureIds.Area, required.RequiredFeatureNames);
        Assert.Equal(AssetsConstants.Features.Assets, feature.FeatureName);
    }

    private static AIFunction Create(string toolName)
        => toolName switch
        {
            ListAssetsTool.ToolName => new ListAssetsTool(),
            GetAssetTool.ToolName => new GetAssetTool(),
            RecordAssetMeterReadingTool.ToolName => new RecordAssetMeterReadingTool(),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, "Unknown tool."),
        };

    private static Task<string> InvokeAsync(CalendarTestHarness harness, string toolName, ClaimsPrincipal user, Dictionary<string, object> arguments)
        => WorkforceTimeOffAdminAIToolTests.InvokeAsync(harness, Create(toolName), user, arguments);

    private static ClaimsPrincipal Viewer()
        => CalendarTestHarness.User("viewer", AssetsPermissions.ViewAssets.Name);

    private static ClaimsPrincipal Manager()
        => CalendarTestHarness.User("admin", AssetsPermissions.ManageAssets.Name);

    private static Task<CalendarTestHarness> CreateAsync()
        => AssetsTests.CreateAsync(services =>
        {
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton(WorkforceTimeOffAdminAIToolTests.LocalClock(() => CalendarTestHarness.Now));
        });

    private static Task<Asset> AddAssetAsync(CalendarTestHarness harness, AssetType type, string name, Action<Asset> configure = null)
        => harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<AssetManager>();
            var asset = await manager.NewAsync();

            asset.Name = name;
            asset.AssetTypeId = type.ItemId;
            configure?.Invoke(asset);
            await manager.CreateAsync(asset);

            return asset;
        });
}
