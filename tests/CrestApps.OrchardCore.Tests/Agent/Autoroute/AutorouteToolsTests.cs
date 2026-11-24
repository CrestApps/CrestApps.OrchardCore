using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.Autoroute;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Autoroute.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Routing;
using OrchardCore.Json;
using OrchardCore.Settings;
using YSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Agent.Autoroute;

/// <summary>
/// The tool that makes a published content item the home page the way the content editor does. Finding the content item
/// behind an address is tested with getContentItem.
/// </summary>
public sealed class AutorouteToolsTests
{
    [Fact]
    public async Task SetHomepage_SetsTheFlagOfTheAutoroutePartAndPublishesTheContentItem()
    {
        // Arrange
        var harness = new Harness();
        var flagWhenUpdated = false;

        harness.ContentManager
            .Setup(manager => manager.UpdateAsync(harness.Page))
            .Callback<ContentItem>(contentItem => flagWhenUpdated = contentItem.TryGet<AutoroutePart>(out var part) && part.SetHomepage);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetHomepageTool(), harness.Services, new()
        {
            ["contentItemId"] = "page-1",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("changed").GetBoolean());
        Assert.Equal("page-1", json.GetProperty("contentItemId").GetString());
        Assert.Equal("about-us", json.GetProperty("path").GetString());
        Assert.Equal("home-1", json.GetProperty("previousContentItemId").GetString());
        Assert.True(flagWhenUpdated);

        // The steps of the content editor: a draft with the flag, then publish.
        harness.ContentManager.Verify(manager => manager.GetAsync("page-1", VersionOptions.DraftRequired), Times.Once);
        harness.ContentManager.Verify(manager => manager.UpdateAsync(harness.Page), Times.Once);
        harness.ContentManager.Verify(manager => manager.PublishAsync(harness.Page), Times.Once);

        // The tool does not write the home route itself; the Autoroute feature does when the item is published.
        harness.SiteService.Verify(service => service.UpdateSiteSettingsAsync(It.IsAny<ISite>()), Times.Never);

        var checks = harness.Services.GetRequiredService<FakeAuthorizationService>().Checks;

        Assert.Equal(new[] { "SetHomepage", "PublishContent" }, checks.Select(check => check.Permission));
        Assert.Null(checks[0].Resource);
        Assert.Same(harness.Page, checks[1].Resource);
    }

    [Theory]
    [InlineData("""{"Published": false, "AutoroutePart": {"Path": "about-us"}}""", "is not published")]
    [InlineData("""{"Published": true}""", "has no Autoroute part")]
    [InlineData("""{"Published": true, "AutoroutePart": {"Path": " "}}""", "has no path")]
    [InlineData("""{"Published": true, "AutoroutePart": {"Path": "about-us", "Disabled": true}}""", "is disabled")]
    public async Task SetHomepage_WhenTheContentItemCannotBeTheHomePage_ChangesNothing(string page, string expected)
    {
        // Arrange
        var harness = new Harness(page: page);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetHomepageTool(), harness.Services, new()
        {
            ["contentItemId"] = "page-1",
        });

        // Assert
        Assert.StartsWith("Nothing was changed", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        harness.VerifyNothingSaved();
    }

