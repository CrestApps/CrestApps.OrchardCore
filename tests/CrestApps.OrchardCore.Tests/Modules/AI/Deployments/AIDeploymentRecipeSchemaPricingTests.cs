using System.Text.Json;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Deployments;

/// <summary>
/// The AIDeployment recipe step schema describes the pricing a deployment step exports and a recipe imports.
/// </summary>
public sealed class AIDeploymentRecipeSchemaPricingTests
{
    [Fact]
    public async Task Schema_AcceptsAPricingSection()
    {
        var result = await EvaluateAsync("""
            { "Meter": "CacheWriteInputTokens", "Amount": 3.75, "Per": 1000000 },
            { "Meter": "InputTokens", "Amount": 6, "Per": 1000000, "AboveInputTokens": 200000 },
            { "Meter": "AudioSeconds", "Amount": 0.006, "Per": 60, "Increment": 15 },
            { "Meter": "AdditionalCount", "Amount": 0.05, "Per": 1, "CountName": "CodeExecutionSeconds" }
            """);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("""{ "Meter": "NotAMeter", "Amount": 1, "Per": 1 }""")]
    [InlineData("""{ "Meter": "InputTokens", "Amount": -1, "Per": 1 }""")]
    [InlineData("""{ "Meter": "InputTokens", "Amount": 1 }""")]
    public async Task Schema_RejectsAnInvalidPrice(string price)
    {
        var result = await EvaluateAsync(price);

        Assert.False(result.IsValid);
    }

    private static async Task<EvaluationResults> EvaluateAsync(string prices)
    {
        var step = new AIDeploymentRecipeStep(Options.Create(new AIOptions()), Options.Create(new AIDeploymentCapabilityOptions()));
        var schema = await step.GetSchemaAsync(TestContext.Current.CancellationToken);
        using var recipeStep = JsonDocument.Parse($$"""
            {
              "name": "AIDeployment",
              "Deployments": [
                {
                  "Name": "gpt-4o",
                  "ClientName": "OpenAI",
                  "ConnectionName": "default",
                  "Pricing": { "Currency": "USD", "Prices": [ {{prices}} ] }
                }
              ]
            }
            """);

        return schema.Evaluate(recipeStep.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
    }
}
