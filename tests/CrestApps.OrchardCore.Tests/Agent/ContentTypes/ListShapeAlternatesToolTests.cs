using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.ContentTypes;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Metadata.Settings;

namespace CrestApps.OrchardCore.Tests.Agent.ContentTypes;

/// <summary>
/// listShapeAlternates names the content item shape and the shapes of the parts and fields that have a display driver,
/// with the shape names Orchard Core's alternate factories give and the theme file names its harvester reads.
/// </summary>
public sealed class ListShapeAlternatesToolTests
{
    [Fact]
    public async Task ListShapeAlternates_InSummary_NamesTheItemPartAndFieldTemplates()
    {
        // Arrange
        using var services = CreateServices(authorized: true);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListShapeAlternatesTool(), services, new()
        {
            ["contentType"] = "BlogPost",
            ["displayType"] = "Summary",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var shapes = json.GetProperty("shapes").EnumerateArray().ToList();
        var item = Assert.Single(shapes, shape => shape.GetProperty("kind").GetString() == "contentItem");
        var title = Assert.Single(shapes, shape => shape.GetProperty("kind").GetString() == "part");
        var subtitle = Assert.Single(shapes, shape => shape.GetProperty("kind").GetString() == "field");

        Assert.Equal("Content_Summary", item.GetProperty("shape").GetString());
        Assert.Contains(Alternates(item), alternate => alternate.Name == "Content_Summary__BlogPost" && alternate.File == "Content-BlogPost.Summary");
        Assert.Contains(Alternates(item), alternate => alternate.Name == "Content__BlogPost" && alternate.File == "Content-BlogPost");

        Assert.Equal("TitlePart", title.GetProperty("shape").GetString());
        Assert.Contains(Alternates(title), alternate => alternate.Name == "TitlePart_Summary" && alternate.File == "TitlePart.Summary");
        Assert.Contains(Alternates(title), alternate => alternate.Name == "BlogPost_Summary__TitlePart" && alternate.File == "BlogPost-TitlePart.Summary");

        Assert.Equal("Subtitle", subtitle.GetProperty("field").GetString());
        Assert.Contains(Alternates(subtitle), alternate => alternate.Name == "BlogPost_Summary__BlogPost__Subtitle" && alternate.File == "BlogPost-BlogPost-Subtitle.Summary");

        // The unknown part without a driver is not listed as a shape, and the result says so.
        Assert.DoesNotContain(shapes, shape => shape.TryGetProperty("part", out var part) && part.GetString() == "Gallery" && shape.GetProperty("kind").GetString() == "part");
        Assert.Contains("Gallery", json.GetProperty("note").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListShapeAlternates_ByDefault_UsesTheDetailDisplayType()
    {
        // Arrange
        using var services = CreateServices(authorized: true);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListShapeAlternatesTool(), services, new()
        {
            ["contentType"] = "BlogPost",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var item = json.GetProperty("shapes").EnumerateArray().First();

        Assert.Equal("Detail", json.GetProperty("displayType").GetString());
        Assert.Equal("Content", item.GetProperty("shape").GetString());
        Assert.Contains(Alternates(item), alternate => alternate.Name == "Content_Detail__BlogPost" && alternate.File == "Content-BlogPost.Detail");
    }

    [Fact]
    public async Task ListShapeAlternates_ForAnUnknownType_SuggestsTheClosestNames()
    {
        // Arrange
        using var services = CreateServices(authorized: true);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListShapeAlternatesTool(), services, new()
        {
            ["contentType"] = "BlogPst",
        });

        // Assert
        Assert.StartsWith("There is no content type named 'BlogPst'. Did you mean BlogPost", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListShapeAlternates_WithoutTheRightToViewContentTypes_Refuses()
    {
        // Arrange
        using var services = CreateServices(authorized: false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListShapeAlternatesTool(), services, new()
        {
            ["contentType"] = "BlogPost",
        });

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Equal("ViewContentTypes", Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
    }

    private static List<(string Name, string File)> Alternates(JsonElement shape)
        => shape.GetProperty("alternates").EnumerateArray()
            .Select(alternate => (alternate.GetProperty("name").GetString(), alternate.GetProperty("file").GetString()))
            .ToList();

    private static ServiceProvider CreateServices(bool authorized)
    {
        var blogPostPart = new ContentPartDefinitionBuilder()
            .Named("BlogPost")
            .WithField("Subtitle", field => field.OfType("TextField").WithDisplayName("Subtitle"))
            .Build();

        var galleryPart = new ContentPartDefinitionBuilder().Named("GalleryPart").Build();

        var blogPost = new ContentTypeDefinitionBuilder()
            .WithName("BlogPost")
            .WithDisplayName("Blog post")
            .WithPart("TitlePart")
            .WithPart("BlogPost", blogPostPart, _ => { })
            .WithPart("Gallery", galleryPart, _ => { })
            .Build();

        var definitionManager = new Mock<IContentDefinitionManager>();
        definitionManager.Setup(manager => manager.GetTypeDefinitionAsync("BlogPost")).ReturnsAsync(blogPost);
        definitionManager.Setup(manager => manager.ListTypeDefinitionsAsync()).ReturnsAsync([blogPost]);

        var partDrivers = new Mock<IContentPartDisplayDriverResolver>();
        partDrivers.Setup(resolver => resolver.GetDisplayModeDrivers(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string partName, string _) => partName == "TitlePart" ? [Mock.Of<IContentPartDisplayDriver>()] : []);

        var fieldDrivers = new Mock<IContentFieldDisplayDriverResolver>();
        fieldDrivers.Setup(resolver => resolver.GetDisplayModeDrivers(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string fieldName, string _) => fieldName == "TextField" ? [Mock.Of<IContentFieldDisplayDriver>()] : []);

        var services = AgentToolTestContext.CreateServices(authorize: (_, _) => authorized);

        services.AddSingleton(definitionManager.Object);
        services.AddSingleton(partDrivers.Object);
        services.AddSingleton(fieldDrivers.Object);

        return services.BuildServiceProvider();
    }
}
