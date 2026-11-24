using CrestApps.OrchardCore.AI.Agent.Contents;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentLocalization;
using OrchardCore.ContentLocalization.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Routing;
using OrchardCore.Localization;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Agent.Contents;

/// <summary>
/// The tool that gets one content item by its ID or by an address, an alias or a handle, with its translations on
/// request. The outline of its nested items is tested with the nested content item tools.
/// </summary>
public sealed class GetContentItemToolTests
{
    [Fact]
    public async Task GetContentItem_ForAUrl_LooksUpItsPathWithoutTheTenantPrefix()
    {
        // Arrange
        var harness = new Harness();
        harness.Services.GetRequiredService<IHttpContextAccessor>().HttpContext.Request.PathBase = "/tenant1";

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetContentItemTool(), harness.Services, new()
        {
            ["handle"] = "https://example.com/tenant1/about-us/?utm=1#top",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("found").GetBoolean());
        Assert.Equal("slug:/about-us", json.GetProperty("matchedBy").GetString());
        Assert.Equal("page-en", json.GetProperty("contentItemId").GetString());
        Assert.Equal("Page", json.GetProperty("contentType").GetString());
        Assert.Equal("About", json.GetProperty("displayText").GetString());
        Assert.Equal("latest", json.GetProperty("version").GetString());
        Assert.True(json.GetProperty("published").GetBoolean());
        Assert.False(json.GetProperty("hasDraft").GetBoolean());
        Assert.False(json.TryGetProperty("localizations", out _));
        Assert.False(json.TryGetProperty("nested", out _));

        var check = Assert.Single(harness.Services.GetRequiredService<FakeAuthorizationService>().Checks);

        Assert.Equal("ViewContent", check.Permission);
        Assert.Same(harness.Page, check.Resource);
    }

