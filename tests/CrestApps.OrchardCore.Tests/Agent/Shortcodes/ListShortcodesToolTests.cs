using CrestApps.OrchardCore.AI.Agent.Shortcodes;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Shortcodes;
using OrchardCore.Shortcodes.Services;

namespace CrestApps.OrchardCore.Tests.Agent.Shortcodes;

/// <summary>
/// The shortcode list tool returns the catalog the shortcode picker of the editors shows: every shortcode with its
/// hint, usage, default value and categories.
/// </summary>
public sealed class ListShortcodesToolTests
{
    [Fact]
    public async Task InvokeAsync_ReturnsEveryShortcodeOfTheCatalogInNameOrder()
    {
        // Arrange
        using var services = CreateServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListShortcodesTool(), services);

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var items = json.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(3, json.GetProperty("total").GetInt32());
        Assert.Equal(1, json.GetProperty("page").GetInt32());
        Assert.Equal(20, json.GetProperty("pageSize").GetInt32());
        Assert.Equal(new[] { "cta", "image", "locale" }, items.Select(item => item.GetProperty("name").GetString()));

        var image = items[1];

        Assert.Equal("Add an image from the media library.", image.GetProperty("hint").GetString());
        Assert.Equal("[image]foo.jpg[/image]", image.GetProperty("usage").GetString());
        Assert.Equal("[image] [/image]", image.GetProperty("defaultValue").GetString());
        Assert.Equal(new[] { "HTML Content", "Media" }, image.GetProperty("categories").EnumerateArray().Select(value => value.GetString()));

        // A shortcode without a default value is inserted as its own name in brackets.
        Assert.Equal("[cta]", items[0].GetProperty("defaultValue").GetString());

        // The shortcode picker checks no permission, so the tool checks none.
        Assert.Empty(services.GetRequiredService<FakeAuthorizationService>().Checks);
    }

    [Theory]
    [InlineData("IMAGE", "image")]
    [InlineData("language", "locale")]
    [InlineData("marketing", "cta")]
    public async Task InvokeAsync_WithASearch_KeepsTheShortcodesWhoseNameHintOrCategoryHasTheText(string search, string expected)
    {
        // Arrange
        using var services = CreateServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListShortcodesTool(), services, new()
        {
            ["search"] = search,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal(expected, Assert.Single(json.GetProperty("items").EnumerateArray()).GetProperty("name").GetString());
        Assert.Equal(1, json.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task InvokeAsync_WithASearchThatMatchesNothing_ReturnsAnEmptyPage()
    {
        // Arrange
        using var services = CreateServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListShortcodesTool(), services, new()
        {
            ["search"] = "gallery",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Empty(json.GetProperty("items").EnumerateArray());
        Assert.Equal(0, json.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task InvokeAsync_ReturnsThePageThatWasAskedFor()
    {
        // Arrange
        using var services = CreateServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListShortcodesTool(), services, new()
        {
            ["page"] = 2,
            ["pageSize"] = 2,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal("locale", Assert.Single(json.GetProperty("items").EnumerateArray()).GetProperty("name").GetString());
        Assert.Equal(3, json.GetProperty("total").GetInt32());
        Assert.Equal(2, json.GetProperty("page").GetInt32());
    }

    private static ServiceProvider CreateServices()
    {
        var descriptors = new[]
        {
            new ShortcodeDescriptor
            {
                Name = "locale",
                Hint = "Conditionally render content in the specified language",
                DefaultValue = "[locale {language_code}] [/locale]",
                Categories = ["Localization"],
            },
            new ShortcodeDescriptor
            {
                Name = "image",
                Hint = "Add an image from the media library.",
                Usage = "[image]foo.jpg[/image]",
                DefaultValue = "[image] [/image]",
                Categories = ["HTML Content", "Media"],
            },
            new ShortcodeDescriptor
            {
                Name = "cta",
                Hint = "A call to action button",
                Categories = ["Marketing"],
            },
        };

        var descriptorManager = new Mock<IShortcodeDescriptorManager>();

        descriptorManager.Setup(manager => manager.GetShortcodeDescriptors()).ReturnsAsync(descriptors);

        var services = AgentToolTestContext.CreateServices();

        services.AddSingleton(descriptorManager.Object);

        return services.BuildServiceProvider();
    }
}
