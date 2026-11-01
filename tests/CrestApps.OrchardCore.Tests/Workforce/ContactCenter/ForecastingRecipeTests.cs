using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.Indexes;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Recipes;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Tests.Workforce.ContactCenter;

/// <summary>
/// The WorkforceForecasting and WorkforceAdherence recipe steps, and the sample recipe that uses them.
/// </summary>
public sealed class ForecastingRecipeTests
{
    [Fact]
    public async Task TheForecastingStep_LinksQueuesTeamsAndGroupsByName_ThenUpdatesInPlace()
    {
        await using var harness = await CreateAsync();
        var team = await WorkforceTests.AddTeamAsync(harness, "Customer Care", TeamKind.Team);
        var step = JsonNode.Parse("""
            {
              "name": "WorkforceForecasting",
              "PlanningGroups": [ { "name": "Support voice", "queueNames": [ "Support" ], "teamName": "Customer Care", "shrinkagePercent": 30 } ],
              "ForecastEvents": [ { "name": "Promotion", "kind": "Promotion", "startDate": "2027-03-15", "endDate": "2027-03-19", "volumeMultiplier": 1.2, "planningGroupNames": [ "Support voice" ] } ],
              "CapacityPlans": [ { "name": "Next year", "planningGroupName": "Support voice", "startDate": "2027-01-06", "weeks": 52, "currentFte": 20,
                "hiringClasses": [ { "startDate": "2027-02-01", "size": 4 } ] } ]
            }
            """).AsObject();

        Assert.Empty(await RunAsync<ForecastingRecipeStep>(harness, step));

        step["PlanningGroups"][0]["shrinkagePercent"] = 35;

        Assert.Empty(await RunAsync<ForecastingRecipeStep>(harness, step));

        await harness.RunAsync(async services =>
        {
            var group = Assert.Single(await services.GetRequiredService<PlanningGroupManager>().GetAllAsync());
            var forecastEvent = Assert.Single(await services.GetRequiredService<ForecastEventManager>().GetAllAsync());
            var plan = Assert.Single(await services.GetRequiredService<CapacityPlanManager>().GetAllAsync());

            Assert.Equal(["support"], group.QueueIds);
            Assert.Equal(team.ItemId, group.TeamId);
            Assert.Equal(35, group.ShrinkagePercent);
            Assert.Equal([group.ItemId], forecastEvent.PlanningGroupIds);
            Assert.Equal(group.ItemId, plan.PlanningGroupId);
            Assert.Equal(new DateOnly(2027, 1, 4), plan.StartDate);
            Assert.Equal(4, Assert.Single(plan.HiringClasses).Size);
        }, commit: false);
    }

    [Fact]
    public async Task TheForecastingStep_ReportsAQueueThatDoesNotExist()
    {
        await using var harness = await CreateAsync();
        var step = JsonNode.Parse("""
            { "name": "WorkforceForecasting", "PlanningGroups": [ { "name": "Billing", "queueNames": [ "Billing" ] } ] }
            """).AsObject();

        var errors = await RunAsync<ForecastingRecipeStep>(harness, step);

        Assert.Equal(["The planning group 'Billing' in the WorkforceForecasting step names a queue that doesn't exist: 'Billing'."], errors);
    }

    [Fact]
    public async Task TheSampleRecipe_RunsCleanly()
    {
        var recipe = JsonNode.Parse(File.ReadAllText(RecipePath())).AsObject();
        await using var harness = await CreateAsync();
        var handlers = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["Workforce"] = typeof(CrestApps.OrchardCore.Workforce.Recipes.WorkforceRecipeStep),
            [ForecastingRecipeStep.ForecastingStepName] = typeof(ForecastingRecipeStep),
            [AdherenceRecipeStep.AdherenceStepName] = typeof(AdherenceRecipeStep),
        };
        var errors = new List<string>();

