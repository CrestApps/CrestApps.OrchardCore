using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.WebMcp.Services;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.AI.WebMcp;

public sealed class WebMcpToolResultFormatterTests
{
    [Fact]
    public void Format_AnswersAnEmptyTextForNothing()
    {
        Assert.Equal(string.Empty, WebMcpToolResultFormatter.Format(null));
    }

    [Fact]
    public void Format_KeepsTextAsItIs()
    {
        Assert.Equal("Hello", WebMcpToolResultFormatter.Format("Hello"));
    }

    [Fact]
    public void Format_ReadsAJsonStringAsItsText()
    {
        // Arrange
        var element = JsonDocument.Parse("\"Hello\"").RootElement;

        // Act
        var text = WebMcpToolResultFormatter.Format(element);

        // Assert
        Assert.Equal("Hello", text);
    }

    [Fact]
    public void Format_WritesOtherJsonAsJson()
    {
        // Arrange
        var element = JsonDocument.Parse("""{"count":2}""").RootElement;
        var node = JsonNode.Parse("""[1,2]""");

        // Act & Assert
        Assert.Equal("""{"count":2}""", WebMcpToolResultFormatter.Format(element));
        Assert.Equal("[1,2]", WebMcpToolResultFormatter.Format(node));
    }

    [Fact]
    public void Format_JoinsTheTextOfAIContents()
    {
        // Arrange
        var contents = new List<AIContent>
        {
            new TextContent("one"),
            new TextContent("two"),
        };

        // Act & Assert
        Assert.Equal("one", WebMcpToolResultFormatter.Format(new TextContent("one")));
        Assert.Equal("one\ntwo", WebMcpToolResultFormatter.Format(contents));
    }

    [Fact]
    public void Format_SerializesOtherObjectsAsJson()
    {
        Assert.Equal("""{"Name":"x","Count":3}""", WebMcpToolResultFormatter.Format(new { Name = "x", Count = 3 }));
    }
}
