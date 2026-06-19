using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Contents;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.Tests.Agent.Contents;

public sealed class DefaultContentItemPayloadAssistanceServiceTests
{
    [Fact]
    public async Task ValidateAsync_WhenInputContainsDroppedValues_ShouldReturnUnmappedPaths()
    {
        // Arrange
        var contentManager = new Mock<IContentManager>();
        contentManager
            .Setup(x => x.NewAsync("Customer"))
            .ReturnsAsync(CreateBasicCustomerContentItem());
        var provider = new DefaultContentItemPayloadAssistanceService(
            contentManager.Object,
            Options.Create(new DocumentJsonSerializerOptions()));
        var definition = new ContentTypeDefinition("Customer", "Customer");
        var inputNode = JsonNode.Parse(
            """
            {
              "DisplayText": "Bill Gates",
              "Email": "bgates@microsoft.com",
              "PhoneNumber": "8004445555",
              "ContentType": "Customer"
            }
            """);

        // Act
        var result = await provider.ValidateAsync(
            definition,
            inputNode,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(["Email", "PhoneNumber"], result.UnmappedPaths);
        Assert.Contains(result.Messages, message => message.Contains("could not be mapped", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateAsync_WhenInputMatchesMappedShape_ShouldReturnSuccess()
    {
        // Arrange
        var contentManager = new Mock<IContentManager>();
        contentManager
            .Setup(x => x.NewAsync("Customer"))
            .ReturnsAsync(CreateContentItemWithEmailPart());
        var provider = new DefaultContentItemPayloadAssistanceService(
            contentManager.Object,
            Options.Create(new DocumentJsonSerializerOptions()));
        var definition = new ContentTypeDefinition("Customer", "Customer");
        var inputNode = JsonNode.Parse(
            """
            {
              "ContentType": "Customer",
              "DisplayText": "Bill Gates",
              "CustomerPart": {
                "Email": {
                  "Text": "bgates@microsoft.com"
                }
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
        Assert.Empty(result.Messages);
        Assert.Empty(result.UnmappedPaths);
    }

    [Fact]
    public async Task ValidateAsync_WhenNestedValueChangesShape_ShouldReturnNestedPath()
    {
        // Arrange
        var contentManager = new Mock<IContentManager>();
        contentManager
            .Setup(x => x.NewAsync("Customer"))
            .ReturnsAsync(CreateContentItemWithEmailPart());
        var provider = new DefaultContentItemPayloadAssistanceService(
            contentManager.Object,
            Options.Create(new DocumentJsonSerializerOptions()));
        var definition = new ContentTypeDefinition("Customer", "Customer");
        var inputNode = JsonNode.Parse(
            """
            {
              "CustomerPart": {
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
        Assert.Equal(["CustomerPart.Email"], result.UnmappedPaths);
    }

    [Fact]
    public async Task ValidateAsync_WhenUnknownContainerWrapsInvalidScalarValues_ShouldReturnLeafPaths()
    {
        // Arrange
        var contentManager = new Mock<IContentManager>();
        contentManager
            .Setup(x => x.NewAsync("Customer"))
            .ReturnsAsync(CreateContentItemWithContactMethods());
        var provider = new DefaultContentItemPayloadAssistanceService(
            contentManager.Object,
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
        Assert.False(result.IsValid);
        Assert.Equal(["Customer.Email", "Customer.PhoneNumber"], result.UnmappedPaths);
    }

    [Fact]
    public async Task ValidateAsync_WhenKnownCollectionPartIsEmptyInSample_ShouldNotRejectArrayItemsAsUnexpected()
    {
        // Arrange
        var contentManager = new Mock<IContentManager>();
        contentManager
            .Setup(x => x.NewAsync("Customer"))
            .ReturnsAsync(CreateContentItemWithContactMethods());
        var provider = new DefaultContentItemPayloadAssistanceService(
            contentManager.Object,
            Options.Create(new DocumentJsonSerializerOptions()));
        var definition = new ContentTypeDefinition("Customer", "Customer");
        var inputNode = JsonNode.Parse(
            """
            {
              "ContentType": "Customer",
              "ContactMethods": [
                {
                  "ContentType": "EmailAddress",
                  "EmailInfoPart": {
                    "Email": {
                      "Text": "bgates@microsoft.com"
                    }
                  }
                }
              ]
            }
            """);

        // Act
        var result = await provider.ValidateAsync(
            definition,
            inputNode,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result.UnmappedPaths);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenDisplayTextIsOmittedFromSampleSerialization_ShouldStillTreatItAsValid()
    {
        // Arrange
        var contentManager = new Mock<IContentManager>();
        contentManager
            .Setup(x => x.NewAsync("Customer"))
            .ReturnsAsync(new ContentItem
            {
                ContentType = "Customer",
            });
        var provider = new DefaultContentItemPayloadAssistanceService(
            contentManager.Object,
            Options.Create(new DocumentJsonSerializerOptions()));
        var definition = new ContentTypeDefinition("Customer", "Customer");
        var inputNode = JsonNode.Parse(
            """
            {
              "ContentType": "Customer",
              "DisplayText": "Bill Gates"
            }
            """);

        // Act
        var result = await provider.ValidateAsync(
            definition,
            inputNode,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result.UnmappedPaths);
        Assert.True(result.IsValid);
    }

    private static ContentItem CreateBasicCustomerContentItem() => new()
    {
        ContentType = "Customer",
        DisplayText = string.Empty,
    };

    private static ContentItem CreateContentItemWithEmailPart()
    {
        var contentItem = CreateBasicCustomerContentItem();
        contentItem.Content["CustomerPart"] = new JsonObject
        {
            ["Email"] = new JsonObject
            {
                ["Text"] = string.Empty,
            },
        };

        return contentItem;
    }

    private static ContentItem CreateContentItemWithContactMethods()
    {
        var contentItem = CreateBasicCustomerContentItem();
        contentItem.Content["ContactMethods"] = new JsonArray();

        return contentItem;
    }
}
