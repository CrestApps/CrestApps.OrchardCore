using CrestApps.OrchardCore.AI.Mcp.Handlers;

namespace CrestApps.OrchardCore.Tests.Core.Mcp;

public sealed class McpResourceHandlerTests
{
    [Theory]
    [InlineData("file", "abc123", "docs/{fileName}", "file://abc123/docs/{fileName}")]
    [InlineData("content-item", "xyz", "{contentItemId}", "content-item://xyz/{contentItemId}")]
    [InlineData("recipe-schema", "abc", "", "recipe-schema://abc")]
    [InlineData("recipe-schema", "abc", null, "recipe-schema://abc")]
    [InlineData("file", "id1", "/leading-slash/path", "file://id1/leading-slash/path")]
    public void BuildUri_ConstructsCorrectUri(string source, string itemId, string path, string expected)
    {
        var result = McpResourceHandler.BuildUri(source, itemId, path);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("file://abc123/docs/{fileName}", "file", "abc123", "docs/{fileName}")]
    [InlineData("content-item://xyz/{contentItemId}", "content-item", "xyz", "{contentItemId}")]
    [InlineData("recipe-schema://abc", "recipe-schema", "abc", "")]
    [InlineData("file://abc123/deep/nested/path", "file", "abc123", "deep/nested/path")]
    public void ExtractPath_ReturnsCorrectPath(string uri, string source, string itemId, string expectedPath)
    {
        var result = McpResourceHandler.ExtractPath(uri, source, itemId);

        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void ExtractPath_WithMismatchedPrefix_ReturnsFullUri()
    {
        var result = McpResourceHandler.ExtractPath("other://xyz/path", "file", "abc123");

        Assert.Equal("other://xyz/path", result);
    }
}
