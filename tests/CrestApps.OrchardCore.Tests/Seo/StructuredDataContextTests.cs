using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Seo;

namespace CrestApps.OrchardCore.Tests.Seo;

public sealed class StructuredDataContextTests
{
    [Theory]
    [InlineData("/blog/post", "https://example.com/blog/post")]
    [InlineData("~/media/logo.png", "https://example.com/tenant/media/logo.png")]
    [InlineData("media/logo.png", "https://example.com/tenant/media/logo.png")]
    [InlineData("https://cdn.example.org/logo.png", "https://cdn.example.org/logo.png")]
    [InlineData("//cdn.example.org/logo.png", "https://cdn.example.org/logo.png")]
    public void ToAbsoluteUrl_ResolvesSiteRelativeUrlsAgainstTheSite(string url, string expected)
    {
        // Arrange
        var context = new StructuredDataContext(null, StructuredDataPurpose.Page, "https://example.com/tenant/");

        // Act & Assert
        Assert.Equal(expected, context.ToAbsoluteUrl(url));
    }

    [Fact]
    public void SuggestType_WhenTheContentTypeSettingsChoseAType_KeepsIt()
    {
        // Arrange
        var context = new StructuredDataContext(null, StructuredDataPurpose.Page, "https://example.com")
        {
            MainEntity = new JsonObject
            {
                ["@type"] = "Event",
            },
            ConfiguredType = "Event",
        };

        // Act
        context.SuggestType("Product");

        // Assert
        Assert.Equal("Event", JsonLd.GetType(context.MainEntity));
    }

    [Fact]
    public void SuggestType_WhenNoTypeWasConfigured_SetsIt()
    {
        // Arrange
        var context = new StructuredDataContext(null, StructuredDataPurpose.Page, "https://example.com")
        {
            MainEntity = [],
        };

        // Act
        context.SuggestType("Product");

        // Assert
        Assert.Equal("Product", JsonLd.GetType(context.MainEntity));
    }
}
