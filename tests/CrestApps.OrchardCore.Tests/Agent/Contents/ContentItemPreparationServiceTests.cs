using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Contents;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using Json.Schema;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.Tests.Agent.Contents;

public sealed class ContentItemPreparationServiceTests
{
    [Fact]
    public async Task PrepareAsync_WhenRecipeContainedPartSchemasAreAvailable_ShouldInitializeNestedItemsRecursively()
    {
        // Arrange
        var landingPage = CreateContentTypeDefinition(
            "LandingPage",
            ("Body", "FlowPart"));
        var heroWidget = CreateContentTypeDefinition(
            "HeroWidget",
            ("Items", "BagPart"));
        var promoBlock = CreateContentTypeDefinition("PromoBlock");
        var contentManager = CreateContentManager(new Dictionary<string, ContentItem>(StringComparer.OrdinalIgnoreCase)
        {
            ["LandingPage"] = new()
            {
                ContentType = "LandingPage",
            },
            ["HeroWidget"] = new()
            {
                ContentType = "HeroWidget",
                Content =
                {
                    ["HeroPart"] = new JsonObject
                    {
                        ["Alignment"] = "Center",
                    },
                },
            },
            ["PromoBlock"] = new()
            {
                ContentType = "PromoBlock",
                Content =
                {
                    ["PromoPart"] = new JsonObject
                    {
                        ["Style"] = "Primary",
                    },
                },
            },
        });
        var service = new ContentItemPreparationService(
            contentManager.Object,
            CreateContentDefinitionManager(landingPage, heroWidget, promoBlock).Object,
            [new FlowPartSchema(), new BagPartSchema()],
            Options.Create(new DocumentJsonSerializerOptions()));
        var inputNode = JsonNode.Parse(
            """
            {
              "ContentType": "LandingPage",
              "Body": {
                "Widgets": [
                  {
                    "ContentType": "HeroWidget",
                    "HeroPart": {
                      "Title": "Welcome"
                    },
                    "Items": {
                      "ContentItems": [
                        {
                          "ContentType": "PromoBlock",
                          "PromoPart": {
                            "Text": "Buy now"
                          }
                        }
                      ]
                    }
                  }
                ]
              }
            }
            """) as JsonObject;

        Assert.NotNull(inputNode);

        // Act
        var prepared = await service.PrepareAsync(
            landingPage,
            inputNode,
            cancellationToken: TestContext.Current.CancellationToken);
        var preparedNode = JsonSerializer.SerializeToNode(
            prepared,
            new DocumentJsonSerializerOptions().SerializerOptions) as JsonObject;

        // Assert
        Assert.NotNull(preparedNode);

        contentManager.Verify(x => x.NewAsync("LandingPage"), Times.Once);
        contentManager.Verify(x => x.NewAsync("HeroWidget"), Times.Once);
        contentManager.Verify(x => x.NewAsync("PromoBlock"), Times.Once);

        var bodyPart = Assert.IsType<JsonObject>(preparedNode["Body"]);
        var widgets = Assert.IsType<JsonArray>(bodyPart["Widgets"]);
        var heroWidgetNode = Assert.IsType<JsonObject>(widgets[0]);
        var heroPart = Assert.IsType<JsonObject>(heroWidgetNode["HeroPart"]);

        Assert.Equal("Center", heroPart["Alignment"]?.GetValue<string>());
        Assert.Equal("Welcome", heroPart["Title"]?.GetValue<string>());

        var itemsPart = Assert.IsType<JsonObject>(heroWidgetNode["Items"]);
        var nestedItems = Assert.IsType<JsonArray>(itemsPart["ContentItems"]);
        var promoBlockNode = Assert.IsType<JsonObject>(nestedItems[0]);
        var promoPart = Assert.IsType<JsonObject>(promoBlockNode["PromoPart"]);

        Assert.Equal("Primary", promoPart["Style"]?.GetValue<string>());
        Assert.Equal("Buy now", promoPart["Text"]?.GetValue<string>());
    }

