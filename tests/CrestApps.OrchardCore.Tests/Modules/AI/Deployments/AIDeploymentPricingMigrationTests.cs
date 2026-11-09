using System.Text.Json.Nodes;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Migrations;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Deployments;

/// <summary>
/// A price line from the old AI settings page becomes the same prices on the deployment.
/// </summary>
public sealed class AIDeploymentPricingMigrationTests
{
    [Fact]
    public void ToPrices_MapsEachOldColumnToItsMeter()
    {
        // Arrange
        var entry = JsonNode.Parse("""
            { "DeploymentName": "gpt-realtime", "InputPerMillion": 4, "OutputPerMillion": 16, "AudioInputPerMillion": 32, "AudioOutputPerMillion": "64", "PerMinute": 0 }
            """)!.AsObject();
        var deployment = new AIDeployment();

        // Act
        deployment.SetPricing("usd", AIDeploymentPricingMigrations.ToPrices(entry));

        // Assert
        var pricing = deployment.GetPricing();
        Assert.NotNull(pricing);
        Assert.Equal("USD", pricing.Currency);
        Assert.Collection(
            pricing.Prices,
            price => AssertPrice(price, AIUsageMeters.InputTokens, 4),
            price => AssertPrice(price, AIUsageMeters.OutputTokens, 16),
            price => AssertPrice(price, AIUsageMeters.InputAudioTokens, 32),
            price => AssertPrice(price, AIUsageMeters.OutputAudioTokens, 64));
    }

    [Fact]
    public void ToPrices_MapsAPerMinutePriceToAudioSeconds()
    {
        var entry = JsonNode.Parse("""{ "DeploymentName": "whisper", "PerMinute": 0.006 }""")!.AsObject();
        var deployment = new AIDeployment();

        deployment.SetPricing("USD", AIDeploymentPricingMigrations.ToPrices(entry));

        var price = Assert.Single(deployment.GetPricing().Prices);
        Assert.Equal(AIUsageMeters.AudioSeconds, price.Meter);
        Assert.Equal(0.006m, price.Amount);
        Assert.Equal(60, price.Per);
    }

    private static void AssertPrice(AIDeploymentPrice price, string meter, decimal amount)
    {
        Assert.Equal(meter, price.Meter);
        Assert.Equal(amount, price.Amount);
        Assert.Equal(1_000_000, price.Per);
    }
}
