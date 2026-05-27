using System.Reflection;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Handlers;

public sealed class AIDeploymentHandlerTests
{
    [Fact]
    public async Task PopulateAsync_WhenLegacyTypeIsProvided_ShouldPopulatePurpose()
    {
        // Arrange
        var deployment = new AIDeployment();
#pragma warning disable CS0618 // Type or member is obsolete
        var data = new JsonObject
        {
            [nameof(AIDeployment.Type)] = new JsonArray("Chat", "Utility"),
        };
#pragma warning restore CS0618 // Type or member is obsolete

        // Act
        await InvokePopulateAsync(deployment, data);

        // Assert
        Assert.Equal(AIDeploymentPurpose.Chat | AIDeploymentPurpose.Utility, deployment.Purpose);
    }

    [Fact]
    public async Task PopulateAsync_WhenPurposeIsInvalidAndLegacyTypeIsProvided_ShouldFallbackToLegacyType()
    {
        // Arrange
        var deployment = new AIDeployment();
#pragma warning disable CS0618 // Type or member is obsolete
        var data = new JsonObject
        {
            [nameof(AIDeployment.Purpose)] = "InvalidPurpose",
            [nameof(AIDeployment.Type)] = "Embedding",
        };
#pragma warning restore CS0618 // Type or member is obsolete

        // Act
        await InvokePopulateAsync(deployment, data);

        // Assert
        Assert.Equal(AIDeploymentPurpose.Embedding, deployment.Purpose);
    }

    private static Task InvokePopulateAsync(AIDeployment deployment, JsonObject data)
    {
        var method = typeof(AIDeploymentCatalogExtensions).Assembly
            .GetType("CrestApps.OrchardCore.AI.Core.Handlers.AIDeploymentHandler", throwOnError: true)!
            .GetMethod(
                "PopulateAsync",
                BindingFlags.NonPublic | BindingFlags.Static)!;

        return (Task)method.Invoke(null, [deployment, data])!;
    }
}