    [Fact]
    public async Task PrepareAsync_WhenRecipeContainedPartSchemasAreUnavailable_ShouldFallbackToKnownContainerShapes()
    {
        // Arrange
        var page = CreateContentTypeDefinition(
            "Page",
            ("ContactMethods", "BagPart"));
        var emailAddress = CreateContentTypeDefinition("EmailAddress");
        var contentManager = CreateContentManager(new Dictionary<string, ContentItem>(StringComparer.OrdinalIgnoreCase)
        {
            ["Page"] = new()
            {
                ContentType = "Page",
            },
            ["EmailAddress"] = new()
            {
                ContentType = "EmailAddress",
                Content =
                {
                    ["EmailInfoPart"] = new JsonObject
                    {
                        ["IsPrimary"] = true,
                    },
                },
            },
        });
        var service = new ContentItemPreparationService(
            contentManager.Object,
            CreateContentDefinitionManager(page, emailAddress).Object,
            [],
            Options.Create(new DocumentJsonSerializerOptions()));
        var inputNode = JsonNode.Parse(
            """
            {
              "ContentType": "Page",
              "ContactMethods": {
                "ContentItems": [
                  {
                    "ContentType": "EmailAddress",
                    "EmailInfoPart": {
                      "Address": "test@example.com"
                    }
                  }
                ]
              }
            }
            """) as JsonObject;

        Assert.NotNull(inputNode);

        // Act
        var prepared = await service.PrepareAsync(
            page,
            inputNode,
            cancellationToken: TestContext.Current.CancellationToken);
        var preparedNode = JsonSerializer.SerializeToNode(
            prepared,
            new DocumentJsonSerializerOptions().SerializerOptions) as JsonObject;

        // Assert
        Assert.NotNull(preparedNode);

        contentManager.Verify(x => x.NewAsync("Page"), Times.Once);
        contentManager.Verify(x => x.NewAsync("EmailAddress"), Times.Once);

        var contactMethods = Assert.IsType<JsonObject>(preparedNode["ContactMethods"]);
        var contentItems = Assert.IsType<JsonArray>(contactMethods["ContentItems"]);
        var emailAddressNode = Assert.IsType<JsonObject>(contentItems[0]);
        var emailInfoPart = Assert.IsType<JsonObject>(emailAddressNode["EmailInfoPart"]);

        Assert.True(emailInfoPart["IsPrimary"]?.GetValue<bool>());
        Assert.Equal("test@example.com", emailInfoPart["Address"]?.GetValue<string>());
    }

