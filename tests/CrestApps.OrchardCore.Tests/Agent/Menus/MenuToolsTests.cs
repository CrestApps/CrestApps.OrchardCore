using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Menus;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Contents.ViewModels;
using OrchardCore.Infrastructure.Html;
using OrchardCore.Json;
using OrchardCore.Security.Permissions;
using YSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Agent.Menus;

/// <summary>
/// The tools that read a menu and add, update, move and delete one of its items. They change one item and leave the
/// rest of the menu as it was, and they save the menu the way the menu editor does.
/// </summary>
public sealed class MenuToolsTests
{
    [Fact]
    public async Task GetMenuTree_ByAlias_ReturnsTheItemsInOutlineOrder()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetMenuTreeTool(), harness.Services, new()
        {
            ["menu"] = "main-menu",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var items = json.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal("menu-1", json.GetProperty("menuContentItemId").GetString());
        Assert.Equal("main-menu", json.GetProperty("alias").GetString());
        Assert.Equal(4, json.GetProperty("total").GetInt32());
        Assert.Equal(new[] { "home-1", "about-1", "team-1", "blog-1" }, items.Select(item => item.GetProperty("menuItemId").GetString()));

        var team = items[2];

        Assert.Equal("ContentMenuItem", team.GetProperty("contentType").GetString());
        Assert.Equal("about-1", team.GetProperty("parentMenuItemId").GetString());
        Assert.Equal(2, team.GetProperty("depth").GetInt32());
        Assert.Equal(0, team.GetProperty("position").GetInt32());
        Assert.Equal("page-9", team.GetProperty("linkedContentItemId").GetString());
        Assert.True(team.GetProperty("checkContentPermissions").GetBoolean());

        var blog = items[3];

        Assert.Equal("/blog", blog.GetProperty("url").GetString());
        Assert.Equal("<b>Blog</b>", blog.GetProperty("html").GetString());
        Assert.Equal("ViewContent", Assert.Single(blog.GetProperty("permissionNames").EnumerateArray()).GetString());
        Assert.Equal(2, blog.GetProperty("position").GetInt32());

        var check = Assert.Single(harness.Services.GetRequiredService<FakeAuthorizationService>().Checks);

        Assert.Equal("ViewContent", check.Permission);
        Assert.Same(harness.Menu, check.Resource);
    }

