using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Contents;
using CrestApps.OrchardCore.AI.Agent.ContentTypes;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Tests.Agent.Contents;

/// <summary>
/// Unpublishing and deleting work on the latest version, so an item that has a draft is found and handled; removing a
/// content type or part runs the DeleteContentDefinition step in the tool call's own scope, not as a recipe imported
/// in another scope, which waited for the chat request's write lock.
/// </summary>
public sealed class ContentDraftAndDefinitionToolTests
{
    private const string ItemId = "item-1";

    [Fact]
    public async Task UnpublishContentItem_WhenTheItemHasADraft_UnpublishesFromTheLatestVersion()
    {
        // Arrange
        var latest = new ContentItem { ContentItemId = ItemId, Latest = true, Published = false };
        var published = new ContentItem { ContentItemId = ItemId, Latest = false, Published = true };
        var contentManager = CreateContentManager(latest, published);
        using var services = CreateServices(contentManager);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new UnpublishContentTool(), services, new() { ["contentItemId"] = ItemId });

        // Assert
        Assert.Equal("Content item was successfully unpublished", result);
        contentManager.Verify(manager => manager.UnpublishAsync(latest), Times.Once);
    }

    [Fact]
    public async Task UnpublishContentItem_WhenTheItemIsNotPublished_ChangesNothing()
    {
        // Arrange
        var latest = new ContentItem { ContentItemId = ItemId, Latest = true, Published = false };
        var contentManager = CreateContentManager(latest, published: null);
        using var services = CreateServices(contentManager);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new UnpublishContentTool(), services, new() { ["contentItemId"] = ItemId });

        // Assert
        Assert.Equal($"Nothing was changed: the content item {ItemId} is not published.", result);
        contentManager.Verify(manager => manager.UnpublishAsync(It.IsAny<ContentItem>()), Times.Never);
    }

    [Fact]
    public async Task DeleteContentItem_WhenTheLatestVersionIsADraft_RemovesTheItem()
    {
        // Arrange
        var latest = new ContentItem { ContentItemId = ItemId, Latest = true, Published = false };
        var contentManager = CreateContentManager(latest, published: null);
        using var services = CreateServices(contentManager);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteContentTool(), services, new() { ["contentItemId"] = ItemId });

        // Assert
        Assert.Equal("Content item was successfully deleted", result);
        contentManager.Verify(manager => manager.RemoveAsync(latest), Times.Once);
    }

    [Fact]
    public async Task RemoveContentTypeDefinition_RunsTheDeleteStepInTheToolCallScope()
    {
        // Arrange
        var definitions = new Mock<IContentDefinitionManager>();
        definitions.Setup(manager => manager.GetTypeDefinitionAsync("Article")).ReturnsAsync(new ContentTypeDefinition("Article", "Article"));
        var handler = new RecordingStepHandler();
        using var services = CreateServices(definitions: definitions, handler: handler);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RemoveContentTypeDefinitionsTool(), services, new() { ["name"] = "Article" });

        // Assert
        var step = Assert.Single(handler.Steps);

        Assert.Equal("The content type Article was removed successfully", result);
        Assert.Equal("DeleteContentDefinition", step.Name);
        Assert.Equal("Article", Assert.Single(Assert.IsType<JsonArray>(step.Step["ContentTypes"])).GetValue<string>());
    }

    [Fact]
    public async Task RemoveContentPartDefinition_WhenTheStepReportsAnError_ReturnsIt()
    {
        // Arrange
        var definitions = new Mock<IContentDefinitionManager>();
        definitions.Setup(manager => manager.GetPartDefinitionAsync("ArticlePart")).ReturnsAsync(new ContentPartDefinition("ArticlePart"));
        var handler = new RecordingStepHandler { Error = "The part is in use." };
        using var services = CreateServices(definitions: definitions, handler: handler);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RemoveContentPartDefinitionsTool(), services, new() { ["name"] = "ArticlePart" });

        // Assert
        var step = Assert.Single(handler.Steps);

        Assert.Equal("Unable to remove the content part definition: The part is in use.", result);
        Assert.Equal("ArticlePart", Assert.Single(Assert.IsType<JsonArray>(step.Step["ContentParts"])).GetValue<string>());
    }

    private static Mock<IContentManager> CreateContentManager(ContentItem latest, ContentItem published)
    {
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync(ItemId, It.Is<VersionOptions>(options => options.IsLatest))).ReturnsAsync(latest);
        contentManager.Setup(manager => manager.GetAsync(ItemId, It.Is<VersionOptions>(options => options.IsPublished))).ReturnsAsync(published);

        return contentManager;
    }

    // No RecipeExecutionService is registered: the remove tools must not import a recipe.
    private static ServiceProvider CreateServices(Mock<IContentManager> contentManager = null, Mock<IContentDefinitionManager> definitions = null, RecordingStepHandler handler = null)
    {
        var services = AgentToolTestContext.CreateServices();

        services.AddLogging();

        if (contentManager is not null)
        {
            services.AddSingleton(contentManager.Object);
        }

        if (definitions is not null)
        {
            services.AddSingleton(definitions.Object);
        }

        if (handler is not null)
        {
            services.AddSingleton<IRecipeStepHandler>(handler);
        }

        return services.BuildServiceProvider();
    }

    private sealed class RecordingStepHandler : IRecipeStepHandler
    {
        public List<RecipeExecutionContext> Steps { get; } = [];

        public string Error { get; init; }

        public Task ExecuteAsync(RecipeExecutionContext context)
        {
            if (context.Name == "DeleteContentDefinition")
            {
                Steps.Add(context);

                if (Error is not null)
                {
                    context.Errors.Add(Error);
                }
            }

            return Task.CompletedTask;
        }
    }
}