    [Fact]
    public async Task PrepareAsync_WhenContainedSchemaDefinitionExists_ShouldPreferItOverFallbackContainerProperties()
    {
        // Arrange
        var page = CreateContentTypeDefinition(
            "Page",
            ("ContactMethods", "BagPart"));
        var schemaEmail = CreateContentTypeDefinition("SchemaEmail");
        var fallbackEmail = CreateContentTypeDefinition("FallbackEmail");
        var contentManager = CreateContentManager(new Dictionary<string, ContentItem>(StringComparer.OrdinalIgnoreCase)
        {
            ["Page"] = new()
            {
                ContentType = "Page",
            },
            ["SchemaEmail"] = new()
            {
                ContentType = "SchemaEmail",
                Content =
                {
                    ["EmailInfoPart"] = new JsonObject
                    {
                        ["Source"] = "Schema",
                    },
                },
            },
            ["FallbackEmail"] = new()
            {
                ContentType = "FallbackEmail",
                Content =
                {
                    ["EmailInfoPart"] = new JsonObject
                    {
                        ["Source"] = "Fallback",
                    },
                },
            },
        });
        var service = new ContentItemPreparationService(
            contentManager.Object,
            CreateContentDefinitionManager(page, schemaEmail, fallbackEmail).Object,
            [new TestContainedContentPartSchemaDefinition("BagPart", "NestedItems")],
            Options.Create(new DocumentJsonSerializerOptions()));
        var inputNode = JsonNode.Parse(
            """
            {
              "ContentType": "Page",
              "ContactMethods": {
                "NestedItems": [
                  {
                    "ContentType": "SchemaEmail",
                    "EmailInfoPart": {
                      "Address": "schema@example.com"
                    }
                  }
                ],
                "ContentItems": [
                  {
                    "ContentType": "FallbackEmail",
                    "EmailInfoPart": {
                      "Address": "fallback@example.com"
                    }
                  }
                ]
              }
            }
            """) as JsonObject;

        Assert.NotNull(inputNode);

        // Act
        var prepared = await service.PrepareAsync(
            page,
            inputNode,
            cancellationToken: TestContext.Current.CancellationToken);
        var preparedNode = JsonSerializer.SerializeToNode(
            prepared,
            new DocumentJsonSerializerOptions().SerializerOptions) as JsonObject;

        // Assert
        Assert.NotNull(preparedNode);

        contentManager.Verify(x => x.NewAsync("Page"), Times.Once);
        contentManager.Verify(x => x.NewAsync("SchemaEmail"), Times.Once);
        contentManager.Verify(x => x.NewAsync("FallbackEmail"), Times.Never);

        var contactMethods = Assert.IsType<JsonObject>(preparedNode["ContactMethods"]);
        var nestedItems = Assert.IsType<JsonArray>(contactMethods["NestedItems"]);
        var schemaEmailNode = Assert.IsType<JsonObject>(nestedItems[0]);
        var schemaEmailPart = Assert.IsType<JsonObject>(schemaEmailNode["EmailInfoPart"]);

        Assert.Equal("Schema", schemaEmailPart["Source"]?.GetValue<string>());
        Assert.Equal("schema@example.com", schemaEmailPart["Address"]?.GetValue<string>());

        var contentItems = Assert.IsType<JsonArray>(contactMethods["ContentItems"]);
        var fallbackEmailNode = Assert.IsType<JsonObject>(contentItems[0]);
        var fallbackEmailPart = Assert.IsType<JsonObject>(fallbackEmailNode["EmailInfoPart"]);

        Assert.Null(fallbackEmailPart["Source"]);
        Assert.Equal("fallback@example.com", fallbackEmailPart["Address"]?.GetValue<string>());
    }

    [Fact]
    public async Task PrepareAsync_WhenBagPartSchemaIsRegistered_ShouldUseItsContentItemsProperty()
    {
        // Arrange
        var page = CreateContentTypeDefinition(
            "Page",
            ("ContactMethods", "BagPart"));
        var bagEmail = CreateContentTypeDefinition("BagEmail");
        var widgetEmail = CreateContentTypeDefinition("WidgetEmail");
        var contentManager = CreateContentManager(new Dictionary<string, ContentItem>(StringComparer.OrdinalIgnoreCase)
        {
            ["Page"] = new()
            {
                ContentType = "Page",
            },
            ["BagEmail"] = new()
            {
                ContentType = "BagEmail",
                Content =
                {
                    ["EmailInfoPart"] = new JsonObject
                    {
                        ["Source"] = "BagPartSchema",
                    },
                },
            },
            ["WidgetEmail"] = new()
            {
                ContentType = "WidgetEmail",
                Content =
                {
                    ["EmailInfoPart"] = new JsonObject
                    {
                        ["Source"] = "WidgetsFallback",
                    },
                },
            },
        });
        var service = new ContentItemPreparationService(
            contentManager.Object,
            CreateContentDefinitionManager(page, bagEmail, widgetEmail).Object,
            [new BagPartSchema()],
            Options.Create(new DocumentJsonSerializerOptions()));
        var inputNode = JsonNode.Parse(
            """
            {
              "ContentType": "Page",
              "ContactMethods": {
                "ContentItems": [
                  {
                    "ContentType": "BagEmail",
                    "EmailInfoPart": {
                      "Address": "bag@example.com"
                    }
                  }
                ],
                "Widgets": [
                  {
                    "ContentType": "WidgetEmail",
                    "EmailInfoPart": {
                      "Address": "widget@example.com"
                    }
                  }
                ]
              }
            }
            """) as JsonObject;

        Assert.NotNull(inputNode);

        // Act
        var prepared = await service.PrepareAsync(
            page,
            inputNode,
            cancellationToken: TestContext.Current.CancellationToken);
        var preparedNode = JsonSerializer.SerializeToNode(
            prepared,
            new DocumentJsonSerializerOptions().SerializerOptions) as JsonObject;

        // Assert
        Assert.NotNull(preparedNode);

        contentManager.Verify(x => x.NewAsync("Page"), Times.Once);
        contentManager.Verify(x => x.NewAsync("BagEmail"), Times.Once);
        contentManager.Verify(x => x.NewAsync("WidgetEmail"), Times.Never);

        var contactMethods = Assert.IsType<JsonObject>(preparedNode["ContactMethods"]);
        var contentItems = Assert.IsType<JsonArray>(contactMethods["ContentItems"]);
        var bagEmailNode = Assert.IsType<JsonObject>(contentItems[0]);
        var bagEmailPart = Assert.IsType<JsonObject>(bagEmailNode["EmailInfoPart"]);

        Assert.Equal("BagPartSchema", bagEmailPart["Source"]?.GetValue<string>());
        Assert.Equal("bag@example.com", bagEmailPart["Address"]?.GetValue<string>());

        var widgets = Assert.IsType<JsonArray>(contactMethods["Widgets"]);
        var widgetEmailNode = Assert.IsType<JsonObject>(widgets[0]);
        var widgetEmailPart = Assert.IsType<JsonObject>(widgetEmailNode["EmailInfoPart"]);

        Assert.Null(widgetEmailPart["Source"]);
        Assert.Equal("widget@example.com", widgetEmailPart["Address"]?.GetValue<string>());
    }