    [Fact]
    public async Task SetHomepage_WhenTheContentItemDoesNotExist_SaysNotFound()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetHomepageTool(), harness.Services, new()
        {
            ["contentItemId"] = "nope",
        });

        // Assert
        Assert.Equal("No content item with the ID 'nope' was found.", result);
        harness.VerifyNothingSaved();
    }

    [Fact]
    public async Task SetHomepage_WhenTheContentItemIsAlreadyTheHomePage_ChangesNothing()
    {
        // Arrange
        var harness = new Harness(homeContentItemId: "page-1");

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetHomepageTool(), harness.Services, new()
        {
            ["contentItemId"] = "page-1",
        });

        // Assert
        Assert.False(AgentToolTestContext.ReadJson(result).GetProperty("changed").GetBoolean());
        harness.VerifyNothingSaved();
    }

    [Fact]
    public async Task SetHomepage_WhenTheContentItemIsNotValid_CancelsTheChange()
    {
        // Arrange
        var harness = new Harness();
        var failure = new ContentValidateResult();
        failure.Fail(new ValidationResult("Your permalink is already in use."));
        harness.ContentManager.Setup(manager => manager.ValidateAsync(harness.Page)).ReturnsAsync(failure);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetHomepageTool(), harness.Services, new()
        {
            ["contentItemId"] = "page-1",
        });

        // Assert
        Assert.StartsWith("Nothing was changed", result, StringComparison.Ordinal);
        Assert.Contains("Your permalink is already in use.", result, StringComparison.Ordinal);
        harness.Session.Verify(session => session.CancelAsync(), Times.Once);
        harness.ContentManager.Verify(manager => manager.PublishAsync(It.IsAny<ContentItem>()), Times.Never);
    }

    [Fact]
    public async Task SetHomepage_WhenTheHomeRouteDoesNotFollow_DoesNotSayItChanged()
    {
        // Arrange
        var harness = new Harness(publishSetsHomeRoute: false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SetHomepageTool(), harness.Services, new()
        {
            ["contentItemId"] = "page-1",
        });

        // Assert
        Assert.StartsWith("The home page was not changed", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// A published page at /about-us, and a site whose home page is another content item. Publishing the page with the
    /// flag of its Autoroute part set writes the home route, as the Autoroute feature does.
    /// </summary>
    private sealed class Harness
    {
        private const string ContentItemIdKey = "contentItemId";

        private static readonly JsonSerializerOptions _serializerOptions = new DocumentJsonSerializerOptions().SerializerOptions;

        public Harness(
            string page = """{"Published": true, "AutoroutePart": {"Path": "about-us"}}""",
            string homeContentItemId = "home-1",
            bool publishSetsHomeRoute = true)
        {
            Page = JsonSerializer.Deserialize<ContentItem>(page, _serializerOptions);
            Page.ContentItemId = "page-1";
            Page.ContentItemVersionId = "version-1";
            Page.ContentType = "Page";
            Page.DisplayText = "About";
            Page.Latest = true;

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

            SiteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site.Object);
            SiteService.Setup(service => service.LoadSiteSettingsAsync()).ReturnsAsync(site.Object);

            ContentManager.Setup(manager => manager.GetAsync("page-1", It.IsAny<VersionOptions>())).ReturnsAsync(Page);
            ContentManager.Setup(manager => manager.ValidateAsync(It.IsAny<ContentItem>())).ReturnsAsync(new ContentValidateResult());
            ContentManager
                .Setup(manager => manager.PublishAsync(It.IsAny<ContentItem>()))
                .Callback<ContentItem>(contentItem =>
                {
                    if (publishSetsHomeRoute && contentItem.TryGet<AutoroutePart>(out var part) && part.SetHomepage)
                    {
                        homeRoute[ContentItemIdKey] = contentItem.ContentItemId;
                    }
                });

            HandleManager.Setup(manager => manager.GetContentItemIdAsync("slug:/about-us")).ReturnsAsync("page-1");
            HandleManager.Setup(manager => manager.GetContentItemIdAsync("slug:about-us")).ReturnsAsync("page-1");
            HandleManager.Setup(manager => manager.GetContentItemIdAsync("alias:main-menu")).ReturnsAsync("page-1");
            HandleManager.Setup(manager => manager.GetContentItemIdAsync("contentitemid:page-1")).ReturnsAsync("page-1");

            var services = AgentToolTestContext.CreateServices();

            services.AddSingleton(ContentManager.Object);
            services.AddSingleton(HandleManager.Object);
            services.AddSingleton(SiteService.Object);
            services.AddSingleton(Session.Object);
            services.AddSingleton(Options.Create(new AutorouteOptions
            {
                ContentItemIdKey = ContentItemIdKey,
                ContainedContentItemIdKey = "containedContentItemId",
                JsonPathKey = "jsonPath",
            }));

            Services = services.BuildServiceProvider();
        }

        public ContentItem Page { get; }

        public Mock<IContentManager> ContentManager { get; } = new();

        public Mock<IContentHandleManager> HandleManager { get; } = new();

        public Mock<ISiteService> SiteService { get; } = new();

        public Mock<YSession> Session { get; } = new();

        public ServiceProvider Services { get; }

        public void VerifyNothingSaved()
        {
            ContentManager.Verify(manager => manager.GetAsync(It.IsAny<string>(), VersionOptions.DraftRequired), Times.Never);
            ContentManager.Verify(manager => manager.UpdateAsync(It.IsAny<ContentItem>()), Times.Never);
            ContentManager.Verify(manager => manager.PublishAsync(It.IsAny<ContentItem>()), Times.Never);
            SiteService.Verify(service => service.UpdateSiteSettingsAsync(It.IsAny<ISite>()), Times.Never);
        }
    }
}
