using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Seo;

namespace CrestApps.OrchardCore.Tests.Seo;

public sealed class JsonLdTests
{
    [Fact]
    public void Add_WhenTwoNodesShareAnId_MergesThemAndTheLaterValuesWin()
    {
        // Arrange
        var graph = new JsonLdGraph();
        var first = JsonLd.CreateNode("Organization", "https://example.com/#publisher");
        first["name"] = "Old name";
        first["url"] = "https://example.com/";

        var second = JsonLd.CreateNode("Organization", "https://example.com/#publisher");
        second["name"] = "New name";

        // Act
        graph.Add(first);
        graph.Add(second);

        // Assert
        var node = Assert.Single(graph.Nodes);
        Assert.Equal("New name", JsonLd.GetString(node, "name"));
        Assert.Equal("https://example.com/", JsonLd.GetString(node, "url"));
    }

    [Fact]
    public void ToDocument_WritesOneContextAndTheNodesInAGraph()
    {
        // Arrange
        var graph = new JsonLdGraph();
        var node = JsonLd.CreateNode("WebSite", "https://example.com/#website");
        node["@context"] = "https://schema.org";
        graph.Add(node);

        // Act
        var document = graph.ToDocument();

        // Assert
        Assert.Equal("https://schema.org", JsonLd.GetString(document, "@context"));
        var item = Assert.IsType<JsonObject>(Assert.Single(document["@graph"].AsArray()));
        Assert.Null(item["@context"]);
        Assert.Equal("WebSite", JsonLd.GetType(item));
    }

    [Fact]
    public void ToDocument_WhenTheGraphIsEmpty_ReturnsNull()
    {
        Assert.Null(new JsonLdGraph().ToDocument());
    }

    [Fact]
    public void ToScriptJson_EscapesCharactersThatCouldCloseTheScriptElement()
    {
        // Arrange
        var node = new JsonObject
        {
            ["name"] = "</script><script>alert(1)</script>",
        };

        // Act
        var json = JsonLd.ToScriptJson(node);

        // Assert
        Assert.DoesNotContain("<", json, StringComparison.Ordinal);
        Assert.DoesNotContain(">", json, StringComparison.Ordinal);
        Assert.Equal("</script><script>alert(1)</script>", JsonLd.GetString(JsonNode.Parse(json).AsObject(), "name"));
    }

    [Theory]
    [InlineData("""{"@type":"Recipe","name":"Pie"}""", 1)]
    [InlineData("""[{"@type":"Recipe"},{"@type":"Person"},"text"]""", 2)]
    [InlineData("""{"@context":"https://schema.org","@graph":[{"@type":"Recipe"},{"@type":"Person"}]}""", 2)]
    public void TryParseNodes_ReadsAnObjectAnArrayOrAGraph(string json, int expectedCount)
    {
        // Act
        var parsed = JsonLd.TryParseNodes(json, out var nodes);

        // Assert
        Assert.True(parsed);
        Assert.Equal(expectedCount, nodes.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("42")]
    public void TryParseNodes_WhenTheTextHoldsNoObject_ReturnsFalse(string json)
    {
        Assert.False(JsonLd.TryParseNodes(json, out _));
    }

    [Fact]
    public void GetType_WhenTheNodeHasSeveralTypes_ReturnsTheFirst()
    {
        // Arrange
        var node = new JsonObject
        {
            ["@type"] = new JsonArray("Product", "Vehicle"),
        };

        // Act & Assert
        Assert.Equal("Product", JsonLd.GetType(node));
    }

    [Fact]
    public void SetIfMissing_KeepsAValueAnotherProviderAlreadySet()
    {
        // Arrange
        var node = new JsonObject
        {
            ["publisher"] = "editor's value",
        };

        // Act
        JsonLd.SetIfMissing(node, "publisher", JsonLd.Reference("https://example.com/#publisher"));
        JsonLd.SetIfMissing(node, "isPartOf", JsonLd.Reference("https://example.com/#website"));

        // Assert
        Assert.Equal("editor's value", JsonLd.GetString(node, "publisher"));
        Assert.Equal("https://example.com/#website", node["isPartOf"]?["@id"]?.GetValue<string>());
    }
}
