using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Seo;
using CrestApps.OrchardCore.Seo.Drivers;
using CrestApps.OrchardCore.Seo.Providers;
using CrestApps.OrchardCore.Seo.Services;

namespace CrestApps.OrchardCore.Tests.Seo;

public sealed class StructuredDataPropertyMapTests
{
    [Fact]
    public void Parse_SplitsEachLineAtItsFirstColonAndSkipsCommentsAndBlankLines()
    {
        // Arrange
        var text = """
            # The article's dates
            headline: {{ ContentItem.DisplayText }}

            datePublished: {{ ContentItem.PublishedUtc | date: "%Y-%m-%d" }}
            not a property
            two words: ignored
            """;

        // Act
        var properties = StructuredDataPropertyMap.Parse(text);

        // Assert
        Assert.Collection(
            properties,
            property =>
            {
                Assert.Equal("headline", property.Key);
                Assert.Equal("{{ ContentItem.DisplayText }}", property.Value);
            },
            property =>
            {
                Assert.Equal("datePublished", property.Key);
                Assert.Equal("{{ ContentItem.PublishedUtc | date: \"%Y-%m-%d\" }}", property.Value);
            });
    }

    [Fact]
    public void Apply_WithADottedName_SetsANestedProperty()
    {
        // Arrange
        var node = new JsonObject();

        // Act
        StructuredDataPropertyMap.Apply(node, "offers.price", "19.99");
        StructuredDataPropertyMap.Apply(node, "offers.priceCurrency", "USD");

        // Assert
        Assert.Equal("19.99", node["offers"]?["price"]?.GetValue<string>());
        Assert.Equal("USD", node["offers"]?["priceCurrency"]?.GetValue<string>());
    }

    [Fact]
    public void Apply_WithAJsonValue_StoresJson()
    {
        // Arrange
        var node = new JsonObject();

        // Act
        StructuredDataPropertyMap.Apply(node, "author", """{"@type":"Person","name":"Ada"}""");
        StructuredDataPropertyMap.Apply(node, "keywords", """["a","b"]""");
        StructuredDataPropertyMap.Apply(node, "description", "{not json}");

        // Assert
        Assert.Equal("Person", JsonLd.GetType(node["author"].AsObject()));
        Assert.Equal(2, node["keywords"].AsArray().Count);
        Assert.Equal("{not json}", node["description"]?.GetValue<string>());
    }

    [Fact]
    public void Apply_WithAnEmptyValue_LeavesTheNodeUnchanged()
    {
        // Arrange
        var node = new JsonObject
        {
            ["name"] = "Kept",
        };

        // Act
        StructuredDataPropertyMap.Apply(node, "name", "   ");

        // Assert
        Assert.Equal("Kept", node["name"]?.GetValue<string>());
    }

    [Theory]
    [InlineData("Article", true)]
    [InlineData("BlogPosting", true)]
    [InlineData("Product2", true)]
    [InlineData("2Product", false)]
    [InlineData("Blog Posting", false)]
    [InlineData("schema:Article", false)]
    public void IsValidTypeName_AcceptsOneWordThatStartsWithALetter(string name, bool expected)
    {
        Assert.Equal(expected, StructuredDataTypeSettingsDisplayDriver.IsValidTypeName(name));
    }

    [Fact]
    public void FormatDate_WritesAnIsoUtcDateTime()
    {
        // Act
        var formatted = ContentItemStructuredDataProvider.FormatDate(new DateTime(2026, 11, 2, 15, 4, 5, DateTimeKind.Unspecified));

        // Assert
        Assert.Equal("2026-11-02T15:04:05Z", formatted);
    }

    [Theory]
    [InlineData("<p>Hello <b>world</b> &amp; friends</p>", "Hello world & friends")]
    [InlineData("<style>p{}</style><p>Text</p><script>alert(1)</script>", "Text")]
    public void ToPlainText_RemovesMarkupScriptsAndEntities(string html, string expected)
    {
        Assert.Equal(expected, StructuredDataText.ToPlainText(html));
    }

    [Fact]
    public void Truncate_CutsAtAWordAndAddsAnEllipsis()
    {
        // Act
        var text = StructuredDataText.Truncate("The quick brown fox jumps over the lazy dog", 20);

        // Assert
        Assert.Equal("The quick brown fox…", text);
    }
}
