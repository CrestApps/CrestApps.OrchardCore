using System.Text.Json;
using CrestApps.OrchardCore.Recipes.Core.Schemas;

namespace CrestApps.OrchardCore.Tests.Core.Schemas;

public sealed class AIProfileRecipeStepTests
{
    [Fact]
    public async Task GetSchemaAsync_ContainsCurrentAndLegacyDeploymentSelectors()
    {
        var step = new AIProfileRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync());

        Assert.Contains("\"ChatDeploymentName\"", json);
        Assert.Contains("\"UtilityDeploymentName\"", json);
        Assert.Contains("\"ChatDeploymentId\"", json);
        Assert.Contains("\"UtilityDeploymentId\"", json);
    }

    [Fact]
    public async Task CreateFromTemplateSchema_ContainsRequiredTemplateId()
    {
        var step = new CreateAIProfileFromTemplateRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync());

        Assert.Contains("\"CreateAIProfileFromTemplate\"", json);
        Assert.Contains("\"TemplateId\"", json);
    }
}
