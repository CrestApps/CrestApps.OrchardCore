using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.Autoroute;
using CrestApps.OrchardCore.AI.Agent.Menus;
using CrestApps.OrchardCore.AI.Agent.Seo;
using CrestApps.OrchardCore.AI.Agent.Sitemaps;
using CrestApps.OrchardCore.AI.Agent.Taxonomies;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.Settings;
using OrchardCore.Sitemaps.Cache;
using OrchardCore.Sitemaps.Services;

namespace CrestApps.OrchardCore.Tests.Agent;

/// <summary>
/// Every tool for addresses, the home page, menus, taxonomies, robots.txt and sitemaps acts for the person in the chat:
/// it refuses a caller who is not signed in and a caller who does not hold the permission the matching admin action asks
/// for, and it changes nothing when it refuses. Its JSON schema takes no argument it does not declare and describes each
/// argument.
/// </summary>
public sealed class OrchardSiteStructureToolCallerTests
{
    public static TheoryData<string> ToolNames =>
    [
        FindContentItemByHandleTool.TheName,
        SetHomepageTool.TheName,
        GetTaxonomyTermsTool.TheName,
        SaveTaxonomyTermTool.TheName,
        DeleteTaxonomyTermTool.TheName,
        GetMenuTreeTool.TheName,
        SaveMenuItemTool.TheName,
        DeleteMenuItemTool.TheName,
        GetRobotsTxtTool.TheName,
        ListSitemapsTool.TheName,
        SaveSitemapTool.TheName,
        DeleteSitemapTool.TheName,
        PurgeSitemapCacheTool.TheName,
    ];

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task InvokeAsync_WhenNobodyIsSignedIn_RefusesAndChangesNothing(string toolName)
    {
        // Arrange
        var doubles = new Doubles();
        var services = doubles.BuildServices(signedIn: false, authorized: true);
        var (tool, arguments) = CreateTool(toolName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        doubles.VerifyNothingChanged();
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task InvokeAsync_WhenTheCallerLacksThePermission_RefusesAndChangesNothing(string toolName)
    {
        // Arrange
        var doubles = new Doubles();
        var services = doubles.BuildServices(signedIn: true, authorized: false);
        var (tool, arguments) = CreateTool(toolName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.NotEmpty(services.GetRequiredService<FakeAuthorizationService>().Checks);
        doubles.VerifyNothingChanged();
    }

    [Theory]
    [InlineData(FindContentItemByHandleTool.TheName, "ViewContent")]
    [InlineData(SetHomepageTool.TheName, "SetHomepage")]
    [InlineData(GetTaxonomyTermsTool.TheName, "ViewContent")]
    [InlineData(SaveTaxonomyTermTool.TheName, "ManageTaxonomy")]
    [InlineData(DeleteTaxonomyTermTool.TheName, "ManageTaxonomy")]
    [InlineData(GetMenuTreeTool.TheName, "ViewContent")]
    [InlineData(SaveMenuItemTool.TheName, "ManageMenu")]
    [InlineData(DeleteMenuItemTool.TheName, "ManageMenu")]
    [InlineData(GetRobotsTxtTool.TheName, "ManageSeoSettings")]
    [InlineData(ListSitemapsTool.TheName, "ManageSitemaps")]
    [InlineData(SaveSitemapTool.TheName, "ManageSitemaps")]
    [InlineData(DeleteSitemapTool.TheName, "ManageSitemaps")]
    [InlineData(PurgeSitemapCacheTool.TheName, "ManageSitemaps")]
    public async Task InvokeAsync_ChecksThePermissionOfTheAdminAction(string toolName, string permission)
    {
        // Arrange
        var doubles = new Doubles();
        var services = doubles.BuildServices(signedIn: true, authorized: false);
        var (tool, arguments) = CreateTool(toolName);

        // Act
        await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        Assert.Equal(permission, services.GetRequiredService<FakeAuthorizationService>().Checks[0].Permission);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public void JsonSchema_ClosesTheArgumentsAndDescribesEachOne(string toolName)
    {
        // Arrange
        var (tool, _) = CreateTool(toolName);

        // Act
        var schema = tool.JsonSchema;

        // Assert
        Assert.Equal(toolName, tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Equal(JsonValueKind.False, schema.GetProperty("additionalProperties").ValueKind);
        Assert.Equal(JsonValueKind.Array, schema.GetProperty("required").ValueKind);

        var properties = schema.GetProperty("properties").EnumerateObject().ToList();

        Assert.All(properties, property => Assert.False(string.IsNullOrWhiteSpace(property.Value.GetProperty("description").GetString())));
        Assert.All(schema.GetProperty("required").EnumerateArray(), required => Assert.Contains(properties, property => property.Name == required.GetString()));
    }

    private static (AIFunction Tool, Dictionary<string, object> Arguments) CreateTool(string toolName)
    {
        return toolName switch
        {
            FindContentItemByHandleTool.TheName => (new FindContentItemByHandleTool(), new() { ["handle"] = "/about-us" }),
            SetHomepageTool.TheName => (new SetHomepageTool(), new() { ["contentItemId"] = "item-1" }),
            GetTaxonomyTermsTool.TheName => (new GetTaxonomyTermsTool(), new() { ["taxonomy"] = "item-1" }),
            SaveTaxonomyTermTool.TheName => (new SaveTaxonomyTermTool(), new() { ["taxonomyContentItemId"] = "item-1", ["title"] = "News" }),
            DeleteTaxonomyTermTool.TheName => (new DeleteTaxonomyTermTool(), new() { ["taxonomyContentItemId"] = "item-1", ["termId"] = "term-1" }),
            GetMenuTreeTool.TheName => (new GetMenuTreeTool(), new() { ["menu"] = "item-1" }),
            SaveMenuItemTool.TheName => (new SaveMenuItemTool(), new() { ["menuContentItemId"] = "item-1", ["contentType"] = "LinkMenuItem", ["title"] = "Careers" }),
            DeleteMenuItemTool.TheName => (new DeleteMenuItemTool(), new() { ["menuContentItemId"] = "item-1", ["menuItemId"] = "link-1" }),
            GetRobotsTxtTool.TheName => (new GetRobotsTxtTool(), []),
            ListSitemapsTool.TheName => (new ListSitemapsTool(), []),
            SaveSitemapTool.TheName => (new SaveSitemapTool(), new() { ["name"] = "Blog" }),
            DeleteSitemapTool.TheName => (new DeleteSitemapTool(), new() { ["sitemapId"] = "sitemap-1" }),
            PurgeSitemapCacheTool.TheName => (new PurgeSitemapCacheTool(), []),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, "The tool has no test arguments."),
        };
    }

    /// <summary>
    /// The services the tools read before their permission check. Every service that changes the site is strict or
    /// verified, so a refused call that still changed something fails the test.
    /// </summary>
    private sealed class Doubles
    {
        private readonly Mock<IContentManager> _contentManager = new();
        private readonly Mock<ISiteService> _siteService = new();
        private readonly Mock<ISitemapManager> _sitemapManager = new(MockBehavior.Strict);
        private readonly Mock<ISitemapCacheProvider> _sitemapCacheProvider = new(MockBehavior.Strict);

        public ServiceProvider BuildServices(bool signedIn, bool authorized)
        {
            var contentItem = new ContentItem
            {
                ContentItemId = "item-1",
                ContentItemVersionId = "version-1",
                ContentType = "Page",
                DisplayText = "About",
                Latest = true,
                Published = true,
            };

            var typeDefinition = new ContentTypeDefinitionBuilder()
                .WithName("Page")
                .WithPart("TitlePart")
                .Build();

            _contentManager.Setup(manager => manager.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>())).ReturnsAsync(contentItem);

            var contentDefinitionManager = new Mock<IContentDefinitionManager>();
            contentDefinitionManager.Setup(manager => manager.GetTypeDefinitionAsync(It.IsAny<string>())).ReturnsAsync(typeDefinition);

            var handleManager = new Mock<IContentHandleManager>();
            handleManager.Setup(manager => manager.GetContentItemIdAsync(It.IsAny<string>())).ReturnsAsync("item-1");

            var services = AgentToolTestContext.CreateServices(signedIn, (_, _) => authorized);

            services.AddSingleton(_contentManager.Object);
            services.AddSingleton(contentDefinitionManager.Object);
            services.AddSingleton(handleManager.Object);
            services.AddSingleton(_siteService.Object);
            services.AddSingleton(_sitemapManager.Object);
            services.AddSingleton(_sitemapCacheProvider.Object);

            return services.BuildServiceProvider();
        }

        public void VerifyNothingChanged()
        {
            _contentManager.Verify(manager => manager.UpdateAsync(It.IsAny<ContentItem>()), Times.Never);
            _contentManager.Verify(manager => manager.SaveDraftAsync(It.IsAny<ContentItem>()), Times.Never);
            _contentManager.Verify(manager => manager.PublishAsync(It.IsAny<ContentItem>()), Times.Never);
            _contentManager.Verify(manager => manager.NewAsync(It.IsAny<string>()), Times.Never);
            _contentManager.Verify(manager => manager.GetAsync(It.IsAny<string>(), VersionOptions.DraftRequired), Times.Never);
            _siteService.Verify(service => service.UpdateSiteSettingsAsync(It.IsAny<ISite>()), Times.Never);
            _sitemapManager.VerifyNoOtherCalls();
            _sitemapCacheProvider.VerifyNoOtherCalls();
        }
    }
}
