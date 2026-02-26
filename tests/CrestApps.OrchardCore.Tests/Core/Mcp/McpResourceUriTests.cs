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

    [Theory]
    [InlineData("file://id/{fileName} ", "file://id/test", "fileName", "test")]
    [InlineData("  file://id/{fileName}", "file://id/test", "fileName", "test")]
    [InlineData("file://id/{fileName}", "  file://id/test  ", "fileName", "test")]
    [InlineData("  file://id/{fileName}  ", "  file://id/test  ", "fileName", "test")]
    public void TryMatch_WithWhitespace_TrimsAndMatchesCorrectly(string template, string uri, string expectedVar, string expectedValue)
    {
        var result = McpResourceUri.TryMatch(template, uri, out var variables);

        Assert.True(result);
        Assert.NotNull(variables);
        Assert.Equal(expectedValue, variables[expectedVar]);
    }

    [Theory]
    [InlineData("   ", "file://server/test")]
    [InlineData("file://server/{path}", "   ")]
    [InlineData("   ", "   ")]
    public void TryMatch_WithWhitespaceOnlyInputs_ReturnsFalse(string template, string uri)
    {
        var result = McpResourceUri.TryMatch(template, uri, out var variables);

        Assert.False(result);
        Assert.Null(variables);
    }

    [Theory]
    [InlineData("  {path}  ", true)]
    [InlineData("   ", false)]
    [InlineData("  no-template  ", false)]
    public void IsTemplate_WithWhitespace_TrimsAndReturnsExpected(string uri, bool expected)
    {
        var result = McpResourceUri.IsTemplate(uri);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryMatch_WithEncodedUri_DecodesValues()
    {
        var template = "file://server/{path}";
        var uri = "file://server/my%20file%20name.txt";

        var result = McpResourceUri.TryMatch(template, uri, out var variables);

        Assert.True(result);
        Assert.Equal("my file name.txt", variables["path"]);
    }

    [Fact]
    public void TryMatch_LastVariable_MatchesMultiplePathSegments()
    {
        var template = "file://server/{path}";
        var uri = "file://server/docs/reports/2024/report.pdf";

        var result = McpResourceUri.TryMatch(template, uri, out var variables);

        Assert.True(result);
        Assert.Equal("docs/reports/2024/report.pdf", variables["path"]);
    }

    [Fact]
    public void TryMatch_NonLastVariable_MatchesSingleSegmentOnly()
    {
        var template = "content://{type}/{id}";
        var uri = "content://Article/abc123";

        var result = McpResourceUri.TryMatch(template, uri, out var variables);

        Assert.True(result);
        Assert.Equal("Article", variables["type"]);
        Assert.Equal("abc123", variables["id"]);
    }

    [Fact]
    public void TryMatch_NonLastVariable_DoesNotMatchMultipleSegments()
    {
        var template = "content://{type}/{id}";
        var uri = "content://Article/Sub/abc123";

        var result = McpResourceUri.TryMatch(template, uri, out var variables);

        // {type} matches "Article", {id} (last var) matches "Sub/abc123"
        Assert.True(result);
        Assert.Equal("Article", variables["type"]);
        Assert.Equal("Sub/abc123", variables["id"]);
    }

    [Fact]
    public void TryMatch_VariablesAreCaseInsensitive()
    {
        var template = "file://server/{FileName}";
        var uri = "file://server/test.txt";

        var result = McpResourceUri.TryMatch(template, uri, out var variables);

        Assert.True(result);
        Assert.Equal("test.txt", variables["FileName"]);
        Assert.Equal("test.txt", variables["filename"]);
    }

    [Fact]
    public void TryMatch_WithSpecialCharactersInLiteral_EscapesCorrectly()
    {
        var template = "schema://host.name/{param}";
        var uri = "schema://host.name/value";

        var result = McpResourceUri.TryMatch(template, uri, out var variables);

        Assert.True(result);
        Assert.Equal("value", variables["param"]);
    }

    [Fact]
    public void TryMatch_WithThreeVariables_ExtractsAll()
    {
        var template = "data://{org}/{repo}/{file}";
        var uri = "data://myorg/myrepo/src/main.cs";

        var result = McpResourceUri.TryMatch(template, uri, out var variables);

        Assert.True(result);
        Assert.Equal("myorg", variables["org"]);
        Assert.Equal("myrepo", variables["repo"]);
        Assert.Equal("src/main.cs", variables["file"]);
    }

    [Fact]
    public void TryMatch_EmptyVariableValue_DoesNotMatch()
    {
        var template = "file://server/{path}";
        var uri = "file://server/";

        var result = McpResourceUri.TryMatch(template, uri, out var variables);

        // .+ requires at least one character
        Assert.False(result);
    }

    [Fact]
    public void TryMatch_ExactLiteralNoVariables_MatchesExactly()
    {
        var template = "static://exact/uri/path";
        var uri = "static://exact/uri/path";

        var result = McpResourceUri.TryMatch(template, uri, out var variables);

        Assert.True(result);
        Assert.NotNull(variables);
        Assert.Empty(variables);
    }

    [Fact]
    public void TryMatch_ExactLiteralNoVariables_DoesNotMatchDifferentUri()
    {
        var template = "static://exact/uri/path";
        var uri = "static://exact/uri/other";

        var result = McpResourceUri.TryMatch(template, uri, out var variables);

        Assert.False(result);
    }
}
