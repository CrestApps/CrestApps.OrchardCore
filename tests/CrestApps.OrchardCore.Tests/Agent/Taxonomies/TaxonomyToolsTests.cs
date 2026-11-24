using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Contents;
using CrestApps.OrchardCore.AI.Agent.Taxonomies;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Json;
using YSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Agent.Taxonomies;

/// <summary>
/// The tools that read the terms of a taxonomy and add, update, move and delete one term. They change one term and
/// leave the rest of the taxonomy as it was, and they save the taxonomy the way the taxonomy editor does.
/// </summary>
public sealed class TaxonomyToolsTests
{
    [Fact]
    public async Task GetTaxonomyTerms_ReturnsTheTermsInOutlineOrderWithTheirPaths()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetTaxonomyTermsTool(), harness.Services, new()
        {
            ["taxonomy"] = "tax-1",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var items = json.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal("tax-1", json.GetProperty("taxonomyContentItemId").GetString());
        Assert.Equal("Category", json.GetProperty("termContentType").GetString());
        Assert.Equal(5, json.GetProperty("total").GetInt32());
        Assert.Equal(new[] { "news-1", "press-1", "events-1", "resources-1", "webinars-1" }, items.Select(item => item.GetProperty("termId").GetString()));
        Assert.Equal(
            new[] { "Company news", "Company news / Press", "Events", "Resources", "Resources / Webinars" },
            items.Select(item => item.GetProperty("path").GetString()));

        var press = items[1];

        Assert.Equal("Press", press.GetProperty("title").GetString());
        Assert.Equal("news-1", press.GetProperty("parentTermId").GetString());
        Assert.Equal(2, press.GetProperty("depth").GetInt32());
        Assert.Equal(0, press.GetProperty("position").GetInt32());
        Assert.False(items[0].TryGetProperty("parentTermId", out _));

        var check = Assert.Single(harness.Services.GetRequiredService<FakeAuthorizationService>().Checks);

        Assert.Equal("ViewContent", check.Permission);
        Assert.Same(harness.Taxonomy, check.Resource);
    }

