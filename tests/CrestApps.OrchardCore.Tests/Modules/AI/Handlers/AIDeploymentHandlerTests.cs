using System.Reflection;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Handlers;

public sealed class AIDeploymentHandlerTests
{
    [Fact]
    public async Task PopulateAsync_WhenLegacyTypeIsProvided_ShouldProjectItOntoCapabilities()
    {
        // Arrange
        var deployment = new AIDeployment();
        var data = new JsonObject
        {
            ["Type"] = new JsonArray("Chat", "Utility"),
        };

        // Act
        await InvokePopulateAsync(deployment, data);

        // Assert
        Assert.True(deployment.TryGet<AIDeploymentMetadata>(out var metadata));
        Assert.True(metadata.SupportsFeature(AIDeploymentFeatureNames.TextGeneration));
    }

    [Fact]
    public async Task PopulateAsync_WhenPurposeIsUnrecognized_ShouldNotDeclareAnyCapability()
    {
        // Arrange
        var deployment = new AIDeployment();
        var data = new JsonObject
        {
            ["Purpose"] = "InvalidPurpose",
            ["Type"] = "Embedding",
        };

        // Act
        await InvokePopulateAsync(deployment, data);

        // Assert
        // Purpose, Capability, and Type are three names for one field, so the first one present is the one
        // read -- the framework's own handler behaves identically. A value naming nothing the projection
        // recognizes therefore implies no capability, and validation rejects the payload rather than
        // quietly guessing a different field's answer.
        Assert.False(deployment.TryGet<AIDeploymentMetadata>(out _));
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
