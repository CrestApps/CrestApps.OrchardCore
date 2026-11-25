using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Deployment;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Deployment;
using OrchardCore.Deployment.Services;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Tests.Agent.SiteAdministration;

/// <summary>
/// The deployment plan tools list the plans with their steps, save a plan through the <c>deployment</c> recipe step,
/// and delete a plan from the session.
/// </summary>
public sealed class DeploymentPlanToolsTests
{
    [Fact]
    public async Task ListDeploymentPlans_WithSteps_ReturnsEachStepTypeAndFields()
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListDeploymentPlansTool(), services, new() { ["includeSteps"] = true });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var plan = Assert.Single(json.GetProperty("items").EnumerateArray());
        var step = Assert.Single(plan.GetProperty("steps").EnumerateArray());

        Assert.Equal(7, plan.GetProperty("id").GetInt64());
        Assert.Equal(nameof(SiteAdministrationToolHarness.TestExportStep), step.GetProperty("type").GetString());
        Assert.True(step.GetProperty("step").GetProperty("IncludeAll").GetBoolean());
    }

    [Fact]
    public async Task SaveDeploymentPlan_RunsTheDeploymentStep()
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveDeploymentPlanTool(), services, new()
        {
            ["name"] = "Settings only",
            ["steps"] = JsonNode.Parse("""[ { "type": "TestExportStep", "step": { "IncludeAll": false } } ]"""),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var step = Assert.Single(harness.RecipeSteps);
        var plan = step.Step["Plans"][0];

        Assert.True(json.GetProperty("created").GetBoolean());
        Assert.Equal("deployment", step.Name);
        Assert.Equal("Settings only", plan["Name"].GetValue<string>());
        Assert.Equal("TestExportStep", plan["Steps"][0]["Type"].GetValue<string>());
    }

    [Fact]
    public async Task SaveDeploymentPlan_ThenListInTheSameScope_ListsTheNewPlanWithItsId()
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness();

        harness.Configure = services =>
        {
            // As Orchard Core's plan service does: the plans it reads first are kept for the rest of the scope,
            // which every tool call of one chat message shares.
            List<DeploymentPlan> cached = null;
            var planService = new Mock<IDeploymentPlanService>();
            planService.Setup(service => service.GetAllDeploymentPlansAsync())
                .ReturnsAsync(() => cached ??= [.. harness.Plans]);
            planService.Setup(service => service.GetDeploymentPlansAsync(It.IsAny<string[]>()))
                .ReturnsAsync((string[] names) => (cached ??= [.. harness.Plans]).Where(plan => names.Contains(plan.Name)).ToList());
            services.AddSingleton(planService.Object);
            services.AddSingleton<IRecipeStepHandler>(new SavingPlanStepHandler(harness));
        };

        using var services = harness.Build();

        // Act
        var saved = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SaveDeploymentPlanTool(), services, new()
        {
            ["name"] = "Settings only",
            ["steps"] = JsonNode.Parse("""[ { "type": "TestExportStep", "step": { "IncludeAll": false } } ]"""),
        }));
        var listed = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListDeploymentPlansTool(), services, new()));

        // Assert
        Assert.True(saved.GetProperty("created").GetBoolean());
        Assert.Equal(8, saved.GetProperty("id").GetInt64());
        Assert.Contains(listed.GetProperty("items").EnumerateArray(), item =>
            item.GetProperty("name").GetString() == "Settings only" && item.GetProperty("id").GetInt64() == 8);
    }

    [Fact]
    public async Task SaveDeploymentPlan_WhenAStepTypeIsUnknown_SavesNothing()
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveDeploymentPlanTool(), services, new()
        {
            ["name"] = "Broken",
            ["steps"] = JsonNode.Parse("""[ { "type": "NoSuchStep" } ]"""),
        });

        // Assert
        Assert.StartsWith("Nothing was saved: NoSuchStep is not a step type", result);
        Assert.Empty(harness.RecipeSteps);
    }

    [Fact]
    public async Task DeleteDeploymentPlan_DeletesThePlan()
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteDeploymentPlanTool(), services, new() { ["id"] = 7 });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("deleted").GetBoolean());
        Assert.Equal("Nightly", Assert.Single(harness.DeletedPlans).Name);
    }

    [Fact]
    public async Task DeleteDeploymentPlan_WhenThePlanIsMissing_DeletesNothing()
    {
        // Arrange
        var harness = new SiteAdministrationToolHarness();
        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteDeploymentPlanTool(), services, new() { ["id"] = 99 });

        // Assert
        Assert.StartsWith("Nothing was deleted: there is no deployment plan with the ID 99", result);
        Assert.Empty(harness.DeletedPlans);
    }

    /// <summary>
    /// Saves the plans of a <c>deployment</c> step as the recipe step does: a new plan gets the next ID.
    /// </summary>
    private sealed class SavingPlanStepHandler : IRecipeStepHandler
    {
        private readonly SiteAdministrationToolHarness _harness;

        public SavingPlanStepHandler(SiteAdministrationToolHarness harness)
        {
            _harness = harness;
        }

        public Task ExecuteAsync(RecipeExecutionContext context)
        {
            if (context.Name == DeploymentToolSupport.RecipeStepName && context.Step["Plans"] is JsonArray plans)
            {
                foreach (var plan in plans)
                {
                    var name = plan["Name"].GetValue<string>();

                    if (!_harness.Plans.Any(existing => existing.Name == name))
                    {
                        _harness.Plans.Add(new DeploymentPlan
                        {
                            Id = _harness.Plans.Max(existing => existing.Id) + 1,
                            Name = name,
                        });
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}