    [Theory]
    [InlineData("about-us", "slug:about-us")]
    [InlineData("main-menu", "alias:main-menu")]
    [InlineData("alias:main-menu", "alias:main-menu")]
    [InlineData("page-en", "contentitemid:page-en")]
    public async Task GetContentItem_ForAWordOrAHandle_TriesTheHandlesInOrder(string handle, string matchedBy)
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetContentItemTool(), harness.Services, new()
        {
            ["handle"] = handle,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("found").GetBoolean());
        Assert.Equal(matchedBy, json.GetProperty("matchedBy").GetString());
        Assert.Equal("page-en", json.GetProperty("contentItemId").GetString());
    }

    [Fact]
    public async Task GetContentItem_ForTheRootPath_ReturnsTheHomePage()
    {
        // Arrange
        var harness = new Harness(homeContentItemId: "page-en");

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetContentItemTool(), harness.Services, new()
        {
            ["handle"] = "/",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("found").GetBoolean());
        Assert.Equal("homepage", json.GetProperty("matchedBy").GetString());
        Assert.Equal("page-en", json.GetProperty("contentItemId").GetString());
        harness.HandleManager.Verify(manager => manager.GetContentItemIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetContentItem_WhenTheHomePageIsNotAContentItem_ReturnsItsRoute()
    {
        // Arrange
        var harness = new Harness(homeContentItemId: null);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetContentItemTool(), harness.Services, new()
        {
            ["handle"] = "/",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.False(json.GetProperty("found").GetBoolean());
        Assert.Equal("OrchardCore.Contents", json.GetProperty("homeRoute").GetProperty("area").GetString());
    }

    [Theory]
    [InlineData("handle", "/nope")]
    [InlineData("contentItemId", "nope")]
    public async Task GetContentItem_WhenNothingMatches_SaysNotFoundAndChecksNothing(string argument, string value)
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetContentItemTool(), harness.Services, new()
        {
            [argument] = value,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.False(json.GetProperty("found").GetBoolean());
        Assert.Equal(value, json.GetProperty(argument).GetString());
        Assert.Empty(harness.Services.GetRequiredService<FakeAuthorizationService>().Checks);
    }

    [Theory]
    [InlineData(null, null, "Give contentItemId or handle")]
    [InlineData("page-en", "/about-us", "Give contentItemId or handle")]
    public async Task GetContentItem_WithoutExactlyOneOfIdAndHandle_SaysWhatToGive(string contentItemId, string handle, string expected)
    {
        // Arrange
        var harness = new Harness();
        var arguments = new Dictionary<string, object>();

        if (contentItemId is not null)
        {
            arguments["contentItemId"] = contentItemId;
        }

        if (handle is not null)
        {
            arguments["handle"] = handle;
        }

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetContentItemTool(), harness.Services, arguments);

        // Assert
        Assert.StartsWith(expected, result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetContentItem_ForAnUnknownIncludeOption_SaysWhichOnesExist()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetContentItemTool(), harness.Services, new()
        {
            ["contentItemId"] = "page-en",
            ["include"] = new[] { "versions" },
        });

        // Assert
        Assert.Equal("These are not include options: versions. Use localizations, nested or both.", result);
    }

    [Fact]
    public async Task GetContentItem_WithLocalizations_ReturnsOneEntryForEachSiteCulture()
    {
        // Arrange
        var harness = new Harness(localization: true);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetContentItemTool(), harness.Services, new()
        {
            ["contentItemId"] = "page-en",
            ["include"] = new[] { "localizations" },
        });

        // Assert
        var localizations = AgentToolTestContext.ReadJson(result).GetProperty("localizations");
        var items = localizations.GetProperty("items").EnumerateArray().ToList();

        Assert.True(localizations.GetProperty("available").GetBoolean());
        Assert.Equal("set-1", localizations.GetProperty("localizationSet").GetString());
        Assert.Equal("en", localizations.GetProperty("defaultCulture").GetString());
        Assert.Equal(2, items.Count);

        var existing = items.Single(item => item.GetProperty("culture").GetString() == "en");

        Assert.True(existing.GetProperty("exists").GetBoolean());
        Assert.Equal("page-en", existing.GetProperty("contentItemId").GetString());
        Assert.True(existing.GetProperty("isRequestedItem").GetBoolean());

        var missing = items.Single(item => item.GetProperty("culture").GetString() == "fr");

        Assert.False(missing.GetProperty("exists").GetBoolean());
        Assert.False(missing.TryGetProperty("contentItemId", out _));
    }

    [Fact]
    public async Task GetContentItem_WithLocalizationsWhenContentLocalizationIsOff_SaysTheyAreNotAvailable()
    {
        // Arrange
        var harness = new Harness(localization: false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetContentItemTool(), harness.Services, new()
        {
            ["contentItemId"] = "page-en",
            ["include"] = new[] { "localizations" },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var localizations = json.GetProperty("localizations");

        Assert.True(json.GetProperty("found").GetBoolean());
        Assert.False(localizations.GetProperty("available").GetBoolean());
        Assert.Contains("Content Localization feature is off", localizations.GetProperty("note").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetContentItem_ForThePublishedVersionOfAnItemNeverPublished_SaysSo()
    {
        // Arrange
        var harness = new Harness(published: false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetContentItemTool(), harness.Services, new()
        {
            ["contentItemId"] = "page-en",
            ["version"] = "published",
        });

        // Assert
        Assert.StartsWith("Content item 'page-en' has never been published", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetContentItem_WhenTheCallerMayNotViewTheItem_Refuses()
    {
        // Arrange
        var harness = new Harness(authorized: false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetContentItemTool(), harness.Services, new()
        {
            ["contentItemId"] = "page-en",
            ["include"] = new[] { "localizations", "nested" },
        });

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Equal("ViewContent", Assert.Single(harness.Services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
    }

    [Fact]
    public async Task GetContentItem_WhenNobodyIsSignedIn_RefusesBeforeReadingAnything()
    {
        // Arrange
        var harness = new Harness(signedIn: false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetContentItemTool(), harness.Services, new()
        {
            ["contentItemId"] = "page-en",
        });

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        harness.ContentManager.Verify(manager => manager.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()), Times.Never);
    }

    /// <summary>
    /// An English page at /about-us in a localization set, and a site whose cultures are English and French.
    /// </summary>
    private sealed class Harness
    {
        private const string ContentItemIdKey = "contentItemId";

        public Harness(
            string homeContentItemId = "home-1",
            bool localization = true,
            bool published = true,
            bool signedIn = true,
            bool authorized = true)
        {
            Page = new ContentItem
            {
                ContentItemId = "page-en",
                ContentItemVersionId = "version-1",
                ContentType = "Page",
                DisplayText = "About",
                Latest = true,
                Published = published,
            };

            Page.Weld(new LocalizationPart
            {
                LocalizationSet = "set-1",
                Culture = "en",
            });

            var homeRoute = new RouteValueDictionary
            {
                ["area"] = "OrchardCore.Contents",
                ["controller"] = "Item",
                ["action"] = "Display",
            };

            if (homeContentItemId is not null)
            {
                homeRoute[ContentItemIdKey] = homeContentItemId;
            }

            var site = new Mock<ISite>();
            site.SetupProperty(settings => settings.HomeRoute, homeRoute);

            var siteService = new Mock<ISiteService>();
            siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

            ContentManager.Setup(manager => manager.GetAsync("page-en", VersionOptions.Latest)).ReturnsAsync(Page);
            ContentManager.Setup(manager => manager.GetAsync("page-en", VersionOptions.Published)).ReturnsAsync(published ? Page : null);

            HandleManager.Setup(manager => manager.GetContentItemIdAsync("slug:/about-us")).ReturnsAsync("page-en");
            HandleManager.Setup(manager => manager.GetContentItemIdAsync("slug:about-us")).ReturnsAsync("page-en");
            HandleManager.Setup(manager => manager.GetContentItemIdAsync("alias:main-menu")).ReturnsAsync("page-en");
            HandleManager.Setup(manager => manager.GetContentItemIdAsync("contentitemid:page-en")).ReturnsAsync("page-en");

            var services = AgentToolTestContext.CreateServices(signedIn, (_, _) => authorized);

            services.AddSingleton(ContentManager.Object);
            services.AddSingleton(HandleManager.Object);
            services.AddSingleton(siteService.Object);
            services.AddSingleton(Options.Create(new AutorouteOptions
            {
                ContentItemIdKey = ContentItemIdKey,
                ContainedContentItemIdKey = "containedContentItemId",
                JsonPathKey = "jsonPath",
            }));

            if (localization)
            {
                var contentLocalizationManager = new Mock<IContentLocalizationManager>();
                contentLocalizationManager.Setup(manager => manager.GetItemsForSetAsync("set-1")).ReturnsAsync([Page]);

                var localizationService = new Mock<ILocalizationService>();
                localizationService.Setup(service => service.GetSupportedCulturesAsync()).ReturnsAsync(["en", "fr"]);
                localizationService.Setup(service => service.GetDefaultCultureAsync()).ReturnsAsync("en");

                services.AddSingleton(contentLocalizationManager.Object);
                services.AddSingleton(localizationService.Object);
            }

            Services = services.BuildServiceProvider();
        }

        public ContentItem Page { get; }

        public Mock<IContentManager> ContentManager { get; } = new();

        public Mock<IContentHandleManager> HandleManager { get; } = new();

        public ServiceProvider Services { get; }
    }
}
