using CrestApps.OrchardCore.AI.Mcp.Core;

namespace CrestApps.OrchardCore.Tests.Core.Mcp;

public sealed class McpResourceUriTests
{
    [Theory]
    [InlineData("file://server/{path}", "file://server/documents/report.pdf", "path", "documents/report.pdf")]
    [InlineData("recipe-step-schema://steps/{stepName}", "recipe-step-schema://steps/feature", "stepName", "feature")]
    [InlineData("content-item://items/{contentItemId}", "content-item://items/abc123", "contentItemId", "abc123")]
    public void TryMatch_WithSingleVariable_ExtractsCorrectly(string template, string uri, string expectedVar, string expectedValue)
    {
        var result = McpResourceUri.TryMatch(template, uri, out var variables);

        Assert.True(result);
        Assert.NotNull(variables);
        Assert.Equal(expectedValue, variables[expectedVar]);
    }

    [Fact]
    public void TryMatch_WithMultipleVariables_ExtractsAll()
    {
        var template = "content-type://items/{contentType}/{contentItemId}";
        var uri = "content-type://items/Article/abc123";

        var result = McpResourceUri.TryMatch(template, uri, out var variables);

        Assert.True(result);
        Assert.Equal("Article", variables["contentType"]);
        Assert.Equal("abc123", variables["contentItemId"]);
    }

    [Fact]
    public void TryMatch_WithNoVariables_MatchesExactUri()
    {
        var template = "recipe-schema://full-schema/recipe";
        var uri = "recipe-schema://full-schema/recipe";

        var result = McpResourceUri.TryMatch(template, uri, out var variables);

        Assert.True(result);
        Assert.NotNull(variables);
        Assert.Empty(variables);
    }

    [Fact]
    public void TryMatch_WithNonMatchingUri_ReturnsFalse()
    {
        var template = "file://server/{path}";
        var uri = "ftp://other/documents/report.pdf";

        var result = McpResourceUri.TryMatch(template, uri, out var variables);

        Assert.False(result);
        Assert.Null(variables);
    }

    [Theory]
    [InlineData(null, "file://server/path")]
    [InlineData("", "file://server/path")]
    [InlineData("file://server/{path}", null)]
    [InlineData("file://server/{path}", "")]
    public void TryMatch_WithNullOrEmptyInputs_ReturnsFalse(string template, string uri)
    {
        var result = McpResourceUri.TryMatch(template, uri, out var variables);

        Assert.False(result);
        Assert.Null(variables);
    }

    [Fact]
    public void TryMatch_IsCaseInsensitive()
    {
        var template = "Recipe-Schema://Steps/{stepName}";
        var uri = "recipe-schema://steps/Feature";

        var result = McpResourceUri.TryMatch(template, uri, out var variables);

        Assert.True(result);
        Assert.Equal("Feature", variables["stepName"]);
    }

    [Fact]
    public void TryMatch_DoesNotMatchPartialUri()
    {
        var template = "file://server/{path}";
        var uri = "other://server/path/extra/segments";

        var result = McpResourceUri.TryMatch(template, uri, out var variables);

        Assert.False(result);
    }

    [Theory]
    [InlineData("file://server/{path}", true)]
    [InlineData("recipe-schema://full-schema/recipe", false)]
    [InlineData("content-type://items/{contentType}/{contentItemId}", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsTemplate_ReturnsExpected(string uri, bool expected)
    {
        var result = McpResourceUri.IsTemplate(uri);

        Assert.Equal(expected, result);
    }
}
