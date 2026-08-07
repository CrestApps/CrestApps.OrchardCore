using System.Text.Json;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Json.Schema;
using Moq;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Tests.Core.Schemas;

public sealed class DeploymentSchemaServiceTests
{
    [Fact]
    public async Task GetStepDescriptorsAsync_IncludesEveryAvailableStep()
    {
        var service = CreateService();

        var descriptors = await service.GetStepDescriptorsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(4, descriptors.Count);
        Assert.Contains(descriptors, descriptor => descriptor.StepType == "ContentDeploymentStep" && descriptor.HasSchemaDefinition);
        Assert.Contains(descriptors, descriptor => descriptor.StepType == "CustomFileDeploymentStep" && descriptor.HasSchemaDefinition);
        Assert.Contains(descriptors, descriptor => descriptor.StepType == "MediaDeploymentStep" && descriptor.HasSchemaDefinition);
        Assert.Contains(descriptors, descriptor => descriptor.StepType == "AllRolesDeploymentStep" && !descriptor.HasSchemaDefinition);
    }

    [Fact]
    public async Task GetStepDescriptorsAsync_SurfacesContentTypesAsSuggestions()
    {
        // Arrange
        var examples = new RecipeSchemaExamples
        {
            ContentTypeNames = ["Article", "BlogPost"],
        };

        var service = CreateService(examples);

        // Act
        var descriptors = await service.GetStepDescriptorsAsync(TestContext.Current.CancellationToken);

        // Assert
        var step = Assert.Single(descriptors, descriptor => descriptor.StepType == "ContentDeploymentStep");
        var contentTypes = Assert.Single(step.Properties, property => property.Name == "ContentTypes");

        var schemaNode = JsonSerializer.SerializeToNode(contentTypes.Schema.Build());
        var examplesNode = schemaNode?["items"]?["examples"]?.AsArray();

        Assert.NotNull(examplesNode);
        Assert.Equal(examples.ContentTypeNames, examplesNode.Select(example => example.GetValue<string>()).ToArray());
    }

    [Fact]
    public async Task GetStepSchemaAsync_CachesResult()
    {
        var service = CreateService();

        var first = await service.GetStepSchemaAsync(TestContext.Current.CancellationToken);
        var second = await service.GetStepSchemaAsync(TestContext.Current.CancellationToken);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetStepSchemaAsync_ValidatesADescribedStep()
    {
        // Arrange
        var schema = await CreateStepSchemaAsync();

        var json = """
        {
          "Type": "ContentDeploymentStep",
          "Step": {
            "ContentTypes": [ "BlogPost" ],
            "ExportAsSetupRecipe": true
          }
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetStepSchemaAsync_ValidatesAMarkerStep()
    {
        // Arrange
        var schema = await CreateStepSchemaAsync();

        var json = """
        {
          "Type": "AllRolesDeploymentStep",
          "Step": {}
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetStepSchemaAsync_AcceptsAnUnknownCustomStep()
    {
        // Arrange
        var schema = await CreateStepSchemaAsync();

        var json = """
        {
          "Type": "My.Custom.WeatherDeploymentStep",
          "Step": {
            "City": "Ottawa"
          }
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetStepSchemaAsync_RejectsAStepWithoutAType()
    {
        // Arrange
        var schema = await CreateStepSchemaAsync();

        var json = """
        {
          "Step": {}
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.False(result);
    }

    private static async Task<JsonSchema> CreateStepSchemaAsync()
    {
        var builder = await CreateService().GetStepSchemaAsync(TestContext.Current.CancellationToken);

        return builder.Build();
    }

    private static bool Evaluate(JsonSchema schema, string json)
    {
        using var document = JsonDocument.Parse(json);

        var result = schema.Evaluate(document.RootElement, new EvaluationOptions
        {
            OutputFormat = OutputFormat.Flag,
        });

        return result.IsValid;
    }

    private static DeploymentSchemaService CreateService()
        => CreateService(RecipeSchemaExamples.Empty);

    private static DeploymentSchemaService CreateService(RecipeSchemaExamples examples)
    {
        var definitions = new IDeploymentStepSchemaDefinition[]
        {
            new ContentDeploymentStepSchema(),
            new CustomFileDeploymentStepSchema(),
            new MediaDeploymentStepSchema(),
        };

        var factories = new[]
        {
            "ContentDeploymentStep",
            "CustomFileDeploymentStep",
            "MediaDeploymentStep",
            "AllRolesDeploymentStep",
        }.Select(name =>
        {
            var factory = new Mock<IDeploymentStepFactory>();
            factory.SetupGet(item => item.Name).Returns(name);

            return factory.Object;
        });

        return new DeploymentSchemaService(factories, definitions, new FakeRecipeSchemaExampleService(examples));
    }
}
