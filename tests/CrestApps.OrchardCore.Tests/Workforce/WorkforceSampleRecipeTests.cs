using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Recipes;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// The Workforce sample recipe: every step runs without errors and sets up teams, workers, templates, time off, labor
/// rules and the time clock, with the teams given their time clock policy.
/// </summary>
public sealed class WorkforceSampleRecipeTests
{
    [Fact]
    public async Task TheSampleRecipe_RunsCleanly()
    {
        var recipe = JsonNode.Parse(File.ReadAllText(RecipePath())).AsObject();
        await using var harness = await WorkforceTests.CreateAsync(services =>
        {
            services.AddWorkforceTimeOffCore();
            services.AddWorkforceLaborRulesCore();
            services.AddWorkforceTimeClockCore();
        });

        await harness.RunAsync(services => services.GetRequiredService<ActivityCodeSeeder>().SeedAsync());

        var errors = new List<string>();
        var handlers = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["Workforce"] = typeof(WorkforceRecipeStep),
            [ShiftPlanningRecipeStep.ShiftPlanningStepName] = typeof(ShiftPlanningRecipeStep),
            [TimeOffRecipeStep.TimeOffStepName] = typeof(TimeOffRecipeStep),
            [LaborRulesRecipeStep.LaborRulesStepName] = typeof(LaborRulesRecipeStep),
            [TimeClockRecipeStep.TimeClockStepName] = typeof(TimeClockRecipeStep),
        };

        foreach (var step in recipe["steps"]!.AsArray().OfType<JsonObject>())
        {
            var name = step["name"]!.GetValue<string>();

            if (name == "feature")
            {
                continue;
            }

            Assert.True(handlers.ContainsKey(name), $"The sample has a step no test handler runs: {name}.");

            errors.AddRange(await harness.RunAsync(async services =>
            {
                var context = new RecipeExecutionContext { Name = name, Step = step.DeepClone().AsObject() };

                await ((NamedRecipeStepHandler)ActivatorUtilities.CreateInstance(services, handlers[name])).ExecuteAsync(context);

                return context.Errors;
            }));
        }

        Assert.Empty(errors);

        await harness.RunAsync(async services =>
        {
            var tree = await services.GetRequiredService<ITeamManager>().ListTreeAsync();
            var (workers, _) = await services.GetRequiredService<IWorkerManager>().SearchAsync(new WorkerQuery(), 0, 50);
            var templates = await services.GetRequiredService<IShiftTemplateManager>().GetAllAsync();
            var policy = Assert.Single(await services.GetRequiredService<ITimeClockPolicyManager>().GetAllAsync());

            Assert.True(tree.Count >= 3);
            Assert.True(workers >= 5);
            Assert.NotEmpty(templates);
            Assert.Contains(tree, entry => entry.Team.TimeClockPolicyId == policy.ItemId);
        }, commit: false);
    }

    private static string RecipePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "src", "Modules", "CrestApps.OrchardCore.Workforce", "Recipes", "workforce-sample.recipe.json")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return Path.Combine(directory.FullName, "src", "Modules", "CrestApps.OrchardCore.Workforce", "Recipes", "workforce-sample.recipe.json");
    }
}
