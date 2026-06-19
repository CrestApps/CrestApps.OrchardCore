using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Contents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Tests.Agent.Contents;

public sealed class CreateOrUpdateContentToolTests
{
    [Fact]
    public void Description_ShouldDirectSchemaLookupBeforeCreateOrUpdate()
    {
        var tool = new CreateOrUpdateContentTool();

        Assert.Contains("Before calling this function", tool.Description, StringComparison.Ordinal);
        Assert.Contains("getContentItemSchema", tool.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_WhenPayloadValidationFails_ShouldExplainNestedItemsBelongInParentPayload()
    {
        // Arrange
        var payloadAssistanceService = new TestContentItemPayloadAssistanceService(
            new ContentItemPayloadValidationResult(
                false,
                ["The provided content item JSON does not match the expected content item JSON schema."],
                [],
                """
                Valid content item JSON schema for retry payloads:
                {"type":"object"}
                """));
        var contentManager = new Mock<IContentManager>();
        contentManager
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
            .ReturnsAsync((ContentItem)null);
        var contentDefinitionManager = new Mock<IContentDefinitionManager>();
        contentDefinitionManager
            .Setup(x => x.GetTypeDefinitionAsync("LandingPage"))
            .ReturnsAsync(new ContentTypeDefinition("LandingPage", "LandingPage"));
        var services = new ServiceCollection()
            .AddSingleton<ILogger<CreateOrUpdateContentTool>>(NullLogger<CreateOrUpdateContentTool>.Instance)
            .AddSingleton<IContentItemPayloadAssistanceService>(payloadAssistanceService)
            .AddSingleton(contentManager.Object)
            .AddSingleton(contentDefinitionManager.Object)
            .BuildServiceProvider();
        var arguments = new AIFunctionArguments(new Dictionary<string, object>
        {
            ["contentItem"] =
                """
                {
                  "ContentType": "LandingPage",
                  "BagPart": {
                    "ContentItems": [
                      {
                        "ContentType": "HeroWidget"
                      }
                    ]
                  }
                }
                """,
            ["isDraft"] = false,
        })
        {
            Services = services,
        };
        var tool = new CreateOrUpdateContentTool();

        // Act
        var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert
        var text = Assert.IsType<string>(result);
        Assert.Contains("getContentItemSchema", text, StringComparison.Ordinal);
        Assert.Contains("Before calling this function", text, StringComparison.Ordinal);
        Assert.Contains("top-level content item", text, StringComparison.Ordinal);
        Assert.Contains("same parent payload", text, StringComparison.Ordinal);
        Assert.Contains("nested item", text, StringComparison.Ordinal);
    }

    private sealed class TestContentItemPayloadAssistanceService(
        ContentItemPayloadValidationResult validationResult) : IContentItemPayloadAssistanceService
    {
        public ValueTask<ContentItemPayloadValidationResult> ValidateAsync(
            ContentTypeDefinition contentDefinition,
            JsonNode inputNode,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(validationResult);

        public ValueTask<string> GetGuidanceAsync(
            ContentTypeDefinition contentDefinition,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(
                """
                Valid content item JSON schema for retry payloads:
                {"type":"object"}
                """);
    }
}
