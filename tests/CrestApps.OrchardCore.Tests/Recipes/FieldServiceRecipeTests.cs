using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Assets;
using CrestApps.OrchardCore.Assets.Core.Indexes;
using CrestApps.OrchardCore.Assets.Core.Services;
using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Assets.Recipes;
using CrestApps.OrchardCore.Geo;
using CrestApps.OrchardCore.Geo.Core.Indexes;
using CrestApps.OrchardCore.Geo.Core.Services;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Recipes;
using CrestApps.OrchardCore.Geo.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Recipes;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Deployment;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Recipes;

/// <summary>
/// The field service modules' deployment steps export what their recipe steps import: run on another site, the
/// records come back with the same ids, children after their parents, and running the step again changes nothing.
/// </summary>
public sealed class FieldServiceRecipeTests
{
    [Fact]
    public async Task Territories_TravelToAnotherSite_WithTheirParent_Once()
    {
        await using var source = await GeoAsync();
        await using var target = await GeoAsync();

        var parent = await source.RunAsync(services => AddTerritoryAsync(services, "Boston area", null, "021*"));

        await source.RunAsync(services => AddTerritoryAsync(services, "Cambridge", parent.ItemId, "02139"));

        var step = await ExportAsync(source, services => ActivatorUtilities.CreateInstance<GeoDeploymentSource>(services), new GeoDeploymentStep());
        var errors = await ImportTwiceAsync(target, step, services => ActivatorUtilities.CreateInstance<GeoRecipeStep>(services));
        var imported = await target.RunAsync(async services => (await services.GetRequiredService<ServiceTerritoryManager>().GetAllAsync()).ToList(), commit: false);

        Assert.Empty(errors);
        Assert.Equal(2, imported.Count);
        Assert.Equal(parent.ItemId, imported.Single(territory => territory.Name == "Cambridge").ParentId);
        Assert.Equal(["021*"], imported.Single(territory => territory.ItemId == parent.ItemId).PostalCodes);
    }

    [Fact]
    public async Task WorkOrderSettings_TravelToAnotherSite_StillLinked()
    {
        await using var source = await WorkOrdersAsync();
        await using var target = await WorkOrdersAsync();

        var type = await source.RunAsync(async services =>
        {
            var checklists = services.GetRequiredService<WorkOrderSettingManager<ChecklistTemplate>>();
            var checklist = await checklists.NewAsync();

            checklist.Name = "Boiler safety";
            await checklists.CreateAsync(checklist);

            var levels = services.GetRequiredService<WorkOrderSettingManager<ServiceLevelPolicy>>();
            var level = await levels.NewAsync();

            level.Name = "Standard";
            await levels.CreateAsync(level);

            var types = services.GetRequiredService<WorkOrderSettingManager<WorkOrderType>>();
            var workOrderType = await types.NewAsync();

            workOrderType.Name = "Boiler service";
            workOrderType.ServiceLevelPolicyId = level.ItemId;
            await types.CreateAsync(workOrderType);

            return workOrderType;
        });

        var step = await ExportAsync(source, services => ActivatorUtilities.CreateInstance<WorkOrdersDeploymentSource>(services), new WorkOrdersDeploymentStep());
        var errors = await ImportTwiceAsync(target, step, services => ActivatorUtilities.CreateInstance<WorkOrdersRecipeStep>(services));
        var (types, levels, checklists) = await target.RunAsync(async services => (
            (await services.GetRequiredService<WorkOrderSettingManager<WorkOrderType>>().GetAllAsync()).ToList(),
            (await services.GetRequiredService<WorkOrderSettingManager<ServiceLevelPolicy>>().GetAllAsync()).ToList(),
            (await services.GetRequiredService<WorkOrderSettingManager<ChecklistTemplate>>().GetAllAsync()).ToList()), commit: false);

        Assert.Empty(errors);
        Assert.False(step.ContainsKey("Agreements"));
        var imported = Assert.Single(types);
        Assert.Equal(type.ItemId, imported.ItemId);
        Assert.Equal(Assert.Single(levels).ItemId, imported.ServiceLevelPolicyId);
        Assert.Equal("Boiler safety", Assert.Single(checklists).Name);
    }

