using CrestApps.OrchardCore.AI.Agent.ContentLocalization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentLocalization;
using OrchardCore.ContentLocalization.Models;
using OrchardCore.ContentManagement;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Tests.Agent.ContentLocalization;

/// <summary>
/// The tool that creates a translation of a content item. Listing the translations is tested with getContentItem.
/// </summary>
public sealed class ContentLocalizationToolsTests
{
    [Fact]
    public async Task LocalizeContentItem_CreatesTheTranslationAndChecksBothPermissionsOfTheLocalizeAction()
    {
        // Arrange
        var english = CreateItem("item-en", "en", published: true);
        var french = CreateItem("item-fr", "fr", published: false);
        var contentLocalizationManager = new Mock<IContentLocalizationManager>();
        contentLocalizationManager.Setup(manager => manager.LocalizeAsync(english, "fr")).ReturnsAsync(french);
        var services = CreateServices(english, contentLocalizationManager.Object);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new LocalizeContentItemTool(), services, new()
        {
            ["contentItemId"] = "item-en",
            ["targetCulture"] = "FR",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("localized").GetBoolean());
        Assert.Equal("item-en", json.GetProperty("sourceContentItemId").GetString());
        Assert.Equal("item-fr", json.GetProperty("contentItemId").GetString());
        Assert.Equal("fr", json.GetProperty("culture").GetString());
        Assert.False(json.GetProperty("published").GetBoolean());

        var checks = services.GetRequiredService<FakeAuthorizationService>().Checks;

        Assert.Equal(new[] { "LocalizeContent", "EditContent" }, checks.Select(check => check.Permission));
        Assert.Same(english, checks[0].Resource);
    }

    [Theory]
    [InlineData("en", "already exists")]
    [InlineData("de", "is not a supported culture")]
    public async Task LocalizeContentItem_WhenTheCultureCannotBeCreated_SaysWhyAndCreatesNothing(string targetCulture, string expected)
    {
        // Arrange
        var english = CreateItem("item-en", "en", published: true);
        var contentLocalizationManager = new Mock<IContentLocalizationManager>();
        contentLocalizationManager.Setup(manager => manager.GetContentItemAsync("set-1", "en")).ReturnsAsync(english);
        var services = CreateServices(english, contentLocalizationManager.Object);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new LocalizeContentItemTool(), services, new()
        {
            ["contentItemId"] = "item-en",
            ["targetCulture"] = targetCulture,
        });

        // Assert
        Assert.StartsWith("Nothing was created", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        contentLocalizationManager.Verify(manager => manager.LocalizeAsync(It.IsAny<ContentItem>(), It.IsAny<string>()), Times.Never);
    }

    private static ContentItem CreateItem(string contentItemId, string culture, bool published)
    {
        var contentItem = new ContentItem
        {
            ContentItemId = contentItemId,
            ContentType = "Page",
            DisplayText = $"About ({culture})",
            Latest = true,
            Published = published,
        };

        contentItem.Weld(new LocalizationPart
        {
            LocalizationSet = "set-1",
            Culture = culture,
        });

        return contentItem;
    }

    private static ServiceProvider CreateServices(ContentItem contentItem, IContentLocalizationManager contentLocalizationManager)
    {
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync(contentItem.ContentItemId, VersionOptions.Latest)).ReturnsAsync(contentItem);

        var localizationService = new Mock<ILocalizationService>();
        localizationService.Setup(service => service.GetSupportedCulturesAsync()).ReturnsAsync(["en", "fr"]);
        localizationService.Setup(service => service.GetDefaultCultureAsync()).ReturnsAsync("en");

        var services = AgentToolTestContext.CreateServices();

        services.AddSingleton(contentManager.Object);
        services.AddSingleton(localizationService.Object);
        services.AddSingleton(contentLocalizationManager);

        return services.BuildServiceProvider();
    }
}
