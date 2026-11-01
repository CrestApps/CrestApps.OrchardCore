using CrestApps.OrchardCore.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.WebMcp.Services;

namespace CrestApps.OrchardCore.Tests.AI.WebMcp;

public sealed class WebMcpBrowserToolValidatorTests
{
    [Fact]
    public void Validate_KeepsAValidTool()
    {
        // Arrange
        var descriptors = new[]
        {
            Descriptor("page_get_info", inputSchema: """{"type":"object","properties":{}}"""),
        };

        // Act
        var tools = WebMcpBrowserToolValidator.Validate(descriptors);

        // Assert
        var tool = Assert.Single(tools);
        Assert.Equal("page_get_info", tool.Name);
        Assert.Equal("Title", tool.Title);
        Assert.Equal("Does something.", tool.Description);
        Assert.Equal("object", tool.InputSchema.GetProperty("type").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("dotted.name")]
    [InlineData("a-name-that-is-far-too-long-for-an-ai-provider-to-accept-as-a-function-1")]
    public void Validate_DropsToolsWithNamesProvidersRefuse(string name)
    {
        // Act
        var tools = WebMcpBrowserToolValidator.Validate([Descriptor(name)]);

        // Assert
        Assert.Empty(tools);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Validate_DropsToolsWithoutADescription(string description)
    {
        // Act
        var tools = WebMcpBrowserToolValidator.Validate([Descriptor("page_tool", description: description)]);

        // Assert
        Assert.Empty(tools);
    }

    [Fact]
    public void Validate_DropsToolsWithADescriptionLongerThanTheLimit()
    {
        // Act
        var tools = WebMcpBrowserToolValidator.Validate([Descriptor("page_tool", description: new string('x', WebMcpBrowserToolValidator.MaxDescriptionLength + 1))]);

        // Assert
        Assert.Empty(tools);
    }

    [Theory]
    [InlineData("[1,2]")]
    [InlineData("{broken")]
    [InlineData("\"text\"")]
    public void Validate_DropsToolsWhoseSchemaIsNotAJsonObject(string inputSchema)
    {
        // Act
        var tools = WebMcpBrowserToolValidator.Validate([Descriptor("page_tool", inputSchema: inputSchema)]);

        // Assert
        Assert.Empty(tools);
    }

    [Fact]
    public void Validate_DropsToolsWithASchemaLargerThanTheLimit()
    {
        // Arrange
        var inputSchema = $$"""{"type":"object","description":"{{new string('x', WebMcpBrowserToolValidator.MaxSchemaLength)}}"}""";

        // Act
        var tools = WebMcpBrowserToolValidator.Validate([Descriptor("page_tool", inputSchema: inputSchema)]);

        // Assert
        Assert.Empty(tools);
    }

    [Fact]
    public void Validate_GivesAToolWithoutASchemaAnEmptyObjectSchema()
    {
        // Act
        var tool = Assert.Single(WebMcpBrowserToolValidator.Validate([Descriptor("page_tool", inputSchema: null)]));

        // Assert
        Assert.Equal("object", tool.InputSchema.GetProperty("type").GetString());
    }

    [Fact]
    public void Validate_KeepsTheFirstToolOfEachName()
    {
        // Act
        var tools = WebMcpBrowserToolValidator.Validate(
        [
            Descriptor("page_tool", description: "First."),
            Descriptor("PAGE_TOOL", description: "Second."),
        ]);

        // Assert
        var tool = Assert.Single(tools);
        Assert.Equal("First.", tool.Description);
    }

    [Fact]
    public void Validate_KeepsAtMostTheLimitOfTools()
    {
        // Arrange
        var descriptors = Enumerable.Range(0, WebMcpBrowserToolValidator.MaxTools + 10)
            .Select(index => Descriptor($"page_tool_{index}"));

        // Act
        var tools = WebMcpBrowserToolValidator.Validate(descriptors);

        // Assert
        Assert.Equal(WebMcpBrowserToolValidator.MaxTools, tools.Count);
    }

    [Fact]
    public void Validate_AcceptsNothing()
    {
        Assert.Empty(WebMcpBrowserToolValidator.Validate(null));
        Assert.Empty(WebMcpBrowserToolValidator.Validate([null]));
    }

    private static WebMcpBrowserToolDescriptor Descriptor(string name, string description = "Does something.", string inputSchema = """{"type":"object"}""")
    {
        return new WebMcpBrowserToolDescriptor
        {
            Name = name,
            Title = "Title",
            Description = description,
            InputSchema = inputSchema,
        };
    }
}
