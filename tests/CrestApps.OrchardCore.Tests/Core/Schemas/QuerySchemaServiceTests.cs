using System.Text.Json;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Queries.Sources;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Json.Schema;

namespace CrestApps.OrchardCore.Tests.Core.Schemas;

public sealed class QuerySchemaServiceTests
{
    [Fact]
    public async Task GetSourceDescriptorsAsync_IncludesEveryRegisteredSource()
    {
        var service = CreateService();

        var descriptors = await service.GetSourceDescriptorsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, descriptors.Count);
        Assert.Contains(descriptors, descriptor => descriptor.Name == "Sql");
        Assert.Contains(descriptors, descriptor => descriptor.Name == "Lucene");
        Assert.Contains(descriptors, descriptor => descriptor.Name == "Elasticsearch");
    }

    [Fact]
    public async Task GetQuerySchemaAsync_CachesResult()
    {
        var service = CreateService();

        var first = await service.GetQuerySchemaAsync(TestContext.Current.CancellationToken);
        var second = await service.GetQuerySchemaAsync(TestContext.Current.CancellationToken);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetQuerySchemaAsync_ValidatesASqlQuery()
    {
        // Arrange
        var schema = await CreateQuerySchemaAsync();

        var json = """
        {
          "Source": "Sql",
          "Name": "RecentBlogPosts",
          "Template": "SELECT DocumentId FROM ContentItemIndex WHERE Published = 1",
          "Schema": "{}",
          "ReturnContentItems": true
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetQuerySchemaAsync_ValidatesALuceneQuery()
    {
        // Arrange
        var schema = await CreateQuerySchemaAsync();

        var json = """
        {
          "Source": "Lucene",
          "Name": "RecentBlogPosts",
          "Index": "Search",
          "Template": "{ }",
          "ReturnContentItems": true
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetQuerySchemaAsync_AcceptsAnUnknownCustomSource()
    {
        // Arrange
        var schema = await CreateQuerySchemaAsync();

        var json = """
        {
          "Source": "Weather",
          "Name": "Forecast",
          "City": "Ottawa"
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetQuerySchemaAsync_RejectsAQueryWithoutASource()
    {
        // Arrange
        var schema = await CreateQuerySchemaAsync();

        var json = """
        {
          "Name": "RecentBlogPosts",
          "Template": "SELECT 1"
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.False(result);
    }

    private static async Task<JsonSchema> CreateQuerySchemaAsync()
    {
        var builder = await CreateService().GetQuerySchemaAsync(TestContext.Current.CancellationToken);

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

    [Fact]
    public async Task GetSourceDescriptorsAsync_SurfacesIndexProfileNamesAsSuggestions()
    {
        // Arrange
        var examples = new RecipeSchemaExamples
        {
            IndexProfileNames = ["articles", "products"],
        };

        var service = CreateService(examples);

        // Act
        var descriptors = await service.GetSourceDescriptorsAsync(TestContext.Current.CancellationToken);

        // Assert
        var lucene = Assert.Single(descriptors, descriptor => descriptor.Name == "Lucene");
        var index = Assert.Single(lucene.Properties, property => property.Name == "Index");

        var schemaNode = JsonSerializer.SerializeToNode(index.Schema.Build());
        var examplesNode = schemaNode?["examples"]?.AsArray();

        Assert.NotNull(examplesNode);
        Assert.Equal(examples.IndexProfileNames, examplesNode.Select(example => example.GetValue<string>()).ToArray());
    }

    private static QuerySchemaService CreateService()
        => CreateService(RecipeSchemaExamples.Empty);

    private static QuerySchemaService CreateService(RecipeSchemaExamples examples)
    {
        var sources = new IQuerySourceSchemaDefinition[]
        {
            new SqlQuerySourceSchema(),
            new LuceneQuerySourceSchema(),
            new ElasticsearchQuerySourceSchema(),
        };

        return new QuerySchemaService(sources, new FakeRecipeSchemaExampleService(examples));
    }
}
