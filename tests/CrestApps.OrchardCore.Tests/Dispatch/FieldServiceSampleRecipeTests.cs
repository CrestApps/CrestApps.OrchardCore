using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Assets.Core.Services;
using CrestApps.OrchardCore.Assets.Recipes;
using CrestApps.OrchardCore.Geo.Core.Services;
using CrestApps.OrchardCore.Geo.Recipes;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Recipes;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Recipes;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Tests.Dispatch;

/// <summary>
/// The field service sample recipe: every step runs without errors, twice, and sets up the team, technicians, a shift
/// template, territories, assets and work order settings linked to each other, without duplicates.
/// </summary>
public sealed class FieldServiceSampleRecipeTests
{
    [Fact]
    public async Task TheSampleRecipe_RunsCleanlyTwice_WithEverythingLinked()
    {
        var recipe = JsonNode.Parse(File.ReadAllText(RecipePath())).AsObject();

        await using var kit = await DispatchKit.CreateAsync();

        await kit.Harness.RunAsync(services => services.GetRequiredService<ActivityCodeSeeder>().SeedAsync());

        var handlers = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["Workforce"] = typeof(WorkforceRecipeStep),
            [ShiftPlanningRecipeStep.ShiftPlanningStepName] = typeof(ShiftPlanningRecipeStep),
            [GeoRecipeStep.RecipeName] = typeof(GeoRecipeStep),
            [AssetsRecipeStep.RecipeName] = typeof(AssetsRecipeStep),
            [WorkOrdersRecipeStep.RecipeName] = typeof(WorkOrdersRecipeStep),
        };
        var errors = new List<string>();

        for (var run = 0; run < 2; run++)
        {
            foreach (var step in recipe["steps"]!.AsArray().OfType<JsonObject>())
            {
                var name = step["name"]!.GetValue<string>();

                if (name == "feature")
                {
                    continue;
                }

                Assert.True(handlers.ContainsKey(name), $"The sample has a step no test handler runs: {name}.");

                errors.AddRange(await kit.Harness.RunAsync(async services =>
                {
                    var context = new RecipeExecutionContext { Name = name, Step = step.DeepClone().AsObject() };

                    await ((NamedRecipeStepHandler)ActivatorUtilities.CreateInstance(services, handlers[name])).ExecuteAsync(context);

                    return context.Errors;
                }));
            }
        }

        Assert.Empty(errors);

        await kit.Harness.RunAsync(async services =>
        {
            var territories = (await services.GetRequiredService<ServiceTerritoryManager>().GetAllAsync()).ToList();
            var assets = (await services.GetRequiredService<AssetManager>().GetAllAsync()).ToList();
            var types = (await services.GetRequiredService<WorkOrderSettingManager<WorkOrderType>>().GetAllAsync()).ToList();
            var checklists = (await services.GetRequiredService<WorkOrderSettingManager<ChecklistTemplate>>().GetAllAsync()).ToList();
            var policies = (await services.GetRequiredService<WorkOrderSettingManager<ServiceLevelPolicy>>().GetAllAsync()).ToList();
            var (_, workers) = await services.GetRequiredService<IStaffMemberManager>().SearchAsync(new StaffQuery(), 0, 50);
            var teams = (await services.GetRequiredService<ITeamManager>().GetAllAsync()).Where(team => team.Name == "Field Operations").ToList();
            var templates = (await services.GetRequiredService<IShiftTemplateManager>().GetAllAsync()).Where(template => template.Name == "Field day").ToList();

            Assert.Equal(2, territories.Count);
            Assert.Equal("4fs7kjkcahkr0q5vh31ktg943y", territories.Single(territory => territory.Name == "Cambridge").ParentId);
            Assert.Equal(2, assets.Count);
            Assert.True(assets.Single(asset => asset.Name == "Van 1").Fleet.CarriesStock);
            Assert.Equal(["Boiler service", "No heat"], types.Where(type => type.Name != kit.Type.Name).Select(type => type.Name).Order());
            Assert.Equal(["4fexpaa3vgvp6s1h1y9jzak6dt"], types.Single(type => type.Name == "Boiler service").ChecklistTemplateIds);
            Assert.Equal(4, Assert.Single(checklists).Questions.Count);
            Assert.Equal(4, Assert.Single(policies).Targets.Count);
            Assert.Single(teams);
            Assert.Single(templates);
            Assert.Equal(4, workers.Count(worker => worker.EmployeeNumber?.StartsWith("200", StringComparison.Ordinal) == true));
        }, commit: false);
    }

    private static string RecipePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        var relative = Path.Combine("src", "Modules", "CrestApps.OrchardCore.Dispatch", "Recipes", "field-service-sample.recipe.json");

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, relative)))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return Path.Combine(directory.FullName, relative);
    }
}