        foreach (var step in recipe["steps"]!.AsArray().OfType<JsonObject>())
        {
            var name = step["name"]!.GetValue<string>();

            // The contact center's own step makes the queue; the test's queue manager already has it.
            if (name is "feature" or "ContactCenterQueue")
            {
                continue;
            }

            Assert.True(handlers.ContainsKey(name), $"The sample has a step no test handler runs: {name}.");
            errors.AddRange(await RunAsync(harness, handlers[name], step));
        }

        Assert.Empty(errors);

        await harness.RunAsync(async services =>
        {
            Assert.Single(await services.GetRequiredService<PlanningGroupManager>().GetAllAsync());
            Assert.Equal(2, (await services.GetRequiredService<ForecastEventManager>().GetAllAsync()).Count());
            Assert.Single(await services.GetRequiredService<CapacityPlanManager>().GetAllAsync());
            Assert.Equal(3, Assert.Single(await services.GetRequiredService<AdherenceRuleSetManager>().GetAllAsync()).Rules.Count);
        }, commit: false);
    }

    private static Task<List<string>> RunAsync<TStep>(Calendars.CalendarTestHarness harness, JsonObject step)
        where TStep : NamedRecipeStepHandler
        => RunAsync(harness, typeof(TStep), step);

    private static Task<List<string>> RunAsync(Calendars.CalendarTestHarness harness, Type handler, JsonObject step)
    {
        return harness.RunAsync(async services =>
        {
            var context = new RecipeExecutionContext { Name = step["name"]!.GetValue<string>(), Step = step.DeepClone().AsObject() };

            await ((NamedRecipeStepHandler)ActivatorUtilities.CreateInstance(services, handler)).ExecuteAsync(context);

            return context.Errors.ToList();
        });
    }

    private static Task<Calendars.CalendarTestHarness> CreateAsync()
    {
        var queues = new Mock<IActivityQueueManager>();

        queues.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ActivityQueue { ItemId = "support", Name = "Support" }]);

        return WorkforceTests.CreateAsync(
            services =>
            {
                services.AddSingleton(queues.Object);
                services.AddSingleton(new Mock<IIntervalStatisticsReader>().Object);
                services.AddScoped<PlanningGroupStore>();
                services.AddScoped<PlanningGroupManager>();
                services.AddScoped<ICatalogEntryHandler<PlanningGroup>, PlanningGroupHandler>();
                services.AddScoped<ForecastEventStore>();
                services.AddScoped<ForecastEventManager>();
                services.AddScoped<ICatalogEntryHandler<ForecastEvent>, ForecastEventHandler>();
                services.AddScoped<CapacityPlanStore>();
                services.AddScoped<CapacityPlanManager>();
                services.AddScoped<ICatalogEntryHandler<CapacityPlan>, CapacityPlanHandler>();
                services.AddScoped<AdherenceRuleSetStore>();
                services.AddScoped<AdherenceRuleSetManager>();
                services.AddScoped<ICatalogEntryHandler<AdherenceRuleSet>, AdherenceRuleSetHandler>();
            },
            async schemaBuilder =>
            {
                await ForecastingIndexSchema.CreateAsync(schemaBuilder);
                await CapacityPlanIndexSchema.CreateAsync(schemaBuilder);
                await AdherenceIndexSchema.CreateAsync(schemaBuilder);
            },
            [new PlanningGroupIndexProvider(), new ForecastEventIndexProvider(), new ForecastIndexProvider(), new CapacityPlanIndexProvider(),
             new AdherenceRuleSetIndexProvider(), new AdherenceExceptionIndexProvider(), new AdherenceDayIndexProvider(), new AdherenceAlertIndexProvider()]);
    }

    private static string RecipePath([System.Runtime.CompilerServices.CallerFilePath] string path = null)
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", "..", "..", ".."));

        return Path.Combine(root, "src", "Modules", "CrestApps.OrchardCore.Workforce.ContactCenter", "Recipes", "workforce-contact-center-sample.recipe.json");
    }
}