    [Fact]
    public async Task GetTaxonomyTerms_WithoutATaxonomy_ListsTheTaxonomies()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetTaxonomyTermsTool(), harness.Services);

        // Assert
        var taxonomy = Assert.Single(AgentToolTestContext.ReadJson(result).GetProperty("taxonomies").EnumerateArray());

        Assert.Equal("tax-1", taxonomy.GetProperty("taxonomyContentItemId").GetString());
        Assert.Equal("Categories", taxonomy.GetProperty("displayText").GetString());
        Assert.Equal("Category", taxonomy.GetProperty("termContentType").GetString());
    }

    [Fact]
    public async Task GetTaxonomyTerms_WhenTheContentItemIsNotATaxonomy_SaysSo()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetTaxonomyTermsTool(), harness.Services, new()
        {
            ["taxonomy"] = "page-9",
        });

        // Assert
        Assert.StartsWith("Content item 'page-9' is a 'Page', not a taxonomy.", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveTaxonomyTerm_ForANewTerm_AddsItUnderTheParentAsATermOfTheTaxonomy()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveTaxonomyTermTool(), harness.Services, new()
        {
            ["taxonomyContentItemId"] = "tax-1",
            ["title"] = "Sustainability",
            ["parentTermId"] = "news-1",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.Equal("added", json.GetProperty("action").GetString());
        Assert.Equal("new-1", json.GetProperty("termId").GetString());
        Assert.Equal("news-1", json.GetProperty("parentTermId").GetString());
        Assert.Equal(1, json.GetProperty("position").GetInt32());

        var children = harness.Terms[0]["Terms"].AsArray();

        Assert.Equal(new[] { "press-1", "new-1" }, children.Select(term => term["ContentItemId"].GetValue<string>()));

        var added = children[1];

        Assert.Equal("Category", added["ContentType"].GetValue<string>());
        Assert.Equal("Sustainability", added["DisplayText"].GetValue<string>());
        Assert.Equal("Sustainability", added["TitlePart"]["Title"].GetValue<string>());
        Assert.Equal("tax-1", added["TermPart"]["TaxonomyContentItemId"].GetValue<string>());

        // The taxonomy keeps what the tool did not touch.
        Assert.Equal(new[] { "news-1", "events-1", "resources-1" }, harness.Terms.Select(term => term["ContentItemId"].GetValue<string>()));
        Assert.Equal("Category", ((JsonObject)harness.Taxonomy.Content)["TaxonomyPart"]["TermContentType"].GetValue<string>());
        Assert.Equal("Categories", ((JsonObject)harness.Taxonomy.Content)["TitlePart"]["Title"].GetValue<string>());

        // The Taxonomy content type can have drafts, so the change goes to a draft, as in the taxonomy editor.
        harness.ContentManager.Verify(manager => manager.GetAsync("tax-1", VersionOptions.DraftRequired), Times.Once);
        harness.ContentManager.Verify(manager => manager.SaveDraftAsync(harness.Taxonomy), Times.Once);
        harness.ContentManager.Verify(manager => manager.PublishAsync(It.IsAny<ContentItem>()), Times.Never);

        var check = Assert.Single(harness.Services.GetRequiredService<FakeAuthorizationService>().Checks);

        Assert.Equal("ManageTaxonomy", check.Permission);
        Assert.Null(check.Resource);
    }

    [Fact]
    public async Task SaveTaxonomyTerm_WithContent_MergesTheFieldsAndRenamesTheTerm()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveTaxonomyTermTool(), harness.Services, new()
        {
            ["taxonomyContentItemId"] = "tax-1",
            ["termId"] = "events-1",
            ["title"] = "Events and fairs",
            ["publish"] = true,
            ["content"] = new JsonObject
            {
                ["Category"] = new JsonObject
                {
                    ["Icon"] = new JsonObject
                    {
                        ["Text"] = "fa-calendar",
                    },
                },
            },
        });

        // Assert
        Assert.Equal("updated", AgentToolTestContext.ReadJson(result).GetProperty("action").GetString());

        var events = harness.Terms[1];

        Assert.Equal("events-1", events["ContentItemId"].GetValue<string>());
        Assert.Equal("Events and fairs", events["DisplayText"].GetValue<string>());
        Assert.Equal("Events and fairs", events["TitlePart"]["Title"].GetValue<string>());
        Assert.Equal("fa-calendar", events["Category"]["Icon"]["Text"].GetValue<string>());
        Assert.Equal("tax-1", events["TermPart"]["TaxonomyContentItemId"].GetValue<string>());
        harness.ContentManager.Verify(manager => manager.PublishAsync(harness.Taxonomy), Times.Once);

        var permissions = harness.Services.GetRequiredService<FakeAuthorizationService>().Checks.Select(check => check.Permission);

        Assert.Equal(new[] { "ManageTaxonomy", "PublishContent" }, permissions);
    }

    [Fact]
    public async Task SaveTaxonomyTerm_WithAnAfterId_MovesTheTermRightAfterThatSibling()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveTaxonomyTermTool(), harness.Services, new()
        {
            ["taxonomyContentItemId"] = "tax-1",
            ["termId"] = "news-1",
            ["afterId"] = "events-1",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal("moved", json.GetProperty("action").GetString());
        Assert.Equal(1, json.GetProperty("position").GetInt32());
        Assert.Equal(new[] { "events-1", "news-1", "resources-1" }, harness.Terms.Select(term => term["ContentItemId"].GetValue<string>()));
        Assert.Equal("press-1", Assert.Single(harness.Terms[1]["Terms"].AsArray())["ContentItemId"].GetValue<string>());
    }

    [Fact]
    public async Task SaveTaxonomyTerm_WithAParent_MovesTheTermUnderIt()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveTaxonomyTermTool(), harness.Services, new()
        {
            ["taxonomyContentItemId"] = "tax-1",
            ["termId"] = "webinars-1",
            ["parentTermId"] = "events-1",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal("moved", json.GetProperty("action").GetString());
        Assert.Equal("events-1", json.GetProperty("parentTermId").GetString());
        Assert.Equal("webinars-1", Assert.Single(harness.Terms[1]["Terms"].AsArray())["ContentItemId"].GetValue<string>());
        Assert.Empty(harness.Terms[2]["Terms"].AsArray());
    }

    [Theory]
    [InlineData("termId", "news-1", "parentTermId", "press-1", "cannot be moved under itself")]
    [InlineData("termId", "nope", "title", "X", "has no term with the ID 'nope'")]
    [InlineData("title", "X", "parentTermId", "nope", "has no term with the ID 'nope'")]
    [InlineData("parentTermId", "news-1", "afterId", "press-1", "give title to add a term")]
    [InlineData("termId", "resources-1", "afterId", "press-1", "'press-1' is not a term directly under the parent")]
    [InlineData("termId", "resources-1", "afterId", "resources-1", "cannot be placed after itself")]
    [InlineData("termId", "news-1", "publish", "false", "give title or content to change the term")]
    public async Task SaveTaxonomyTerm_WhenTheChangeIsNotAllowed_SavesNothing(string name, string value, string otherName, string otherValue, string expected)
    {
        // Arrange
        var harness = new Harness();
        object other = otherName switch
        {
            "publish" => bool.Parse(otherValue),
            _ => otherValue,
        };

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveTaxonomyTermTool(), harness.Services, new()
        {
            ["taxonomyContentItemId"] = "tax-1",
            [name] = value,
            [otherName] = other,
        });

        // Assert
        Assert.StartsWith("Nothing was saved", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        harness.VerifyNothingSaved();
    }

    [Fact]
    public async Task SaveTaxonomyTerm_WhenContentHoldsChildTerms_SavesNothing()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveTaxonomyTermTool(), harness.Services, new()
        {
            ["taxonomyContentItemId"] = "tax-1",
            ["termId"] = "news-1",
            ["content"] = new JsonObject
            {
                ["Terms"] = new JsonArray(),
            },
        });

        // Assert
        Assert.StartsWith("Nothing was saved: content cannot hold Terms.", result, StringComparison.Ordinal);
        Assert.Equal("press-1", harness.Terms[0]["Terms"][0]["ContentItemId"].GetValue<string>());
        harness.VerifyNothingSaved();
    }

    [Fact]
    public async Task SaveTaxonomyTerm_WhenTheTaxonomyTypeCannotHaveDrafts_ChangesTheLatestVersion()
    {
        // Arrange
        var harness = new Harness(draftable: false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveTaxonomyTermTool(), harness.Services, new()
        {
            ["taxonomyContentItemId"] = "tax-1",
            ["title"] = "Careers",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("published").GetBoolean());
        Assert.Equal("The taxonomy is published, so the change is live.", json.GetProperty("note").GetString());
        Assert.Equal(4, harness.Terms.Count);
        harness.ContentManager.Verify(manager => manager.GetAsync(It.IsAny<string>(), VersionOptions.DraftRequired), Times.Never);
        harness.ContentManager.Verify(manager => manager.UpdateAsync(harness.Taxonomy), Times.Once);
    }

    [Fact]
    public async Task DeleteTaxonomyTerm_RemovesTheTermAndTheTermsUnderIt()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteTaxonomyTermTool(), harness.Services, new()
        {
            ["taxonomyContentItemId"] = "tax-1",
            ["termId"] = "news-1",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("deleted").GetBoolean());
        Assert.Equal("Company news", json.GetProperty("title").GetString());
        Assert.Equal(1, json.GetProperty("deletedChildTerms").GetInt32());
        Assert.Contains("keep its ID", json.GetProperty("note").GetString(), StringComparison.Ordinal);
        Assert.Equal(new[] { "events-1", "resources-1" }, harness.Terms.Select(term => term["ContentItemId"].GetValue<string>()));
        harness.ContentManager.Verify(manager => manager.SaveDraftAsync(harness.Taxonomy), Times.Once);
    }

    [Fact]
    public async Task DeleteTaxonomyTerm_WhenTheTermIsNotInTheTaxonomy_DeletesNothing()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteTaxonomyTermTool(), harness.Services, new()
        {
            ["taxonomyContentItemId"] = "tax-1",
            ["termId"] = "nope",
        });

        // Assert
        Assert.StartsWith("Nothing was deleted", result, StringComparison.Ordinal);
        Assert.Equal(3, harness.Terms.Count);
        harness.VerifyNothingSaved();
    }

    /// <summary>
    /// A published taxonomy of categories: company news with press under it, events, and resources with webinars under it.
    /// </summary>
    private sealed class Harness
    {
        private static readonly JsonSerializerOptions _serializerOptions = new DocumentJsonSerializerOptions().SerializerOptions;

        public Harness(bool draftable = true)
        {
            var definitions = new[]
            {
                new ContentTypeDefinitionBuilder().WithName("Taxonomy").Draftable(draftable).Versionable(draftable)
                    .WithPart("TitlePart").WithPart("AliasPart").WithPart("TaxonomyPart").Build(),
                new ContentTypeDefinitionBuilder().WithName("Category").WithPart("TitlePart").WithPart("Category").Build(),
                new ContentTypeDefinitionBuilder().WithName("Page").WithPart("TitlePart").Build(),
            };

            Taxonomy = JsonSerializer.Deserialize<ContentItem>(
                """
                {
                  "ContentItemId": "tax-1",
                  "ContentItemVersionId": "version-1",
                  "ContentType": "Taxonomy",
                  "DisplayText": "Categories",
                  "Latest": true,
                  "Published": true,
                  "TitlePart": {
                    "Title": "Categories"
                  },
                  "AliasPart": {
                    "Alias": "categories"
                  },
                  "TaxonomyPart": {
                    "TermContentType": "Category",
                    "Terms": [
                      {
                        "ContentItemId": "news-1",
                        "ContentType": "Category",
                        "DisplayText": "Company news",
                        "TitlePart": {
                          "Title": "Company news"
                        },
                        "TermPart": {
                          "TaxonomyContentItemId": "tax-1"
                        },
                        "Terms": [
                          {
                            "ContentItemId": "press-1",
                            "ContentType": "Category",
                            "DisplayText": "Press",
                            "TitlePart": {
                              "Title": "Press"
                            }
                          }
                        ]
                      },
                      {
                        "ContentItemId": "events-1",
                        "ContentType": "Category",
                        "DisplayText": "Events",
                        "TitlePart": {
                          "Title": "Events"
                        }
                      },
                      {
                        "ContentItemId": "resources-1",
                        "ContentType": "Category",
                        "DisplayText": "Resources",
                        "TitlePart": {
                          "Title": "Resources"
                        },
                        "Terms": [
                          {
                            "ContentItemId": "webinars-1",
                            "ContentType": "Category",
                            "DisplayText": "Webinars",
                            "TitlePart": {
                              "Title": "Webinars"
                            }
                          }
                        ]
                      }
                    ]
                  }
                }
                """,
                _serializerOptions);

            var page = new ContentItem
            {
                ContentItemId = "page-9",
                ContentType = "Page",
                DisplayText = "Team",
                Latest = true,
                Published = true,
            };

            var newItems = 0;

            ContentManager.Setup(manager => manager.GetAsync("tax-1", It.IsAny<VersionOptions>())).ReturnsAsync(Taxonomy);
            ContentManager.Setup(manager => manager.GetAsync("page-9", It.IsAny<VersionOptions>())).ReturnsAsync(page);
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
            services.AddSingleton(Session.Object);
            services.AddSingleton<IContentItemPayloadAssistanceService>(new AcceptingPayloadAssistanceService());
            services.AddAdminContentList(ContentManager, null, Taxonomy);

            Services = services.BuildServiceProvider();
        }

        public ContentItem Taxonomy { get; }

        public Mock<IContentManager> ContentManager { get; } = new();

        public Mock<YSession> Session { get; } = new();

        public ServiceProvider Services { get; }

        public JsonArray Terms
            => ((JsonObject)Taxonomy.Content)["TaxonomyPart"][nameof(Terms)].AsArray();

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
