using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Recipes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Recipes;

/// <summary>
/// Round 3 (Medium, AI): the catalog saves an update as it applies it, and these steps checked the endpoint only when
/// they validated the updated entry, after the save. A recipe could point an existing deployment, provider connection or
/// tool instance at the site's own network. The A2A and MCP steps already refuse it from what the recipe sends (C13).
/// </summary>
public sealed class AIRecipeUpdateEndpointTests
{
    [Fact]
    public async Task AIDeploymentStep_AnUpdateToTheSiteOwnNetwork_IsRefused_BeforeItIsSaved()
    {
        // Arrange
        var existing = new AIDeployment { ItemId = "deployment-1", Source = "OpenAI", Name = "speech" };
        var manager = new Mock<IAIDeploymentManager>();
        manager.Setup(x => x.FindByIdAsync("deployment-1", It.IsAny<CancellationToken>())).Returns(() => ValueTask.FromResult(existing));
        var step = new AIDeploymentStep(manager.Object, Options.Create(new AIOptions()), Policy(), new PassThroughLocalizer<AIDeploymentStep>());
        var context = Context(AIDeploymentStep.StepKey, "Deployments", new JsonObject
        {
            [nameof(AIDeployment.ItemId)] = "deployment-1",
            ["ClientName"] = "OpenAI",
            [nameof(AIDeployment.Name)] = "speech",
            ["Properties"] = new JsonObject { ["Endpoint"] = "http://10.0.0.5/speech" },
        });

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => step.ExecuteAsync(context));

        // Assert
        Assert.Contains("site's own network", exception.Message, StringComparison.Ordinal);
        manager.Verify(x => x.UpdateAsync(It.IsAny<AIDeployment>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AIProviderConnectionsStep_AnUpdateToTheSiteOwnNetwork_IsRefused_BeforeItIsSaved()
    {
        // Arrange
        var existing = new AIProviderConnection { ItemId = "connection-1", Source = "OpenAI", Name = "default" };
        var manager = new Mock<INamedSourceCatalogManager<AIProviderConnection>>();
        manager.Setup(x => x.FindByIdAsync("connection-1", It.IsAny<CancellationToken>())).Returns(() => ValueTask.FromResult(existing));
        var step = new AIProviderConnectionsStep(manager.Object, Options.Create(new AIOptions()), Policy(), new PassThroughLocalizer<AIProfileStep>());
        var context = Context(AIProviderConnectionsStep.StepKey, "Connections", new JsonObject
        {
            [nameof(AIProviderConnection.ItemId)] = "connection-1",
            [nameof(AIProviderConnection.Source)] = "OpenAI",
            [nameof(AIProviderConnection.Name)] = "default",
            ["Properties"] = new JsonObject { ["Endpoint"] = "http://169.254.169.254/latest/meta-data/" },
        });

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => step.ExecuteAsync(context));

        // Assert
        Assert.Contains("site's own network", exception.Message, StringComparison.Ordinal);
        manager.Verify(x => x.UpdateAsync(It.IsAny<AIProviderConnection>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AIToolInstanceStep_AnUpdateToTheSiteOwnNetwork_IsRefused_BeforeItIsSaved()
    {
        // Arrange
        var existing = new AIToolInstance { ItemId = "instance-1", Source = "http-api-request", Name = "Order Lookup API" };
        var manager = new Mock<ISourceCatalogManager<AIToolInstance>>();
        manager.Setup(x => x.FindByIdAsync("instance-1", It.IsAny<CancellationToken>())).Returns(() => ValueTask.FromResult(existing));
        var step = new AIToolInstanceStep(manager.Object, Mock.Of<INamedCatalog<AIToolInstance>>(), Options.Create(new AIOptions()), Policy(), new PassThroughLocalizer<AIToolInstanceStep>());
        var context = Context(AIToolInstanceStep.StepKey, "Instances", new JsonObject
        {
            [nameof(AIToolInstance.ItemId)] = "instance-1",
            [nameof(AIToolInstance.Source)] = "http-api-request",
            [nameof(AIToolInstance.Name)] = "Order Lookup API",
            ["Properties"] = new JsonObject
            {
                ["HttpApiRequestToolSettings"] = new JsonObject { ["BaseUrl"] = "http://169.254.169.254/latest/meta-data/" },
            },
        });

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => step.ExecuteAsync(context));

        // Assert
        Assert.Contains("site's own network", exception.Message, StringComparison.Ordinal);
        manager.Verify(x => x.UpdateAsync(It.IsAny<AIToolInstance>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // The policy the AI feature registers, on a production host that allows no private network.
    private static AIEndpointPolicy Policy()
        => new(Mock.Of<IHostEnvironment>(), Options.Create(new AIEgressOptions()), _ => [IPAddress.Parse("203.0.113.10")]);

    private static RecipeExecutionContext Context(string stepName, string entriesName, JsonObject entry)
        => new()
        {
            Name = stepName,
            Step = new JsonObject
            {
                ["name"] = stepName,
                [entriesName] = new JsonArray(entry),
            },
        };

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
