using CrestApps.OrchardCore.AI.Mcp.Core;

namespace CrestApps.OrchardCore.Tests.Core.Mcp;

public sealed class McpResourceUriTests
{
    [Theory]
    [InlineData("file://abc123/path/to/file.txt", "file", "abc123", "path/to/file.txt")]
    [InlineData("content://item1/id/contentItem456", "content", "item1", "id/contentItem456")]
    [InlineData("ftp://res99/documents/report.pdf", "ftp", "res99", "documents/report.pdf")]
    [InlineData("recipe-schema://item5/ContentDefinition", "recipe-schema", "item5", "ContentDefinition")]
    [InlineData("media://item7/images/logo.png", "media", "item7", "images/logo.png")]
    [InlineData("sftp://item8/remote/data.csv", "sftp", "item8", "remote/data.csv")]
    public void TryParse_WithValidUri_ReturnsTrue(string uri, string expectedScheme, string expectedItemId, string expectedPath)
    {
        var result = McpResourceUri.TryParse(uri, out var parsed);

        Assert.True(result);
        Assert.NotNull(parsed);
        Assert.Equal(expectedScheme, parsed.Scheme);
        Assert.Equal(expectedItemId, parsed.ItemId);
        Assert.Equal(expectedPath, parsed.Path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-uri")]
    [InlineData("://missing-scheme")]
    public void TryParse_WithInvalidUri_ReturnsFalse(string uri)
    {
        var result = McpResourceUri.TryParse(uri, out var parsed);

        Assert.False(result);
        Assert.Null(parsed);
    }

    [Fact]
    public void TryParse_WithEmptyPath_ReturnsEmptyPathString()
    {
        var result = McpResourceUri.TryParse("file://abc123/", out var parsed);

        Assert.True(result);
        Assert.Equal(string.Empty, parsed.Path);
    }

    [Theory]
    [InlineData("file", "abc123", "path/to/file.txt", "file://abc123/path/to/file.txt")]
    [InlineData("content", "item1", "id/contentItem456", "content://item1/id/contentItem456")]
    [InlineData("recipe-schema", "item5", "feature", "recipe-schema://item5/feature")]
    public void Build_WithValidInputs_ReturnsExpectedUri(string scheme, string itemId, string path, string expected)
    {
        var result = McpResourceUri.Build(scheme, itemId, path);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("file", "abc123", null)]
    [InlineData("file", "abc123", "")]
    public void Build_WithEmptyPath_ReturnsEmptyString(string scheme, string itemId, string path)
    {
        var result = McpResourceUri.Build(scheme, itemId, path);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Build_WithNullScheme_ThrowsArgumentException()
    {
        Assert.ThrowsAny<ArgumentException>(() => McpResourceUri.Build(null, "abc123", "path"));
    }

    [Fact]
    public void Build_WithEmptyScheme_ThrowsArgumentException()
    {
        Assert.ThrowsAny<ArgumentException>(() => McpResourceUri.Build(string.Empty, "abc123", "path"));
    }

    [Fact]
    public void Build_WithNullItemId_ThrowsArgumentException()
    {
        Assert.ThrowsAny<ArgumentException>(() => McpResourceUri.Build("file", null, "path"));
    }

    [Fact]
    public void Build_WithEmptyItemId_ThrowsArgumentException()
    {
        Assert.ThrowsAny<ArgumentException>(() => McpResourceUri.Build("file", string.Empty, "path"));
    }

    [Fact]
    public void Build_TrimsLeadingSlashFromPath()
    {
        var result = McpResourceUri.Build("file", "abc123", "/path/to/file.txt");

        Assert.Equal("file://abc123/path/to/file.txt", result);
    }

    [Fact]
    public void ToString_ReturnsBuiltUri()
    {
        _ = McpResourceUri.TryParse("content://item1/id/abc", out var parsed);

        Assert.Equal("content://item1/id/abc", parsed.ToString());
    }

    [Fact]
    public void TryParse_Roundtrip_PreservesAllComponents()
    {
        var original = McpResourceUri.Build("ftp", "itemX", "remote/path/file.txt");

        _ = McpResourceUri.TryParse(original, out var parsed);

        Assert.Equal("ftp", parsed.Scheme);
        Assert.Equal("itemx", parsed.ItemId);
        Assert.Equal("remote/path/file.txt", parsed.Path);
        Assert.Equal(original.ToLowerInvariant(), parsed.ToString().ToLowerInvariant());
    }

    [Theory]
    [InlineData("content://item1/{contentType}/list", "{contentType}/list")]
    [InlineData("content://item1/{contentType}/{contentItemId}", "{contentType}/{contentItemId}")]
    [InlineData("recipe-schema://item5/{step-name}", "{step-name}")]
    public void TryParse_PreservesCurlyBracesInPath(string uri, string expectedPath)
    {
        var result = McpResourceUri.TryParse(uri, out var parsed);

        Assert.True(result);
        Assert.Equal(expectedPath, parsed.Path);
    }

    [Fact]
    public void TryParse_BuildRoundtrip_PreservesCurlyBraces()
    {
        var original = McpResourceUri.Build("content", "item1", "{contentType}/list");

        _ = McpResourceUri.TryParse(original, out var parsed);

        Assert.Equal("{contentType}/list", parsed.Path);
        Assert.Equal(original, parsed.ToString());
    }
}
