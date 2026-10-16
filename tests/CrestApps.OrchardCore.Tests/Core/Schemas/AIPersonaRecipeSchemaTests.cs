using System.Text.Json;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

namespace CrestApps.OrchardCore.Tests.Core.Schemas;

public sealed class AIPersonaRecipeSchemaTests
{
    [Fact]
    public async Task AIPersonaRecipeStep_DescribesEveryPersonaFieldAndRequiresTheName()
    {
        var step = new AIPersonaRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync(TestContext.Current.CancellationToken));

        Assert.Equal("AIPersonas", step.Name);
        Assert.Contains("\"personas\"", json);

        foreach (var property in new[] { "ItemId", "Name", "DisplayText", "Description", "Instructions", "AssistantName", "Language", "VoiceName", "VoiceDeploymentName", "AvatarUrl", "Properties" })
        {
            Assert.Contains($"\"{property}\"", json);
        }

        Assert.Contains("\"required\":[\"Name\"]", json);
    }

    [Fact]
    public async Task AIPersonaDeploymentStepSchema_DescribesTheSelection()
    {
        IDeploymentStepSchemaDefinition definition = new AIPersonaDeploymentStepSchema();

        var schema = await definition.GetStepSchemaAsync(
            new DeploymentStepSchemaContext { StepType = definition.StepType },
            TestContext.Current.CancellationToken);

        Assert.Equal("AIPersonaDeploymentStep", definition.StepType);
        Assert.Equal(["IncludeAll", "PersonaIds"], schema.Properties.Select(property => property.Name));
    }

    [Fact]
    public async Task AIProfileRecipeStep_DescribesThePersonaLink()
    {
        var json = JsonSerializer.Serialize(await new AIProfileRecipeStep().GetSchemaAsync(TestContext.Current.CancellationToken));

        Assert.Contains("\"PersonaId\"", json);
        Assert.Contains("\"AIPersonaMetadata\"", json);
    }
}
