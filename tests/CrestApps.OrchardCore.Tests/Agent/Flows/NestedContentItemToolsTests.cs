using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Contents;
using CrestApps.OrchardCore.AI.Agent.Flows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Flows.Models;
using OrchardCore.Json;
using YSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Agent.Flows;

/// <summary>
/// The tools that list (getContentItem with include nested), add, update, move and remove the content items nested in a page's Flow and Bag parts. They
/// change one nested item and leave the rest of the page as it was.
/// </summary>
public sealed class NestedContentItemToolsTests
{
    [Fact]
    public async Task GetContentItem_WithNested_ReturnsTheOutlineWithChildrenAfterTheirParent()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetContentItemTool(), harness.Services, new()
        {
            ["contentItemId"] = "page-1",
            ["include"] = new[] { "nested" },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result).GetProperty("nested");
        var items = json.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(3, json.GetProperty("total").GetInt32());
        Assert.Equal(new[] { "widget-1", "section-1", "column-1" }, items.Select(item => item.GetProperty("nestedContentItemId").GetString()));

        var column = items[2];

        Assert.Equal("Columns", column.GetProperty("part").GetString());
        Assert.Equal("bag", column.GetProperty("kind").GetString());
        Assert.Equal(2, column.GetProperty("depth").GetInt32());
        Assert.Equal(0, column.GetProperty("position").GetInt32());
        Assert.Equal("section-1", column.GetProperty("parentNestedContentItemId").GetString());
        Assert.Equal(1, items[1].GetProperty("position").GetInt32());
    }

    [Fact]
    public async Task SaveNestedContentItem_ForANewWidget_InsertsItAfterTheGivenSiblingAndLeavesTheRestOfThePage()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveNestedContentItemTool(), harness.Services, new()
        {
            ["contentItemId"] = "page-1",
            ["afterId"] = "widget-1",
            ["contentItem"] = new JsonObject
            {
                ["ContentType"] = "Quote",
                ["QuotePart"] = new JsonObject
                {
                    ["Text"] = "Well done.",
                },
            },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.Equal("added", json.GetProperty("action").GetString());
        Assert.Equal("new-1", json.GetProperty("nestedContentItemId").GetString());
        Assert.Equal(1, json.GetProperty("position").GetInt32());

        var widgets = harness.Widgets;

        Assert.Equal(new[] { "widget-1", "new-1", "section-1" }, widgets.Select(widget => widget["ContentItemId"].GetValue<string>()));
        Assert.Equal("Well done.", widgets[1]["QuotePart"]["Text"].GetValue<string>());
        Assert.NotNull(widgets[1][nameof(FlowMetadata)]);
        Assert.Equal("First paragraph", widgets[0]["ParagraphPart"]["Text"].GetValue<string>());
        Assert.Equal("column-1", widgets[2]["Columns"]["ContentItems"][0]["ContentItemId"].GetValue<string>());

        // The parent keeps what the tool did not touch.
        Assert.Equal("owner-1", harness.Page.Owner);
        Assert.Equal("About", harness.Page.DisplayText);
        Assert.Equal("About us", ((JsonObject)harness.Page.Content)["TitlePart"]["Title"].GetValue<string>());

        harness.ContentManager.Verify(manager => manager.UpdateAsync(harness.Page), Times.Once);
        harness.ContentManager.Verify(manager => manager.SaveDraftAsync(harness.Page), Times.Once);
        harness.ContentManager.Verify(manager => manager.PublishAsync(It.IsAny<ContentItem>()), Times.Never);
    }

    [Fact]
    public async Task SaveNestedContentItem_ForAnExistingNestedItem_MergesTheGivenFieldsAndKeepsItsId()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveNestedContentItemTool(), harness.Services, new()
        {
            ["contentItemId"] = "page-1",
            ["nestedContentItemId"] = "column-1",
            ["isDraft"] = false,
            ["contentItem"] = new JsonObject
            {
                ["ParagraphPart"] = new JsonObject
                {
                    ["Text"] = "Changed column",
                },
            },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal("updated", json.GetProperty("action").GetString());
        Assert.Equal("Columns", json.GetProperty("part").GetString());

        var column = harness.Widgets[1]["Columns"]["ContentItems"][0];

        Assert.Equal("column-1", column["ContentItemId"].GetValue<string>());
        Assert.Equal("Paragraph", column["ContentType"].GetValue<string>());
        Assert.Equal("Changed column", column["ParagraphPart"]["Text"].GetValue<string>());
        Assert.Equal("First paragraph", harness.Widgets[0]["ParagraphPart"]["Text"].GetValue<string>());
        harness.ContentManager.Verify(manager => manager.PublishAsync(harness.Page), Times.Once);
        harness.ContentManager.Verify(manager => manager.SaveDraftAsync(It.IsAny<ContentItem>()), Times.Never);

        var permissions = harness.Services.GetRequiredService<FakeAuthorizationService>().Checks.Select(check => check.Permission);

        Assert.Equal(new[] { "EditContent", "PublishContent" }, permissions);
    }

    [Fact]
    public async Task SaveNestedContentItem_WithAnEmptyAfterId_MovesTheNestedItemFirstInItsList()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveNestedContentItemTool(), harness.Services, new()
        {
            ["contentItemId"] = "page-1",
            ["nestedContentItemId"] = "section-1",
            ["afterId"] = string.Empty,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal("moved", json.GetProperty("action").GetString());
        Assert.Equal(0, json.GetProperty("position").GetInt32());
        Assert.Equal(new[] { "section-1", "widget-1" }, harness.Widgets.Select(widget => widget["ContentItemId"].GetValue<string>()));
        Assert.Equal("column-1", harness.Widgets[0]["Columns"]["ContentItems"][0]["ContentItemId"].GetValue<string>());
    }

    [Fact]
    public async Task SaveNestedContentItem_WithAnAfterId_MovesTheNestedItemRightAfterThatSibling()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveNestedContentItemTool(), harness.Services, new()
        {
            ["contentItemId"] = "page-1",
            ["nestedContentItemId"] = "widget-1",
            ["afterId"] = "section-1",
        });

        // Assert
        Assert.Equal(1, AgentToolTestContext.ReadJson(result).GetProperty("position").GetInt32());
        Assert.Equal(new[] { "section-1", "widget-1" }, harness.Widgets.Select(widget => widget["ContentItemId"].GetValue<string>()));
    }

    [Theory]
    [InlineData("column-1", "is not a nested item of the target list")]
    [InlineData("nope", "is not a nested item of the target list")]
    [InlineData("section-1", "cannot be placed after itself")]
    public async Task SaveNestedContentItem_WhenTheAfterIdIsNotASiblingInTheList_SavesNothing(string afterId, string expected)
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveNestedContentItemTool(), harness.Services, new()
        {
            ["contentItemId"] = "page-1",
            ["nestedContentItemId"] = "section-1",
            ["afterId"] = afterId,
        });

        // Assert
        Assert.StartsWith("Nothing was saved", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        Assert.Equal(new[] { "widget-1", "section-1" }, harness.Widgets.Select(widget => widget["ContentItemId"].GetValue<string>()));
        harness.VerifyNothingSaved();
    }

    [Theory]
    [InlineData("Article", null, "takes widgets only")]
    [InlineData("Quote", "section-1", "takes these content types only: Paragraph")]
    [InlineData("Nope", null, "no content type named 'Nope'")]
    public async Task SaveNestedContentItem_WhenTheContentTypeIsNotAllowedInTheList_SavesNothing(string contentType, string parentNestedContentItemId, string expected)
    {
        // Arrange
        var harness = new Harness();
        var arguments = new Dictionary<string, object>
        {
            ["contentItemId"] = "page-1",
            ["contentItem"] = new JsonObject
            {
                ["ContentType"] = contentType,
            },
        };

        if (parentNestedContentItemId is not null)
        {
            arguments["parentNestedContentItemId"] = parentNestedContentItemId;
        }

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveNestedContentItemTool(), harness.Services, arguments);

        // Assert
        Assert.StartsWith("Nothing was saved", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        harness.VerifyNothingSaved();
    }

    [Fact]
    public async Task SaveNestedContentItem_WhenTheParentIsNotValid_CancelsTheChange()
    {
        // Arrange
        var harness = new Harness();
        var failure = new ContentValidateResult();
        failure.Fail(new ValidationResult("The quote is too long."));
        harness.ContentManager.Setup(manager => manager.ValidateAsync(harness.Page)).ReturnsAsync(failure);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveNestedContentItemTool(), harness.Services, new()
        {
            ["contentItemId"] = "page-1",
            ["contentItem"] = new JsonObject
            {
                ["ContentType"] = "Quote",
            },
        });

        // Assert
        Assert.StartsWith("Nothing was saved", result, StringComparison.Ordinal);
        Assert.Contains("The quote is too long.", result, StringComparison.Ordinal);
        harness.Session.Verify(session => session.CancelAsync(), Times.Once);
        harness.ContentManager.Verify(manager => manager.SaveDraftAsync(It.IsAny<ContentItem>()), Times.Never);
    }

    [Fact]
    public async Task RemoveNestedContentItem_RemovesTheItemAndWhatIsNestedInIt()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RemoveNestedContentItemTool(), harness.Services, new()
        {
            ["contentItemId"] = "page-1",
            ["nestedContentItemId"] = "section-1",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("removed").GetBoolean());
        Assert.Equal(1, json.GetProperty("removedNestedItems").GetInt32());
        Assert.Equal("widget-1", Assert.Single(harness.Widgets)["ContentItemId"].GetValue<string>());
        harness.ContentManager.Verify(manager => manager.SaveDraftAsync(harness.Page), Times.Once);
    }

    [Fact]
    public async Task RemoveNestedContentItem_WhenTheNestedItemIsNotOnThePage_RemovesNothing()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RemoveNestedContentItemTool(), harness.Services, new()
        {
            ["contentItemId"] = "page-1",
            ["nestedContentItemId"] = "nope",
        });

        // Assert
        Assert.StartsWith("Nothing was removed", result, StringComparison.Ordinal);
        Assert.Equal(2, harness.Widgets.Count);
        harness.VerifyNothingSaved();
    }

    /// <summary>
    /// A page with a Flow part that holds a paragraph and a section; the section has a Bag part named Columns that
    /// takes paragraphs only and holds one.
    /// </summary>
    private sealed class Harness
    {
        private static readonly JsonSerializerOptions _serializerOptions = new DocumentJsonSerializerOptions().SerializerOptions;

        public Harness()
        {
            var definitions = new[]
            {
                new ContentTypeDefinitionBuilder().WithName("Page").WithPart("TitlePart").WithPart("FlowPart").Build(),
                new ContentTypeDefinitionBuilder().WithName("Paragraph").Stereotype("Widget").WithPart("ParagraphPart").Build(),
                new ContentTypeDefinitionBuilder().WithName("Quote").Stereotype("Widget").WithPart("QuotePart").Build(),
                new ContentTypeDefinitionBuilder().WithName("Article").WithPart("TitlePart").Build(),
                new ContentTypeDefinitionBuilder().WithName("Section").Stereotype("Widget")
                    .WithPart("Columns", "BagPart", part => part.WithSettings(new BagPartSettings
                    {
                        ContainedContentTypes = ["Paragraph"],
                    }))
                    .Build(),
            };

            Page = JsonSerializer.Deserialize<ContentItem>(
                """
                {
                  "ContentItemId": "page-1",
                  "ContentItemVersionId": "version-1",
                  "ContentType": "Page",
                  "DisplayText": "About",
                  "Latest": true,
                  "Published": true,
                  "Owner": "owner-1",
                  "Author": "jane",
                  "TitlePart": {
                    "Title": "About us"
                  },
                  "FlowPart": {
                    "Widgets": [
                      {
                        "ContentItemId": "widget-1",
                        "ContentType": "Paragraph",
                        "DisplayText": "Intro",
                        "ParagraphPart": {
                          "Text": "First paragraph"
                        }
                      },
                      {
                        "ContentItemId": "section-1",
                        "ContentType": "Section",
                        "DisplayText": "Two columns",
                        "Columns": {
                          "ContentItems": [
                            {
                              "ContentItemId": "column-1",
                              "ContentType": "Paragraph",
                              "ParagraphPart": {
                                "Text": "Left column"
                              }
                            }
                          ]
                        }
                      }
                    ]
                  }
                }
                """,
                _serializerOptions);

            var newItems = 0;

            ContentManager.Setup(manager => manager.GetAsync("page-1", It.IsAny<VersionOptions>())).ReturnsAsync(Page);
            ContentManager.Setup(manager => manager.ValidateAsync(It.IsAny<ContentItem>())).ReturnsAsync(new ContentValidateResult());
            ContentManager
                .Setup(manager => manager.NewAsync(It.IsAny<string>()))
                .ReturnsAsync((string contentType) => new ContentItem
                {
                    ContentItemId = $"new-{++newItems}",
                    ContentType = contentType,
                });

            var contentDefinitionManager = new Mock<IContentDefinitionManager>();
            contentDefinitionManager
                .Setup(manager => manager.GetTypeDefinitionAsync(It.IsAny<string>()))
                .ReturnsAsync((string name) => definitions.FirstOrDefault(definition => string.Equals(definition.Name, name, StringComparison.OrdinalIgnoreCase)));

            var services = AgentToolTestContext.CreateServices();

            services.AddSingleton(ContentManager.Object);
            services.AddSingleton(contentDefinitionManager.Object);
            services.AddSingleton<IContentItemPayloadAssistanceService>(new AcceptingPayloadAssistanceService());
            services.AddSingleton(Session.Object);
            services.AddSingleton(new ContentItemPreparationService(
                ContentManager.Object,
                contentDefinitionManager.Object,
                [],
                Options.Create(new DocumentJsonSerializerOptions())));

            Services = services.BuildServiceProvider();
        }

        public ContentItem Page { get; }

        public Mock<IContentManager> ContentManager { get; } = new();

        public Mock<YSession> Session { get; } = new();

        public ServiceProvider Services { get; }

        public JsonArray Widgets
            => ((JsonObject)Page.Content)[nameof(FlowPart)][nameof(FlowPart.Widgets)].AsArray();

        public void VerifyNothingSaved()
        {
            ContentManager.Verify(manager => manager.GetAsync(It.IsAny<string>(), VersionOptions.DraftRequired), Times.Never);
            ContentManager.Verify(manager => manager.UpdateAsync(It.IsAny<ContentItem>()), Times.Never);
            ContentManager.Verify(manager => manager.SaveDraftAsync(It.IsAny<ContentItem>()), Times.Never);
            ContentManager.Verify(manager => manager.PublishAsync(It.IsAny<ContentItem>()), Times.Never);
        }
    }

    private sealed class AcceptingPayloadAssistanceService : IContentItemPayloadAssistanceService
    {
        public ValueTask<ContentItemPayloadValidationResult> ValidateAsync(
            ContentTypeDefinition contentDefinition,
            JsonNode inputNode,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(ContentItemPayloadValidationResult.Success);

        public ValueTask<string> GetGuidanceAsync(ContentTypeDefinition contentDefinition, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(string.Empty);
    }
}
