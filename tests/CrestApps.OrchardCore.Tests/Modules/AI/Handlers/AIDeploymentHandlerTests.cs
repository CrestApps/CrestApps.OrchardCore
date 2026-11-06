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

    [Fact]
    public async Task PopulateAsync_ReadsThePricingSection()
    {
        // Arrange
        var deployment = new AIDeployment();
        var data = JsonNode.Parse("""
            {
              "Pricing": {
                "currency": "eur",
                "Prices": [
                  { "Meter": "InputTokens", "Amount": 2.5, "Per": 1000000 },
                  { "Meter": "InputTokens", "Amount": "5", "Per": 1000000, "AboveInputTokens": 200000 },
                  { "Meter": "AdditionalCount", "Amount": 0.05, "Per": 1, "CountName": "CodeExecutionSeconds" },
                  { "Meter": "Unknown", "Amount": 1, "Per": 1 }
                ]
              }
            }
            """)!.AsObject();

        // Act
        await InvokePopulateAsync(deployment, data);

        // Assert
        var pricing = deployment.GetPricing();
        Assert.NotNull(pricing);
        Assert.Equal("EUR", pricing.Currency);
        Assert.Equal(3, pricing.Prices.Count);
        Assert.Equal(200_000, pricing.Prices[1].AboveInputTokens);
        Assert.Equal("CodeExecutionSeconds", pricing.Prices[2].CountName);
    }

    [Fact]
    public async Task PopulateAsync_KeepsThePricesWithoutAPricingSection_AndRemovesThemWithANullOne()
    {
        // Arrange
        var deployment = new AIDeployment();
        deployment.SetPricing("USD", [new AIDeploymentPrice { Meter = "OutputTokens", Amount = 10, Per = 1_000_000 }]);

        // Act
        await InvokePopulateAsync(deployment, new JsonObject { ["ModelName"] = "gpt" });
        var kept = deployment.GetPricing();
        await InvokePopulateAsync(deployment, new JsonObject { ["Pricing"] = null });

        // Assert
        Assert.NotNull(kept);
        Assert.Null(deployment.GetPricing());
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
