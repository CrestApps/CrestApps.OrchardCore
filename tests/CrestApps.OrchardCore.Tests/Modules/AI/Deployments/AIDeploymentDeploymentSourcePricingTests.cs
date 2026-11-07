using System.IO;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Deployments.Sources;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using Moq;
using OrchardCore.Deployment;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Deployments;

/// <summary>
/// The AIDeployment deployment step exports each deployment's prices in its own Pricing section.
/// </summary>
public sealed class AIDeploymentDeploymentSourcePricingTests
{
    [Fact]
    public async Task ProcessAsync_ExportsThePricingSection_AndLeavesItOutOfProperties()
    {
        // Arrange
        var priced = new AIDeployment { ItemId = "1", Name = "gpt-4o", ClientName = "OpenAI" };
        priced.SetPricing("USD",
        [
            new AIDeploymentPrice { Meter = AIUsageMeters.InputTokens, Amount = 2.5m, Per = 1_000_000 },
            new AIDeploymentPrice { Meter = AIUsageMeters.InputTokens, Amount = 5m, Per = 1_000_000, AboveInputTokens = 200_000 },
        ]);
        var unpriced = new AIDeployment { ItemId = "2", Name = "whisper", ClientName = "OpenAI" };

        var catalog = new Mock<INamedSourceCatalog<AIDeployment>>();
        catalog
            .Setup(store => store.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { priced, unpriced });

        var source = new AIDeploymentDeploymentSource(catalog.Object);
        var result = new DeploymentPlanResult(new NullFileBuilder(), new RecipeDescriptor());

        // Act
        await source.ProcessDeploymentStepAsync(new AIDeploymentDeploymentStep { IncludeAll = true }, result);

        // Assert
        var step = Assert.IsType<JsonObject>(Assert.Single(result.Steps));
        var deployments = Assert.IsType<JsonArray>(step["Deployments"]);
        var exported = Assert.IsType<JsonObject>(deployments[0]);

        Assert.Equal("USD", exported["Pricing"]?["Currency"]?.GetValue<string>());
        Assert.Equal(2, exported["Pricing"]?["Prices"]?.AsArray().Count);
        Assert.Equal(200_000, exported["Pricing"]?["Prices"]?[1]?["AboveInputTokens"]?.GetValue<long>());
        Assert.False(exported["Pricing"]?["Prices"]?[0]?.AsObject().ContainsKey(nameof(AIDeploymentPrice.CountName)));
        Assert.Null(exported["Properties"]?[nameof(AIDeploymentPricing)]);
        Assert.Null(deployments[1]?["Pricing"]);
    }

    private sealed class NullFileBuilder : IFileBuilder
    {
        public Task SetFileAsync(string subpath, Stream stream)
        {
            return Task.CompletedTask;
        }
    }
}
