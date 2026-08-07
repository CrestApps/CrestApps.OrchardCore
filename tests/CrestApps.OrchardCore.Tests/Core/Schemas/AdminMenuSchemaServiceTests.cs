using System.Text.Json;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas;
using CrestApps.OrchardCore.Recipes.Core.Schemas.AdminMenu.Nodes;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Json.Schema;

namespace CrestApps.OrchardCore.Tests.Core.Schemas;

public sealed class AdminMenuSchemaServiceTests
{
    [Fact]
    public async Task GetNodeDescriptorsAsync_IncludesEveryRegisteredNode()
    {
        var service = CreateService();

        var descriptors = await service.GetNodeDescriptorsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(4, descriptors.Count);
        Assert.Contains(descriptors, descriptor => descriptor.Name == "LinkAdminNode");
        Assert.Contains(descriptors, descriptor => descriptor.Name == "PlaceholderAdminNode");
        Assert.Contains(descriptors, descriptor => descriptor.Name == "ContentTypesAdminNode");
        Assert.Contains(descriptors, descriptor => descriptor.Name == "ListsAdminNode");
    }

    [Fact]
    public async Task GetAdminMenuSchemaAsync_CachesResult()
    {
        var service = CreateService();

        var first = await service.GetAdminMenuSchemaAsync(TestContext.Current.CancellationToken);
        var second = await service.GetAdminMenuSchemaAsync(TestContext.Current.CancellationToken);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetAdminMenuSchemaAsync_ValidatesAMenuWithNestedNodes()
    {
        // Arrange
        var schema = await CreateAdminMenuSchemaAsync();

        var json = """
        {
          "Id": "baef6f85ad13481681cde70ada401333",
          "Name": "Admin menus",
          "Enabled": true,
          "MenuItems": [
            {
              "$type": "OrchardCore.AdminMenu.AdminNodes.LinkAdminNode, OrchardCore.AdminMenu",
              "LinkText": "Blog",
              "LinkUrl": "/blog",
              "IconClass": "fas fa-rss",
              "PermissionNames": [],
              "UniqueId": "7b293d57056a4eebb3713f07f12c65d8",
              "Enabled": true,
              "Items": []
            },
            {
              "$type": "OrchardCore.AdminMenu.AdminNodes.PlaceholderAdminNode, OrchardCore.AdminMenu",
              "LinkText": "Content",
              "Items": [
                {
                  "$type": "OrchardCore.Contents.AdminNodes.ContentTypesAdminNode, OrchardCore.Contents",
                  "ShowAll": false,
                  "ContentTypes": [
                    {
                      "ContentTypeName": "Article",
                      "ContentTypeDisplayName": "Article"
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetAdminMenuSchemaAsync_ValidatesAListsNode()
    {
        // Arrange
        var schema = await CreateAdminMenuSchemaAsync();

        var json = """
        {
          "Name": "Admin menus",
          "MenuItems": [
            {
              "$type": "OrchardCore.Lists.AdminNodes.ListsAdminNode, OrchardCore.Lists",
              "ContentType": "BlogPost",
              "AddContentTypeAsParent": true
            }
          ]
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetAdminMenuSchemaAsync_AcceptsAnUnknownCustomNode()
    {
        // Arrange
        var schema = await CreateAdminMenuSchemaAsync();

        var json = """
        {
          "Name": "Admin menus",
          "MenuItems": [
            {
              "$type": "My.Custom.WeatherAdminNode, My.Custom",
              "City": "Ottawa"
            }
          ]
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetAdminMenuSchemaAsync_RejectsANodeWithoutADiscriminator()
    {
        // Arrange
        var schema = await CreateAdminMenuSchemaAsync();

        var json = """
        {
          "Name": "Admin menus",
          "MenuItems": [
            {
              "LinkText": "Blog"
            }
          ]
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.False(result);
    }

    private static async Task<JsonSchema> CreateAdminMenuSchemaAsync()
    {
        var builder = await CreateService().GetAdminMenuSchemaAsync(TestContext.Current.CancellationToken);

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
    public async Task GetNodeDescriptorsAsync_SurfacesContentTypesAsSuggestions()
    {
        // Arrange
        var examples = new RecipeSchemaExamples
        {
            ContentTypeNames = ["Article", "BlogPost"],
        };

        var service = CreateService(examples);

        // Act
        var descriptors = await service.GetNodeDescriptorsAsync(TestContext.Current.CancellationToken);

        // Assert
        var lists = Assert.Single(descriptors, descriptor => descriptor.Name == "ListsAdminNode");
        var contentType = Assert.Single(lists.Properties, property => property.Name == "ContentType");

        var schemaNode = JsonSerializer.SerializeToNode(contentType.Schema.Build());
        var examplesNode = schemaNode?["examples"]?.AsArray();

        Assert.NotNull(examplesNode);
        Assert.Equal(examples.ContentTypeNames, examplesNode.Select(example => example.GetValue<string>()).ToArray());
    }

    private static AdminMenuSchemaService CreateService()
        => CreateService(RecipeSchemaExamples.Empty);

    private static AdminMenuSchemaService CreateService(RecipeSchemaExamples examples)
    {
        var nodes = new IAdminNodeSchemaDefinition[]
        {
            new LinkAdminNodeSchema(),
            new PlaceholderAdminNodeSchema(),
            new ContentTypesAdminNodeSchema(),
            new ListsAdminNodeSchema(),
        };

        return new AdminMenuSchemaService(nodes, new FakeRecipeSchemaExampleService(examples));
    }
}
