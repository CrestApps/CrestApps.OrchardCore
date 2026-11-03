using CrestApps.OrchardCore.Seo;
using CrestApps.OrchardCore.Seo.Services;

namespace CrestApps.OrchardCore.Tests.Seo;

public sealed class PageStructuredDataExtensionsTests
{
    [Fact]
    public void AddJsonLd_ANodeWithoutIdDescribesThePage_AndOthersJoinTheGraph()
    {
        // Arrange
        var page = new PageStructuredData();

        // Act
        var added = page.AddJsonLd("""[{ "@context": "https://schema.org", "@type": "AboutPage", "name": "About" }, { "@type": "Person", "@id": "#founder", "name": "Ada" }]""");

        // Assert
        Assert.True(added);
        Assert.Equal("""{"@type":"AboutPage","name":"About"}""", page.MainEntity.ToJsonString());
        Assert.Equal("#founder", JsonLd.GetString(Assert.Single(page.Nodes), "@id"));
    }

    [Fact]
    public void AddJsonLd_AddsToTheNodeThatAlreadyDescribesThePage()
    {
        // Arrange
        var page = new PageStructuredData();
        page.AddJsonLd("""{ "@type": "ContactPage", "name": "Contact" }""");

        // Act
        page.AddJsonLd("""{ "telephone": "+1 702 555 0100" }""");

        // Assert
        Assert.Equal("ContactPage", JsonLd.GetType(page.MainEntity));
        Assert.Equal("Contact", JsonLd.GetString(page.MainEntity, "name"));
        Assert.Equal("+1 702 555 0100", JsonLd.GetString(page.MainEntity, "telephone"));
    }

    [Fact]
    public void AddJsonLd_WhenTheTextIsNotJson_AddsNothing()
    {
        // Arrange
        var page = new PageStructuredData();

        // Act & Assert
        Assert.False(page.AddJsonLd("not json"));
        Assert.Null(page.MainEntity);
        Assert.Empty(page.Nodes);
    }
}
