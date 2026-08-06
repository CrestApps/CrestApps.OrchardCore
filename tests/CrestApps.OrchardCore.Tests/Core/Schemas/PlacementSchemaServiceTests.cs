using System.Text.Json;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Placements.Filters;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Json.Schema;

namespace CrestApps.OrchardCore.Tests.Core.Schemas;

public sealed class PlacementSchemaServiceTests
{
    [Fact]
    public async Task GetFilterDescriptorsAsync_IncludesEveryRegisteredFilter()
    {
        var service = CreateService();

        var descriptors = await service.GetFilterDescriptorsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, descriptors.Count);
        Assert.Contains(descriptors, descriptor => descriptor.Key == "path");
        Assert.Contains(descriptors, descriptor => descriptor.Key == "contentType");
        Assert.Contains(descriptors, descriptor => descriptor.Key == "contentPart");
    }

    [Fact]
    public async Task GetPlacementNodeSchemaAsync_CachesResult()
    {
        var service = CreateService();

        var first = await service.GetPlacementNodeSchemaAsync(TestContext.Current.CancellationToken);
        var second = await service.GetPlacementNodeSchemaAsync(TestContext.Current.CancellationToken);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetPlacementNodeSchemaAsync_ValidatesANodeWithFilters()
    {
        // Arrange
        var schema = await CreatePlacementNodeSchemaAsync();

        var json = """
        {
          "place": "Content:1",
          "displayType": "Detail",
          "contentType": "Article",
          "path": ["/about", "~/blog/*"]
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetPlacementNodeSchemaAsync_AcceptsAnUnknownCustomFilter()
    {
        // Arrange
        var schema = await CreatePlacementNodeSchemaAsync();

        var json = """
        {
          "place": "Content:1",
          "culture": "en-US"
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetPlacementNodeSchemaAsync_RejectsANodeWithoutAPlace()
    {
        // Arrange
        var schema = await CreatePlacementNodeSchemaAsync();

        var json = """
        {
          "displayType": "Detail"
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.False(result);
    }

    private static async Task<JsonSchema> CreatePlacementNodeSchemaAsync()
    {
        var builder = await CreateService().GetPlacementNodeSchemaAsync(TestContext.Current.CancellationToken);

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

    private static PlacementSchemaService CreateService()
    {
        var filters = new IPlacementNodeFilterSchemaDefinition[]
        {
            new PathPlacementNodeFilterSchema(),
            new ContentTypePlacementNodeFilterSchema(),
            new ContentPartPlacementNodeFilterSchema(),
        };

        return new PlacementSchemaService(filters);
    }
}
