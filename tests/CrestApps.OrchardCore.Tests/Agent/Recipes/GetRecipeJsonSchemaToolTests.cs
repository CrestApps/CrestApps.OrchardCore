using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.Recipes;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Json.Schema;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Agent.Recipes;

public sealed class GetRecipeJsonSchemaToolTests
{
    [Fact]
    public async Task InvokeAsync_WithoutRequestedStep_ShouldReturnRootRecipeSchemaWithAllKnownStepNames()
    {
        // Arrange
        var featureStep = CreateRecipeStep(
            "feature",
            new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("feature")),
                    ("enable", new JsonSchemaBuilder().Type(SchemaValueType.Array)))
                .Required("name")
                .AdditionalProperties(true)
                .Build());
        var contentStep = CreateRecipeStep(
            "content",
            new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("content")),
                    ("data", new JsonSchemaBuilder().Type(SchemaValueType.Array)))
                .Required("name")
                .AdditionalProperties(true)
                .Build());
        var services = CreateServices([featureStep.Object, contentStep.Object]);
        var arguments = new AIFunctionArguments(new Dictionary<string, object>())
        {
            Services = services,
        };
        var tool = new GetRecipeJsonSchemaTool();

        // Act
        var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert
        var text = Assert.IsType<string>(result);
        using var document = JsonDocument.Parse(text);
        var stepItems = document.RootElement.GetProperty("properties").GetProperty("steps").GetProperty("items");
        var stepNameEnum = stepItems.GetProperty("properties").GetProperty("name").GetProperty("enum");
        var allOf = stepItems.GetProperty("allOf");

        Assert.Equal(["content", "feature"], stepNameEnum.EnumerateArray().Select(element => element.GetString()).Order().ToArray());
        Assert.Equal(2, allOf.GetArrayLength());
        Assert.Contains(allOf.EnumerateArray(), element => element.GetProperty("then").GetProperty("properties").TryGetProperty("enable", out _));
        Assert.Contains(allOf.EnumerateArray(), element => element.GetProperty("then").GetProperty("properties").TryGetProperty("data", out _));
    }

    [Fact]
    public async Task InvokeAsync_WithRequestedStep_ShouldReturnRootRecipeSchemaWithSingleStepDefinition()
    {
        // Arrange
        var featureStep = CreateRecipeStep(
            "feature",
            new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("feature")),
                    ("enable", new JsonSchemaBuilder().Type(SchemaValueType.Array)))
                .Required("name")
                .AdditionalProperties(true)
                .Build());
        var contentStep = CreateRecipeStep(
            "content",
            new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("content")),
                    ("data", new JsonSchemaBuilder().Type(SchemaValueType.Array)))
                .Required("name")
                .AdditionalProperties(true)
                .Build());
        var services = CreateServices([featureStep.Object, contentStep.Object]);
        var arguments = new AIFunctionArguments(new Dictionary<string, object>
        {
            ["step"] = "feature",
        })
        {
            Services = services,
        };
        var tool = new GetRecipeJsonSchemaTool();

        // Act
        var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert
        var text = Assert.IsType<string>(result);
        using var document = JsonDocument.Parse(text);
        var stepItems = document.RootElement.GetProperty("properties").GetProperty("steps").GetProperty("items");
        var stepNameEnum = stepItems.GetProperty("properties").GetProperty("name").GetProperty("enum");
        var allOf = stepItems.GetProperty("allOf");
        var allOfEntries = allOf.EnumerateArray().ToArray();
        var selectedStep = allOfEntries[0].GetProperty("then");

        Assert.Equal(["content", "feature"], stepNameEnum.EnumerateArray().Select(element => element.GetString()).Order().ToArray());
        Assert.Single(allOfEntries);
        Assert.Equal("feature", selectedStep.GetProperty("properties").GetProperty("name").GetProperty("const").GetString());
        Assert.True(selectedStep.GetProperty("properties").TryGetProperty("enable", out _));
        Assert.False(selectedStep.GetProperty("properties").TryGetProperty("data", out _));
    }

    [Fact]
    public async Task InvokeAsync_WithUnknownRequestedStep_ShouldReturnUnknownMessage()
    {
        // Arrange
        var featureStep = CreateRecipeStep(
            "feature",
            new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("feature")))
                .Required("name")
                .AdditionalProperties(true)
                .Build());
        var services = CreateServices([featureStep.Object]);
        var arguments = new AIFunctionArguments(new Dictionary<string, object>
        {
            ["step"] = "unknown",
        })
        {
            Services = services,
        };
        var tool = new GetRecipeJsonSchemaTool();

        // Act
        var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Unknown recipe step 'unknown'. Available steps: feature", result);
    }

    private static ServiceProvider CreateServices(IEnumerable<IRecipeStep> recipeSteps)
    {
        var recipeSchemaService = new RecipeSchemaService(
            [],
            recipeSteps,
            new MemoryCache(new MemoryCacheOptions()));

        return new ServiceCollection()
            .AddSingleton<RecipeSchemaService>(recipeSchemaService)
            .AddSingleton<ILogger<GetRecipeJsonSchemaTool>>(NullLogger<GetRecipeJsonSchemaTool>.Instance)
            .BuildServiceProvider();
    }

    private static Mock<IRecipeStep> CreateRecipeStep(string name, JsonSchema schema)
    {
        var step = new Mock<IRecipeStep>();
        step.SetupGet(x => x.Name).Returns(name);
        step.Setup(x => x.GetSchemaAsync(It.IsAny<CancellationToken>())).ReturnsAsync(schema);

        return step;
    }
}
