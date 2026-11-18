using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Seo;
using CrestApps.OrchardCore.Seo.AI;
using CrestApps.OrchardCore.Seo.Models;
using CrestApps.OrchardCore.Tests.Agent;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Seo;

/// <summary>
/// <c>getContentItemStructuredData</c> returns what the structured data engine builds for a published item's page, to a
/// caller who may view that item.
/// </summary>
public sealed class GetContentItemStructuredDataToolTests
{
    private readonly ContentItem _item = new()
    {
        ContentItemId = "spring-sale",
        ContentItemVersionId = "v1",
        ContentType = "Page",
        DisplayText = "Spring sale",
        Published = true,
        Latest = true,
    };

    private StructuredDataSettings _settings = new();
    private JsonObject _entity = new() { ["@type"] = "WebPage", ["name"] = "Spring sale" };

    [Fact]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn()
    {
        // Arrange
        var services = Build(signedIn: false);

        // Act
        var result = await InvokeAsync(services, "spring-sale");

        // Assert
        Assert.Equal(GetContentItemStructuredDataTool.NotAllowedMessage, result);
    }

    [Fact]
    public async Task Tool_RefusesACallerWhoMayNotViewTheItem()
    {
        // Arrange
        var services = Build(authorize: (permission, _) => permission.Name != "ViewContent");

        // Act
        var result = await InvokeAsync(services, "spring-sale");

        // Assert
        var check = Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks);

        Assert.Equal(GetContentItemStructuredDataTool.NotAllowedMessage, result);
        Assert.Equal("ViewContent", check.Permission);
        Assert.Same(_item, check.Resource);
    }

    [Fact]
    public async Task Tool_ReturnsTheJsonLdOfThePage()
    {
        // Arrange
        var services = Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(services, "spring-sale"));

        // Assert
        Assert.True(json.GetProperty("describesItem").GetBoolean());
        Assert.Equal("WebPage", json.GetProperty("jsonLd").GetProperty("@graph")[0].GetProperty("@type").GetString());
    }

    [Fact]
    public async Task Tool_SaysWhenPagesPublishNoStructuredData()
    {
        // Arrange
        _settings = new StructuredDataSettings { RenderOnPages = false };
        _entity = null;
        var services = Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(services, "spring-sale"));

        // Assert
        Assert.False(json.GetProperty("renderOnPages").GetBoolean());
        Assert.False(json.TryGetProperty("jsonLd", out _));
        Assert.Contains("turned off", json.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tool_WithAnUnknownItem_SaysSo()
    {
        // Arrange
        var services = Build();

        // Act
        var result = await InvokeAsync(services, "missing");

        // Assert
        Assert.Contains("No published content item has the id 'missing'", result, StringComparison.Ordinal);
    }

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync("spring-sale", It.IsAny<VersionOptions>())).ReturnsAsync(_item);
        services.AddSingleton(contentManager.Object);

        var site = new SiteSettings();
        site.Put(_settings);
        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site);
        services.AddSingleton(siteService.Object);

        var manager = new Mock<IStructuredDataManager>();
        manager.Setup(value => value.BuildEntityAsync(_item, StructuredDataPurpose.Page)).ReturnsAsync(() => _entity?.DeepClone().AsObject());
        manager.Setup(value => value.BuildPageDocumentAsync(_item, It.IsAny<IEnumerable<JsonObject>>(), It.IsAny<JsonObject>()))
            .ReturnsAsync(() => new JsonObject
            {
                ["@context"] = "https://schema.org",
                ["@graph"] = new JsonArray(_entity?.DeepClone()),
            });
        services.AddSingleton(manager.Object);

        return services.BuildServiceProvider();
    }

    private static Task<string> InvokeAsync(IServiceProvider services, string contentItemId)
        => AgentToolTestContext.InvokeAsync(new GetContentItemStructuredDataTool(), services, new() { ["contentItemId"] = contentItemId });
}