    [Fact]
    public async Task Assets_TravelToAnotherSite_WithTheirTypeAndParent()
    {
        await using var source = await AssetsAsync();
        await using var target = await AssetsAsync();

        var boiler = await source.RunAsync(async services =>
        {
            var types = services.GetRequiredService<AssetTypeManager>();
            var type = await types.NewAsync();

            type.Name = "Boiler";
            await types.CreateAsync(type);

            var assets = services.GetRequiredService<AssetManager>();
            var plant = await assets.NewAsync();

            plant.Name = "Plant room";
            plant.AssetTypeId = type.ItemId;
            await assets.CreateAsync(plant);

            var asset = await assets.NewAsync();

            asset.Name = "Boiler 1";
            asset.AssetTypeId = type.ItemId;
            asset.ParentAssetId = plant.ItemId;
            await assets.CreateAsync(asset);

            return asset;
        });

        var step = await ExportAsync(source, services => ActivatorUtilities.CreateInstance<AssetsDeploymentSource>(services), new AssetsDeploymentStep { IncludeAssets = true });
        var errors = await ImportTwiceAsync(target, step, services => ActivatorUtilities.CreateInstance<AssetsRecipeStep>(services));
        var imported = await target.RunAsync(async services => (await services.GetRequiredService<AssetManager>().GetAllAsync()).ToList(), commit: false);

        Assert.Empty(errors);
        Assert.Equal(2, imported.Count);
        Assert.Equal(boiler.ParentAssetId, imported.Single(asset => asset.ItemId == boiler.ItemId).ParentAssetId);
    }

    private static async Task<JsonObject> ExportAsync<TStep>(CalendarTestHarness harness, Func<IServiceProvider, IDeploymentSource> source, TStep step)
        where TStep : DeploymentStep
    {
        var result = new DeploymentPlanResult(Mock.Of<global::OrchardCore.Deployment.IFileBuilder>(), new RecipeDescriptor());

        await harness.RunAsync(services => source(services).ProcessDeploymentStepAsync(step, result), commit: false);

        return Assert.Single(result.Steps);
    }

    private static async Task<List<string>> ImportTwiceAsync(CalendarTestHarness harness, JsonObject step, Func<IServiceProvider, global::OrchardCore.Recipes.Services.IRecipeStepHandler> handler)
    {
        var errors = new List<string>();

        for (var run = 0; run < 2; run++)
        {
            await harness.RunAsync(async services =>
            {
                var context = new RecipeExecutionContext { Name = step["name"].GetValue<string>(), Step = step.DeepClone().AsObject() };

                await handler(services).ExecuteAsync(context);
                errors.AddRange(context.Errors);
            });
        }

        return errors;
    }

    private static async Task<ServiceTerritory> AddTerritoryAsync(IServiceProvider services, string name, string parentId, string postalCode)
    {
        var manager = services.GetRequiredService<ServiceTerritoryManager>();
        var territory = await manager.NewAsync();

        territory.Name = name;
        territory.ParentId = parentId;
        territory.PostalCodes = [postalCode];
        await manager.CreateAsync(territory);

        return territory;
    }

    private static Task<CalendarTestHarness> GeoAsync()
        => CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddSingleton(new Mock<IGeoSettingsService>().Object);
                services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())));
                services.AddGeoCore();
            },
            GeoIndexSchema.CreateAsync,
            [new ServiceTerritoryIndexProvider(), new GeocodeRequestIndexProvider()],
            [GeoConstants.CollectionName]);

    private static Task<CalendarTestHarness> WorkOrdersAsync()
        => CalendarTestHarness.CreateAsync(
            services => services.AddWorkOrdersCore(),
            WorkOrdersIndexSchema.CreateAsync,
            [new WorkOrderIndexProvider(), new WorkOrderTypeIndexProvider(), new ChecklistTemplateIndexProvider(), new ServiceLevelPolicyIndexProvider(), new WorkOrderSubStatusIndexProvider()],
            [WorkOrdersConstants.CollectionName]);

    private static Task<CalendarTestHarness> AssetsAsync()
        => CalendarTestHarness.CreateAsync(
            services => services.AddAssetsCore(),
            AssetsIndexSchema.CreateAsync,
            [new AssetTypeIndexProvider(), new AssetIndexProvider(), new AssetMeterReadingIndexProvider()],
            [AssetsConstants.CollectionName]);
}
