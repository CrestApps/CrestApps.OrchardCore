using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.ContentTypes;
using Moq;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentTypes;

namespace CrestApps.OrchardCore.Tests.Agent.ContentTypes;

/// <summary>
/// The tools that attach a part to a content type, change how it is attached and remove it.
/// </summary>
public sealed class ContentTypePartToolsTests
{
    [Fact]
    public async Task SaveContentTypePart_WhenThePartIsNotAttached_AttachesItAndMergesItsSettings()
    {
        // Arrange
        var store = new ContentDefinitionTestStore();
        store.AddPart("TitlePart", part => part.Attachable());
        store.AddPart("AutoroutePart", part => part.Attachable());
        store.AddType("Article", type => type.WithPart("TitlePart"));
        var services = store.BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveContentTypePartTool(), services, new()
        {
            ["contentType"] = "article",
            ["part"] = "AutoroutePart",
            ["settings"] = new JsonObject
            {
                ["AutoroutePartSettings"] = new JsonObject
                {
                    ["AllowCustomPath"] = true,
                    ["Pattern"] = "blog/{{ ContentItem | display_text | slugify }}",
                },
            },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.True(json.GetProperty("attached").GetBoolean());
        store.Service.Verify(service => service.AddPartToTypeAsync("AutoroutePart", "Article"), Times.Once);

        var typePart = store.TypeNamed("Article").Parts.Single(part => part.Name == "AutoroutePart");

        Assert.True(typePart.Settings["AutoroutePartSettings"]["AllowCustomPath"].GetValue<bool>());
        Assert.Equal("blog/{{ ContentItem | display_text | slugify }}", typePart.Settings["AutoroutePartSettings"]["Pattern"].GetValue<string>());
        Assert.Contains(store.TypeNamed("Article").Parts, part => part.Name == "TitlePart");
    }

    [Fact]
    public async Task SaveContentTypePart_WhenOnlyTheEditorIsGiven_KeepsTheOtherValuesOfThePart()
    {
        // Arrange
        AlterTypePartContext altered = null;
        var store = new ContentDefinitionTestStore();
        var bagPart = store.AddPart("BagPart", part => part.Attachable().Reusable());
        store.AddType("Article", type => type.WithPart("Gallery", bagPart, part =>
        {
            part.WithDisplayName("Photo gallery");
            part.WithDescription("The photos of the article.");
            part.WithDisplayMode("Carousel");
        }));
        store.Service
            .Setup(service => service.AlterTypePartAsync(It.IsAny<AlterTypePartContext>()))
            .Callback((AlterTypePartContext context) => altered = context)
            .Returns(Task.CompletedTask);
        var services = store.BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveContentTypePartTool(), services, new()
        {
            ["contentType"] = "Article",
            ["part"] = "BagPart",
            ["name"] = "Gallery",
            ["editor"] = "Blocks",
        });

        // Assert
        Assert.True(AgentToolTestContext.ReadJson(result).GetProperty("saved").GetBoolean());
        Assert.NotNull(altered);
        Assert.Equal("Article", altered.TypeName);
        Assert.Equal("Gallery", altered.PartName);
        Assert.Equal("Blocks", altered.Editor);
        Assert.Equal("Photo gallery", altered.DisplayName);
        Assert.Equal("The photos of the article.", altered.Description);
        Assert.Equal("Carousel", altered.DisplayMode);
        store.Service.Verify(service => service.AddPartToTypeAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SaveContentTypePart_WithANameForAReusablePart_AttachesItAsANamedPart()
    {
        // Arrange
        var store = new ContentDefinitionTestStore();
        store.AddPart("BagPart", part => part.Attachable().Reusable());
        store.AddType("Article");
        var services = store.BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveContentTypePartTool(), services, new()
        {
            ["contentType"] = "Article",
            ["part"] = "BagPart",
            ["name"] = "Gallery",
            ["displayName"] = "Photo gallery",
        });

        // Assert
        Assert.True(AgentToolTestContext.ReadJson(result).GetProperty("attached").GetBoolean());
        store.Service.Verify(service => service.AddReusablePartToTypeAsync("Gallery", "Photo gallery", null, "BagPart", "Article"), Times.Once);
    }

    [Theory]
    [InlineData("Nope", "AutoroutePart", null, "no content type named 'Nope'")]
    [InlineData("Article", "NopePart", null, "no content part named 'NopePart'")]
    [InlineData("Article", "AutoroutePart", "Second", "is not reusable")]
    public async Task SaveContentTypePart_WhenTheArgumentsAreWrong_SaysWhyAndChangesNothing(string contentType, string part, string name, string expected)
    {
        // Arrange
        var store = new ContentDefinitionTestStore();
        store.AddPart("AutoroutePart", definition => definition.Attachable());
        store.AddType("Article");
        var services = store.BuildServices();
        var arguments = new Dictionary<string, object>
        {
            ["contentType"] = contentType,
            ["part"] = part,
        };

        if (name is not null)
        {
            arguments["name"] = name;
            arguments["displayName"] = "Second route";
        }

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveContentTypePartTool(), services, arguments);

        // Assert
        Assert.StartsWith("Nothing was changed", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        Assert.Empty(store.TypeNamed("Article").Parts);
        store.Manager.Verify(manager => manager.StoreTypeDefinitionAsync(It.IsAny<ContentTypeDefinition>()), Times.Never);
    }

    [Fact]
    public async Task RemoveContentTypePart_RemovesThePartThroughTheDefinitionService()
    {
        // Arrange
        var store = new ContentDefinitionTestStore();
        store.AddType("Article", type => type.WithPart("TitlePart").WithPart("AutoroutePart"));
        var services = store.BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RemoveContentTypePartTool(), services, new()
        {
            ["contentType"] = "article",
            ["name"] = "autoroutepart",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.True(json.GetProperty("removed").GetBoolean());
        Assert.Equal("AutoroutePart", json.GetProperty("name").GetString());
        store.Service.Verify(service => service.RemovePartFromTypeAsync("AutoroutePart", "Article"), Times.Once);
    }

    [Fact]
    public async Task RemoveContentTypePart_WhenThePartIsNotOnTheType_RemovesNothingAndListsTheParts()
    {
        // Arrange
        var store = new ContentDefinitionTestStore();
        store.AddType("Article", type => type.WithPart("TitlePart"));
        var services = store.BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RemoveContentTypePartTool(), services, new()
        {
            ["contentType"] = "Article",
            ["name"] = "AutoroutePart",
        });

        // Assert
        Assert.StartsWith("Nothing was removed", result, StringComparison.Ordinal);
        Assert.Contains("TitlePart", result, StringComparison.Ordinal);
        store.Service.Verify(service => service.RemovePartFromTypeAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RemoveContentTypePart_WhenThePartIsSystemDefined_SaysItCannotBeRemoved()
    {
        // Arrange
        var store = new ContentDefinitionTestStore();
        store.AddType("Article", type => type.WithPart("TitlePart"));
        store.Service
            .Setup(service => service.RemovePartFromTypeAsync("TitlePart", "Article"))
            .ThrowsAsync(new InvalidOperationException("Unable to remove system-defined part."));
        var services = store.BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RemoveContentTypePartTool(), services, new()
        {
            ["contentType"] = "Article",
            ["name"] = "TitlePart",
        });

        // Assert
        Assert.Contains("defined by the system", result, StringComparison.Ordinal);
    }
}