    private static Mock<IContentManager> CreateContentManager(Dictionary<string, ContentItem> contentItemsByType)
    {
        var contentManager = new Mock<IContentManager>();

        contentManager
            .Setup(x => x.NewAsync(It.IsAny<string>()))
            .ReturnsAsync((string contentType) => Clone(contentItemsByType[contentType]));

        contentManager
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
            .ReturnsAsync((ContentItem)null);

        return contentManager;
    }

    private static Mock<IContentDefinitionManager> CreateContentDefinitionManager(params ContentTypeDefinition[] contentTypeDefinitions)
    {
        var contentDefinitionManager = new Mock<IContentDefinitionManager>();

        contentDefinitionManager
            .Setup(x => x.GetTypeDefinitionAsync(It.IsAny<string>()))
            .ReturnsAsync((string contentType) => contentTypeDefinitions.FirstOrDefault(definition =>
                string.Equals(definition.Name, contentType, StringComparison.OrdinalIgnoreCase)));

        return contentDefinitionManager;
    }

    private static ContentTypeDefinition CreateContentTypeDefinition(string name, params (string Name, string PartDefinitionName)[] parts)
    {
        var contentTypeDefinition = new ContentTypeDefinition(
            name,
            name,
            parts.Select(static part =>
                new ContentTypePartDefinition(
                    part.Name,
                    new ContentPartDefinition(part.PartDefinitionName, [], []),
                    []))
                .ToArray(),
            []);

        foreach (var part in contentTypeDefinition.Parts)
        {
            part.ContentTypeDefinition = contentTypeDefinition;
        }

        return contentTypeDefinition;
    }

    private static ContentItem Clone(ContentItem contentItem)
        => JsonSerializer.Deserialize<ContentItem>(
            JsonSerializer.Serialize(contentItem, new DocumentJsonSerializerOptions().SerializerOptions),
            new DocumentJsonSerializerOptions().SerializerOptions)!;

    private sealed class TestContainedContentPartSchemaDefinition(string name, string nestedItemsPropertyName)
        : IContentSchemaDefinition, IContainedContentPartSchemaDefinition
    {
        public ContentDefinitionSchemaType Type => ContentDefinitionSchemaType.Part;

        public string Name => name;

        public string NestedItemsPropertyName => nestedItemsPropertyName;

        public ValueTask<IReadOnlyList<string>> GetContainedContentTypesAsync(
            ContentPartSchemaContext context,
            IReadOnlyList<ContentTypeDefinition> knownContentTypeDefinitions,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<string>>([]);

        public ValueTask<JsonSchemaBuilder> GetSettingsSchemaAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new JsonSchemaBuilder());
    }
}