    [Fact]
    public async Task GetMenuTree_ByTitle_FindsTheMenuInTheAdminContentList()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetMenuTreeTool(), harness.Services, new()
        {
            ["menu"] = "main MENU",
        });

        // Assert
        Assert.Equal("menu-1", AgentToolTestContext.ReadJson(result).GetProperty("menuContentItemId").GetString());
        Assert.Equal("Menu", harness.QueriedOptions.SelectedContentType);
        Assert.Equal(ContentsStatus.Latest, harness.QueriedOptions.ContentsStatus);
    }

    [Fact]
    public async Task GetMenuTree_WithoutAMenu_ListsTheMenusTheCallerMayView()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetMenuTreeTool(), harness.Services);

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var menu = Assert.Single(json.GetProperty("menus").EnumerateArray());

        Assert.Equal("menu-1", menu.GetProperty("menuContentItemId").GetString());
        Assert.Equal("Main Menu", menu.GetProperty("displayText").GetString());
        Assert.Equal("main-menu", menu.GetProperty("alias").GetString());
    }

    [Fact]
    public async Task GetMenuTree_WhenNoMenuHasTheName_SaysWhichMenusThereAre()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetMenuTreeTool(), harness.Services, new()
        {
            ["menu"] = "Footer",
        });

        // Assert
        Assert.StartsWith("No content item was found for 'Footer'", result, StringComparison.Ordinal);
        Assert.Contains("'Main Menu' (menu-1)", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveMenuItem_ForANewLink_InsertsItAtThePositionAndSavesADraftOfTheMenu()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveMenuItemTool(), harness.Services, new()
        {
            ["menuContentItemId"] = "menu-1",
            ["contentType"] = "linkmenuitem",
            ["title"] = "Careers",
            ["url"] = "/careers",
            ["position"] = 2,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.Equal("added", json.GetProperty("action").GetString());
        Assert.Equal("new-1", json.GetProperty("menuItemId").GetString());
        Assert.Equal("LinkMenuItem", json.GetProperty("contentType").GetString());
        Assert.Equal(2, json.GetProperty("position").GetInt32());

        var items = harness.MenuItems;

        Assert.Equal(new[] { "home-1", "about-1", "new-1", "blog-1" }, items.Select(item => item["ContentItemId"].GetValue<string>()));
        Assert.Equal("Careers", items[2]["DisplayText"].GetValue<string>());
        Assert.Equal("/careers", items[2]["LinkMenuItemPart"]["Url"].GetValue<string>());

        // The menu keeps what the tool did not touch.
        Assert.Equal("team-1", items[1]["MenuItemsListPart"]["MenuItems"][0]["ContentItemId"].GetValue<string>());
        Assert.Equal("Main Menu", ((JsonObject)harness.Menu.Content)["TitlePart"]["Title"].GetValue<string>());
        Assert.Equal("owner-1", harness.Menu.Owner);

        // The Menu content type can have drafts, so the change goes to a draft, as in the menu editor.
        harness.ContentManager.Verify(manager => manager.GetAsync("menu-1", VersionOptions.DraftRequired), Times.Once);
        harness.ContentManager.Verify(manager => manager.SaveDraftAsync(harness.Menu), Times.Once);
        harness.ContentManager.Verify(manager => manager.PublishAsync(It.IsAny<ContentItem>()), Times.Never);

        var check = Assert.Single(harness.Services.GetRequiredService<FakeAuthorizationService>().Checks);

        Assert.Equal("ManageMenu", check.Permission);
        Assert.Null(check.Resource);
    }

    [Fact]
    public async Task SaveMenuItem_ForAnExistingItem_ChangesTheGivenFieldsAndPublishesWhenAsked()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveMenuItemTool(), harness.Services, new()
        {
            ["menuContentItemId"] = "menu-1",
            ["menuItemId"] = "blog-1",
            ["title"] = "News",
            ["target"] = "_blank",
            ["permissionNames"] = Array.Empty<string>(),
            ["publish"] = true,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal("updated", json.GetProperty("action").GetString());
        Assert.Equal(2, json.GetProperty("position").GetInt32());

        var blog = harness.MenuItems[2];

        Assert.Equal("blog-1", blog["ContentItemId"].GetValue<string>());
        Assert.Equal("News", blog["DisplayText"].GetValue<string>());
        Assert.Equal("_blank", blog["HtmlMenuItemPart"]["Target"].GetValue<string>());
        Assert.Equal("/blog", blog["HtmlMenuItemPart"]["Url"].GetValue<string>());
        Assert.Equal("<b>Blog</b>", blog["HtmlMenuItemPart"]["Html"].GetValue<string>());
        Assert.Empty(blog["MenuItemPermissionPart"]["PermissionNames"].AsArray());

        harness.ContentManager.Verify(manager => manager.PublishAsync(harness.Menu), Times.Once);
        harness.ContentManager.Verify(manager => manager.SaveDraftAsync(It.IsAny<ContentItem>()), Times.Never);

        var permissions = harness.Services.GetRequiredService<FakeAuthorizationService>().Checks.Select(check => check.Permission);

        Assert.Equal(new[] { "ManageMenu", "PublishContent" }, permissions);
    }

    [Fact]
    public async Task SaveMenuItem_WithAParent_MovesTheItemAndItsChildrenUnderIt()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveMenuItemTool(), harness.Services, new()
        {
            ["menuContentItemId"] = "menu-1",
            ["menuItemId"] = "about-1",
            ["parentMenuItemId"] = "blog-1",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal("moved", json.GetProperty("action").GetString());
        Assert.Equal("blog-1", json.GetProperty("parentMenuItemId").GetString());
        Assert.Equal(new[] { "home-1", "blog-1" }, harness.MenuItems.Select(item => item["ContentItemId"].GetValue<string>()));

        var moved = harness.MenuItems[1]["MenuItemsListPart"]["MenuItems"][0];

        Assert.Equal("about-1", moved["ContentItemId"].GetValue<string>());
        Assert.Equal("team-1", moved["MenuItemsListPart"]["MenuItems"][0]["ContentItemId"].GetValue<string>());
    }

    [Fact]
    public async Task SaveMenuItem_WithTheMenuAsParent_MovesTheItemToTheTopLevel()
    {
        // Arrange
        var harness = new Harness();

        // Act
        await AgentToolTestContext.InvokeAsync(new SaveMenuItemTool(), harness.Services, new()
        {
            ["menuContentItemId"] = "menu-1",
            ["menuItemId"] = "team-1",
            ["parentMenuItemId"] = "menu-1",
            ["position"] = 0,
        });

        // Assert
        Assert.Equal(new[] { "team-1", "home-1", "about-1", "blog-1" }, harness.MenuItems.Select(item => item["ContentItemId"].GetValue<string>()));
        Assert.Empty(harness.MenuItems[2]["MenuItemsListPart"]["MenuItems"].AsArray());
    }

    [Theory]
    [InlineData("menuItemId", "about-1", "parentMenuItemId", "team-1", "cannot be moved under itself")]
    [InlineData("contentType", "LinkMenuItem", "url", "javascript:alert(1)", "is not a valid URL")]
    [InlineData("contentType", "LinkMenuItem", "html", "<b>x</b>", "has no html")]
    [InlineData("contentType", "Page", "url", "/x", "does not have the MenuItem stereotype")]
    [InlineData("contentType", "ContentMenuItem", "url", "/x", "needs linkedContentItemId")]
    [InlineData("contentType", "LinkMenuItem", "parentMenuItemId", "nope", "has no item with the ID 'nope'")]
    [InlineData("menuItemId", "nope", "url", "/x", "has no item with the ID 'nope'")]
    public async Task SaveMenuItem_WhenTheChangeIsNotAllowed_SavesNothing(string name, string value, string otherName, string otherValue, string expected)
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveMenuItemTool(), harness.Services, new()
        {
            ["menuContentItemId"] = "menu-1",
            ["title"] = "Careers",
            [name] = value,
            [otherName] = otherValue,
        });

        // Assert
        Assert.StartsWith("Nothing was saved", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        harness.VerifyNothingSaved();
    }

    [Fact]
    public async Task SaveMenuItem_WithPermissionNamesThatDoNotExist_SavesNothing()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveMenuItemTool(), harness.Services, new()
        {
            ["menuContentItemId"] = "menu-1",
            ["menuItemId"] = "home-1",
            ["permissionNames"] = new[] { "ViewContent", "Nope" },
        });

        // Assert
        Assert.Equal("Nothing was saved: these are not names of permissions: Nope.", result);
        harness.VerifyNothingSaved();
    }

    [Fact]
    public async Task SaveMenuItem_WhenTheMenuTypeCannotHaveDrafts_ChangesTheLatestVersion()
    {
        // Arrange
        var harness = new Harness(draftable: false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveMenuItemTool(), harness.Services, new()
        {
            ["menuContentItemId"] = "menu-1",
            ["menuItemId"] = "home-1",
            ["title"] = "Start",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("published").GetBoolean());
        Assert.Equal("The menu is published, so the change is live.", json.GetProperty("note").GetString());
        Assert.Equal("Start", harness.MenuItems[0]["DisplayText"].GetValue<string>());
        harness.ContentManager.Verify(manager => manager.GetAsync(It.IsAny<string>(), VersionOptions.DraftRequired), Times.Never);
        harness.ContentManager.Verify(manager => manager.UpdateAsync(harness.Menu), Times.Once);
    }

    [Fact]
    public async Task SaveMenuItem_WhenTheContentItemIsNotAMenu_SavesNothing()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveMenuItemTool(), harness.Services, new()
        {
            ["menuContentItemId"] = "page-9",
            ["contentType"] = "LinkMenuItem",
            ["title"] = "Careers",
        });

        // Assert
        Assert.Equal("Nothing was saved: content item 'page-9' is a 'Page', not a menu.", result);
        harness.VerifyNothingSaved();
    }

    [Fact]
    public async Task SaveMenuItem_ToPublishWithoutThePermissionToPublishTheMenu_Refuses()
    {
        // Arrange
        var harness = new Harness(authorize: (permission, _) => permission.Name != "PublishContent");

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveMenuItemTool(), harness.Services, new()
        {
            ["menuContentItemId"] = "menu-1",
            ["menuItemId"] = "home-1",
            ["title"] = "Start",
            ["publish"] = true,
        });

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        harness.VerifyNothingSaved();
    }

    [Fact]
    public async Task DeleteMenuItem_RemovesTheItemAndTheItemsUnderIt()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteMenuItemTool(), harness.Services, new()
        {
            ["menuContentItemId"] = "menu-1",
            ["menuItemId"] = "about-1",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("deleted").GetBoolean());
        Assert.Equal("About", json.GetProperty("title").GetString());
        Assert.Equal(1, json.GetProperty("deletedChildItems").GetInt32());
        Assert.Equal(new[] { "home-1", "blog-1" }, harness.MenuItems.Select(item => item["ContentItemId"].GetValue<string>()));
        harness.ContentManager.Verify(manager => manager.SaveDraftAsync(harness.Menu), Times.Once);
    }

    [Fact]
    public async Task DeleteMenuItem_WhenTheItemIsNotInTheMenu_DeletesNothing()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteMenuItemTool(), harness.Services, new()
        {
            ["menuContentItemId"] = "menu-1",
            ["menuItemId"] = "nope",
        });

        // Assert
        Assert.StartsWith("Nothing was deleted", result, StringComparison.Ordinal);
        Assert.Equal(3, harness.MenuItems.Count);
        harness.VerifyNothingSaved();
    }

    /// <summary>
    /// A published menu with a home link, an about link that has one content menu item under it, and an HTML item.
    /// </summary>
    private sealed class Harness
    {
        private static readonly JsonSerializerOptions _serializerOptions = new DocumentJsonSerializerOptions().SerializerOptions;

        public Harness(bool draftable = true, Func<Permission, object, bool> authorize = null)
        {
            var definitions = new[]
            {
                new ContentTypeDefinitionBuilder().WithName("Menu").Draftable(draftable).Versionable(draftable)
                    .WithPart("TitlePart").WithPart("AliasPart").WithPart("MenuPart").WithPart("MenuItemsListPart").Build(),
                new ContentTypeDefinitionBuilder().WithName("LinkMenuItem").Stereotype("MenuItem")
                    .WithPart("LinkMenuItemPart").WithPart("MenuItemPermissionPart").Build(),
                new ContentTypeDefinitionBuilder().WithName("ContentMenuItem").Stereotype("MenuItem")
                    .WithPart("ContentMenuItemPart").Build(),
                new ContentTypeDefinitionBuilder().WithName("HtmlMenuItem").Stereotype("MenuItem")
                    .WithPart("HtmlMenuItemPart").WithPart("MenuItemPermissionPart").Build(),
                new ContentTypeDefinitionBuilder().WithName("Page").WithPart("TitlePart").Build(),
            };

            Menu = JsonSerializer.Deserialize<ContentItem>(
                """
                {
                  "ContentItemId": "menu-1",
                  "ContentItemVersionId": "version-1",
                  "ContentType": "Menu",
                  "DisplayText": "Main Menu",
                  "Latest": true,
                  "Published": true,
                  "Owner": "owner-1",
                  "TitlePart": {
                    "Title": "Main Menu"
                  },
                  "AliasPart": {
                    "Alias": "main-menu"
                  },
                  "MenuPart": {},
                  "MenuItemsListPart": {
                    "MenuItems": [
                      {
                        "ContentItemId": "home-1",
                        "ContentType": "LinkMenuItem",
                        "DisplayText": "Home",
                        "LinkMenuItemPart": {
                          "Url": "~/"
                        }
                      },
                      {
                        "ContentItemId": "about-1",
                        "ContentType": "LinkMenuItem",
                        "DisplayText": "About",
                        "LinkMenuItemPart": {
                          "Url": "/about"
                        },
                        "MenuItemsListPart": {
                          "MenuItems": [
                            {
                              "ContentItemId": "team-1",
                              "ContentType": "ContentMenuItem",
                              "DisplayText": "Team",
                              "ContentMenuItemPart": {
                                "CheckContentPermissions": true,
                                "SelectedContentItem": {
                                  "ContentItemIds": [
                                    "page-9"
                                  ]
                                }
                              }
                            }
                          ]
                        }
                      },
                      {
                        "ContentItemId": "blog-1",
                        "ContentType": "HtmlMenuItem",
                        "DisplayText": "Blog",
                        "HtmlMenuItemPart": {
                          "Url": "/blog",
                          "Html": "<b>Blog</b>"
                        },
                        "MenuItemPermissionPart": {
                          "PermissionNames": [
                            "ViewContent"
                          ]
                        }
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

            ContentManager.Setup(manager => manager.GetAsync("menu-1", It.IsAny<VersionOptions>())).ReturnsAsync(Menu);
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

            var handleManager = new Mock<IContentHandleManager>();
            handleManager.Setup(manager => manager.GetContentItemIdAsync("alias:main-menu")).ReturnsAsync("menu-1");

            var permissionService = new Mock<IPermissionService>();
            permissionService
                .Setup(service => service.FindByNameAsync(It.IsAny<string>()))
                .Returns((string name) => ValueTask.FromResult(name == "ViewContent" ? new Permission(name) : null));

            var services = AgentToolTestContext.CreateServices(authorize: authorize);

            services.AddSingleton(ContentManager.Object);
            services.AddSingleton(contentDefinitionManager.Object);
            services.AddSingleton(handleManager.Object);
            services.AddSingleton(permissionService.Object);
            services.AddSingleton(Session.Object);
            services.AddSingleton<IHtmlSanitizerService>(new ScriptRemovingSanitizer());
            services.AddAdminContentList(ContentManager, options => QueriedOptions = options, Menu);

            Services = services.BuildServiceProvider();
        }

        public ContentItem Menu { get; }

        public ContentOptionsViewModel QueriedOptions { get; private set; }

        public Mock<IContentManager> ContentManager { get; } = new();

        public Mock<YSession> Session { get; } = new();

        public ServiceProvider Services { get; }

        public JsonArray MenuItems
            => ((JsonObject)Menu.Content)["MenuItemsListPart"][nameof(MenuItems)].AsArray();

        public void VerifyNothingSaved()
        {
            ContentManager.Verify(manager => manager.GetAsync(It.IsAny<string>(), VersionOptions.DraftRequired), Times.Never);
            ContentManager.Verify(manager => manager.UpdateAsync(It.IsAny<ContentItem>()), Times.Never);
            ContentManager.Verify(manager => manager.SaveDraftAsync(It.IsAny<ContentItem>()), Times.Never);
            ContentManager.Verify(manager => manager.PublishAsync(It.IsAny<ContentItem>()), Times.Never);
        }
    }

    /// <summary>
    /// Stands in for the HTML sanitizer: it removes a link whose address runs a script, as the real one does.
    /// </summary>
    private sealed class ScriptRemovingSanitizer : IHtmlSanitizerService
    {
        public string Sanitize(string html)
        {
            return html.Contains("javascript:", StringComparison.OrdinalIgnoreCase)
                ? "<a></a>"
                : html;
        }
    }
}
