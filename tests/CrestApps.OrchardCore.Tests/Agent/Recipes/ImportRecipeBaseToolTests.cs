using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Json.Schema;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Deployment;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.Tests.Agent.Recipes;

public sealed class ImportRecipeBaseToolTests
{
    [Fact]
    public async Task InvokeAsync_WithUnknownStepName_ShouldReturnSharedSchemaValidationMessage()
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
            ["recipe"] = """{"steps":[{"name":"unknown"}]}""",
        })
        {
            Services = services,
        };
        var tool = new TestImportRecipeTool();

        // Act
        var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert
        var text = Assert.IsType<string>(result);
        Assert.Contains("Invalid recipe format.", text, StringComparison.Ordinal);
        Assert.Contains("\"steps\"", text, StringComparison.Ordinal);
        Assert.Contains("\"enum\":[\"content\",\"feature\"]", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_WithValidRecipe_ShouldReturnSuccessMessage()
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
        var services = CreateServices([featureStep.Object]);
        var arguments = new AIFunctionArguments(new Dictionary<string, object>
        {
            ["recipe"] = """{"steps":[{"name":"feature","enable":["OrchardCore.Title"]}]}""",
        })
        {
            Services = services,
        };
        var tool = new TestImportRecipeTool();

        // Act
        var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert
        var text = Assert.IsType<string>(result);
        Assert.Equal("Recipe was successfully imported", text);
    }

    private static ServiceProvider CreateServices(IEnumerable<IRecipeStep> recipeSteps)
    {
        var recipeSchemaService = new RecipeSchemaService(
            [],
            recipeSteps,
            new MemoryCache(new MemoryCacheOptions()));
        var recipeExecutionService = new RecipeExecutionService(
            [],
            Options.Create(new DocumentJsonSerializerOptions()),
            NullLogger<RecipeExecutionService>.Instance);

        return new ServiceCollection()
            .AddSingleton(recipeSchemaService)
            .AddSingleton(recipeExecutionService)
            .AddSingleton<ILogger<ImportRecipeBaseTool>>(NullLogger<ImportRecipeBaseTool>.Instance)
            .BuildServiceProvider();
    }

    private static Mock<IRecipeStep> CreateRecipeStep(string name, JsonSchema schema)
    {
        var step = new Mock<IRecipeStep>();
        step.SetupGet(x => x.Name).Returns(name);
        step.Setup(x => x.GetSchemaAsync(It.IsAny<CancellationToken>())).ReturnsAsync(schema);

        return step;
    }

    private sealed class TestImportRecipeTool : ImportRecipeBaseTool
    {
        public override string Name => "testImportRecipe";

        public override string Description => "Test import recipe tool.";
    }
}
