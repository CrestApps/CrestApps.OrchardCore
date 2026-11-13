using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Chat.Core.Services;
using CrestApps.OrchardCore.AI.Chat.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat;

/// <summary>
/// The admin chat widget runs the chat as an AG-UI agent: it tells the AI which page the user is on, and shows the
/// tool calls and approvals with texts in the user's language. These pin the page context and the texts.
/// </summary>
public sealed class AIChatAGUITests
{
    [Fact]
    public void PageContext_KeepsNonEmptyEntriesUpToTheLimit_AndCutsLongValues()
    {
        var context = new AIChatPageContext(new DefaultHttpContext(), new RouteValueDictionary());

        context.Add("Empty", " ");
        context.Add(" ", "No description");
        context.Add("Long", new string('x', AIChatPageContext.MaxValueLength + 50));

        for (var i = 0; i < AIChatPageContext.MaxEntries + 5; i++)
        {
            context.Add("Entry " + i, "Value " + i);
        }

        Assert.Equal(AIChatPageContext.MaxEntries, context.Entries.Count);
        Assert.Equal("Long", context.Entries[0].Description);
        Assert.Equal(AIChatPageContext.MaxValueLength, context.Entries[0].Value.Length);
    }

    [Fact]
    public async Task ContentItemProvider_OnTheEditPage_DescribesTheItemBeingEdited()
    {
        var contentItem = new ContentItem
        {
            ContentItemId = "item-1",
            ContentType = "BlogPost",
            DisplayText = "Hello world",
            Published = false,
        };
        var provider = CreateContentItemProvider(contentItem);
        var context = CreateContext(new { area = "OrchardCore.Contents", controller = "Admin", action = "Edit", contentItemId = "item-1" });

        await provider.PopulateAsync(context, TestContext.Current.CancellationToken);

        var entry = Assert.Single(context.Entries);
        Assert.Equal("Content item being edited", entry.Description);
        Assert.Equal("\"Hello world\", a Blog Post (content type BlogPost, content item id item-1, draft)", entry.Value);
    }

    [Fact]
    public async Task ContentItemProvider_OnTheCreatePage_NamesTheTypeBeingCreated()
    {
        var provider = CreateContentItemProvider(contentItem: null);
        var context = CreateContext(new { area = "OrchardCore.Contents", controller = "Admin", action = "Create", id = "BlogPost" });

        await provider.PopulateAsync(context, TestContext.Current.CancellationToken);

        var entry = Assert.Single(context.Entries);
        Assert.Equal("Content item being created", entry.Description);
        Assert.Equal("A new Blog Post (content type BlogPost)", entry.Value);
    }

    [Fact]
    public async Task ContentItemProvider_OnOtherPagesOrAMissingItem_AddsNothing()
    {
        var provider = CreateContentItemProvider(contentItem: null);
        var otherPage = CreateContext(new { area = "OrchardCore.Users", controller = "Admin", action = "Edit", contentItemId = "item-1" });
        var missingItem = CreateContext(new { area = "OrchardCore.Contents", controller = "Admin", action = "Edit", contentItemId = "gone" });

        await provider.PopulateAsync(otherPage, TestContext.Current.CancellationToken);
        await provider.PopulateAsync(missingItem, TestContext.Current.CancellationToken);

        Assert.Empty(otherPage.Entries);
        Assert.Empty(missingItem.Entries);
    }

    [Fact]
    public void UIConfiguration_AddsTheAGUITextsAndAToolLabelForEveryTool_KeepingValuesTheViewSet()
    {
        var configuration = CreateUIConfiguration(("deleteContent", "Delete Content Item"));

        var json = JsonNode.Parse(configuration.SerializeChatConfig(new { signalRHubUrl = "/hub", aguiAllowLabel = "Yes, do it" }))!.AsObject();

        Assert.Equal("/hub", json["signalRHubUrl"]!.GetValue<string>());
        Assert.Equal("Yes, do it", json["aguiAllowLabel"]!.GetValue<string>());
        Assert.Equal("Deny", json["aguiDenyLabel"]!.GetValue<string>());
        Assert.Equal("Current page", json["aguiCurrentPageLabel"]!.GetValue<string>());

        var labels = json["aguiToolLabels"]!.AsObject();
        Assert.Equal("Delete Content Item", labels["deleteContent"]!.GetValue<string>());
        Assert.Equal("Opening a page", labels["page_navigate"]!.GetValue<string>());
        Assert.Equal("Reading the page's content", labels["page_read_content"]!.GetValue<string>());
    }

    [Fact]
    public void UIConfiguration_ForAWidget_AddsTheTextsToItsChatConfiguration()
    {
        var configuration = CreateUIConfiguration();

        var json = JsonNode.Parse(configuration.SerializeWidgetConfig(new { shellSelector = "#shell", chatConfig = new { signalRHubUrl = "/hub" } }))!.AsObject();

        Assert.Equal("#shell", json["shellSelector"]!.GetValue<string>());
        Assert.Null(json["aguiAllowLabel"]);
        Assert.Equal("Allow", json["chatConfig"]!["aguiAllowLabel"]!.GetValue<string>());
    }

    [Fact]
    public void AdminWidgetStartup_RegistersTheSiteContext_AndTheContentsStartupTheContentItemContext()
    {
        var services = new ServiceCollection();

        new CrestApps.OrchardCore.AI.Chat.AdminWidgetStartup().ConfigureServices(services);
        new CrestApps.OrchardCore.AI.Chat.AdminWidgetContentsStartup().ConfigureServices(services);

        Assert.Contains(services, d => d.ServiceType == typeof(IAIChatPageContextProvider) && d.ImplementationType == typeof(SitePageContextProvider));
        Assert.Contains(services, d => d.ServiceType == typeof(IAIChatPageContextProvider) && d.ImplementationType == typeof(ContentItemPageContextProvider));
    }

    private static AIChatPageContext CreateContext(object routeValues)
    {
        return new AIChatPageContext(new DefaultHttpContext(), new RouteValueDictionary(routeValues));
    }

    private static ContentItemPageContextProvider CreateContentItemProvider(ContentItem contentItem)
    {
        var contentManager = new Mock<IContentManager>();

        contentManager
            .Setup(manager => manager.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
            .ReturnsAsync((string id, VersionOptions _) => contentItem is not null && contentItem.ContentItemId == id ? contentItem : null);

        var definitions = new Mock<IContentDefinitionManager>();

        definitions
            .Setup(manager => manager.GetTypeDefinitionAsync("BlogPost"))
            .ReturnsAsync(new ContentTypeDefinition("BlogPost", "Blog Post"));

        return new ContentItemPageContextProvider(contentManager.Object, definitions.Object, new PassThroughLocalizer<ContentItemPageContextProvider>());
    }

    private static AIChatUIConfiguration CreateUIConfiguration(params (string Name, string Title)[] tools)
    {
        var services = new ServiceCollection().AddOptions();

        foreach (var (name, title) in tools)
        {
            var builder = services.AddCoreAITool<TestTool>(name);

            if (title is not null)
            {
                builder.WithTitle(title);
            }
        }

        using var provider = services.BuildServiceProvider();

        return new AIChatUIConfiguration(provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>(), new PassThroughLocalizer<AIChatUIConfiguration>());
    }

    private sealed class TestTool : AIFunction
    {
        protected override ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
            => ValueTask.FromResult<object>(null);
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
