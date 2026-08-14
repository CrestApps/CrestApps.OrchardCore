using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Contents;
using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.Tests.Agent.Contents;

public sealed class RecipeContentItemPayloadAssistanceServiceTests
{
    [Fact]
    public async Task ValidateAsync_WhenSchemaRejectsPayload_ShouldReturnSchemaFailure()
    {
        // Arrange
        var schemaService = new Mock<IContentItemSchemaService>();
        schemaService
            .Setup(x => x.GetSchemaAsync("Customer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    ("ContentType", new JsonSchemaBuilder().Const("Customer")),
                    ("EmailInfoPart", new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(("Email", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Object)
                            .Properties(("Text", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                            .Required("Text")
                            .AdditionalProperties(false)))
                        .AdditionalProperties(false)))
                .Required("ContentType")
                .AdditionalProperties(false));
        var provider = new RecipeContentItemPayloadAssistanceService(
            Mock.Of<IContentManager>(),
            schemaService.Object,
            Options.Create(new DocumentJsonSerializerOptions()));
        var definition = new ContentTypeDefinition("Customer", "Customer");
        var inputNode = JsonNode.Parse(
            """
            {
              "ContentType": "Customer",
              "EmailInfoPart": {
                "Email": "bgates@microsoft.com"
              }
            }
            """);
        // Act
        var result = await provider.ValidateAsync(
            definition,
            inputNode,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Empty(result.UnmappedPaths);
        Assert.Contains(result.Messages, message => message.Contains("JSON schema", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateAsync_WhenSchemaAllowsPayload_ShouldReturnSuccess()
    {
        // Arrange
        var contentManager = new Mock<IContentManager>();
        contentManager
            .Setup(x => x.NewAsync("Customer"))
            .ReturnsAsync(new ContentItem
            {
                ContentType = "Customer",
                DisplayText = string.Empty,
            });
        var schemaService = new Mock<IContentItemSchemaService>();
        schemaService
            .Setup(x => x.GetSchemaAsync("Customer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(("ContentType", new JsonSchemaBuilder().Const("Customer")))
                .Required("ContentType")
                .AdditionalProperties(true));
        var provider = new RecipeContentItemPayloadAssistanceService(
            contentManager.Object,
            schemaService.Object,
            Options.Create(new DocumentJsonSerializerOptions()));
        var definition = new ContentTypeDefinition("Customer", "Customer");
        var inputNode = JsonNode.Parse(
            """
            {
              "ContentType": "Customer"
            }
            """);
        // Act
        var result = await provider.ValidateAsync(
            definition,
            inputNode,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.UnmappedPaths);
        Assert.Empty(result.Messages);
    }

    [Fact]
    public async Task ValidateAsync_WhenSchemaAllowsUnknownWrapper_ShouldReturnSuccess()
    {
        // Arrange
        var contentManager = new Mock<IContentManager>();
        contentManager
            .Setup(x => x.NewAsync("Customer"))
            .ReturnsAsync(new ContentItem
            {
                ContentType = "Customer",
                DisplayText = string.Empty,
            });
        var schemaService = new Mock<IContentItemSchemaService>();
        schemaService
            .Setup(x => x.GetSchemaAsync("Customer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(("ContentType", new JsonSchemaBuilder().Const("Customer")))
                .Required("ContentType")
                .AdditionalProperties(true));
        var provider = new RecipeContentItemPayloadAssistanceService(
            contentManager.Object,
            schemaService.Object,
            Options.Create(new DocumentJsonSerializerOptions()));
        var definition = new ContentTypeDefinition("Customer", "Customer");
        var inputNode = JsonNode.Parse(
            """
            {
              "ContentType": "Customer",
              "DisplayText": "Bill Gates",
              "Customer": {
                "Email": "bgates@microsoft.com",
                "PhoneNumber": "8004445555"
              }
            }
            """);

        // Act
        var result = await provider.ValidateAsync(
            definition,
            inputNode,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.UnmappedPaths);
        Assert.Empty(result.Messages);
        Assert.Null(result.Guidance);
    }

    [Fact]
    public async Task GetGuidanceAsync_ShouldReturnRecipeSchemaGuidanceWithoutContentTypeDefinitionBlock()
    {
        // Arrange
        var contentManager = new Mock<IContentManager>();
        contentManager
            .Setup(x => x.NewAsync("Customer"))
            .ReturnsAsync(new ContentItem
            {
                ContentType = "Customer",
            });

        var schemaService = new Mock<IContentItemSchemaService>();
        schemaService
            .Setup(x => x.GetSchemaAsync("Customer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(("ContentType", new JsonSchemaBuilder().Const("Customer")))
                .Required("ContentType"));

        var provider = new RecipeContentItemPayloadAssistanceService(
            contentManager.Object,
            schemaService.Object,
            Options.Create(new DocumentJsonSerializerOptions()));
        var definition = new ContentTypeDefinition("Customer", "Customer");

        // Act
        var result = await provider.GetGuidanceAsync(definition, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Valid content item JSON schema for retry payloads", result, StringComparison.Ordinal);
        Assert.DoesNotContain("Sample content item JSON", result, StringComparison.Ordinal);
        Assert.DoesNotContain("Content type definition", result, StringComparison.Ordinal);
        Assert.Contains("\"const\":\"Customer\"", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetGuidanceAsync_ShouldPreserveSchemaAdditionalPropertiesSetting()
    {
        // Arrange
        var contentManager = new Mock<IContentManager>();
        contentManager
            .Setup(x => x.NewAsync("Customer"))
            .ReturnsAsync(new ContentItem
            {
                ContentType = "Customer",
            });

        var schemaService = new Mock<IContentItemSchemaService>();
        schemaService
            .Setup(x => x.GetSchemaAsync("Customer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(("ContentType", new JsonSchemaBuilder().Const("Customer")))
                .Required("ContentType")
                .AdditionalProperties(true));

        var provider = new RecipeContentItemPayloadAssistanceService(
            contentManager.Object,
            schemaService.Object,
            Options.Create(new DocumentJsonSerializerOptions()));
        var definition = new ContentTypeDefinition("Customer", "Customer");

        // Act
        var result = await provider.GetGuidanceAsync(definition, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("\"additionalProperties\":true", result, StringComparison.Ordinal);
    }
}
