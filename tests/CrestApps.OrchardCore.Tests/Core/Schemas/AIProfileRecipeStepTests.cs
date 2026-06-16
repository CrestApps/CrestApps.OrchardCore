using System.Text.Json;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

namespace CrestApps.OrchardCore.Tests.Core.Schemas;

public sealed class AIProfileRecipeStepTests
{
    [Fact]
    public async Task GetSchemaAsync_ContainsCurrentAndLegacyDeploymentSelectors()
    {
        var step = new AIProfileRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync(TestContext.Current.CancellationToken));

        Assert.Contains("\"ChatDeploymentName\"", json);
        Assert.Contains("\"UtilityDeploymentName\"", json);
        Assert.Contains("\"ChatDeploymentId\"", json);
        Assert.Contains("\"UtilityDeploymentId\"", json);
    }

    [Fact]
    public async Task CreateFromTemplateSchema_ContainsRequiredTemplateId()
    {
        var step = new CreateAIProfileFromTemplateRecipeStep();
        var json = JsonSerializer.Serialize(await step.GetSchemaAsync(TestContext.Current.CancellationToken));

        Assert.Contains("\"CreateAIProfileFromTemplate\"", json);
        Assert.Contains("\"TemplateId\"", json);
        Assert.Contains("\"Source\"", json);
        Assert.Contains("\"DisplayText\"", json);
        Assert.Contains("\"Agent\"", json);
        Assert.Contains("\"Description\"", json);
        Assert.Contains("\"PromptSubject\"", json);
        Assert.Contains("\"OrchestratorName\"", json);
        Assert.Contains("\"Properties\"", json);
        Assert.Contains("\"Settings\"", json);
        Assert.DoesNotContain("\"ChatDeploymentId\"", json);
        Assert.DoesNotContain("\"UtilityDeploymentId\"", json);
    }
}
