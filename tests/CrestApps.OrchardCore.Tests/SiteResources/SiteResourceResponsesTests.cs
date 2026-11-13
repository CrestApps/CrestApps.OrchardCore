using CrestApps.OrchardCore.SiteResources.Models;
using CrestApps.OrchardCore.SiteResources.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.SiteResources;

public sealed class SiteResourceResponsesTests
{
    [Theory]
    [InlineData("brand.css", "brand", SiteResourceType.Stylesheet)]
    [InlineData("my.slider.JS", "my.slider", SiteResourceType.Script)]
    public void TryParseFileName_ACssOrJsFile_GivesTheNameAndType(string fileName, string expectedName, SiteResourceType expectedType)
    {
        // Act
        var parsed = SiteResourceResponses.TryParseFileName(fileName, out var name, out var type);

        // Assert
        Assert.True(parsed);
        Assert.Equal(expectedName, name);
        Assert.Equal(expectedType, type);
    }

    [Theory]
    [InlineData("brand")]
    [InlineData("brand.txt")]
    [InlineData(".css")]
    [InlineData("bad name.css")]
    public void TryParseFileName_AnythingElse_IsRefused(string fileName)
    {
        // Act & Assert
        Assert.False(SiteResourceResponses.TryParseFileName(fileName, out _, out _));
    }

    [Fact]
    public async Task Serve_TheCurrentVersion_IsCachedForAYear()
    {
        // Arrange
        var httpContext = CreateHttpContext();

        // Act
        var result = SiteResourceResponses.Serve(httpContext, Resource(), "v1", isPrivate: false);
        await result.ExecuteAsync(httpContext);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        Assert.Equal("public, max-age=31536000, immutable", httpContext.Response.Headers.CacheControl.ToString());
        Assert.StartsWith("text/css", httpContext.Response.ContentType);
    }

    [Fact]
    public void Serve_AnOldOrMissingVersion_IsCheckedAgainEveryTime()
    {
        // Arrange
        var httpContext = CreateHttpContext();

        // Act
        SiteResourceResponses.Serve(httpContext, Resource(), "old", isPrivate: true);

        // Assert
        Assert.Equal("private, no-cache", httpContext.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Serve_WhenTheBrowserHasIt_AnswersNotModified()
    {
        // Arrange
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers.IfNoneMatch = "\"v1\"";

        // Act
        var result = SiteResourceResponses.Serve(httpContext, Resource(), "v1", isPrivate: false);
        await result.ExecuteAsync(httpContext);

        // Assert
        Assert.Equal(StatusCodes.Status304NotModified, httpContext.Response.StatusCode);
    }

    private static SiteResource Resource()
        => new()
        {
            Name = "brand",
            Type = SiteResourceType.Stylesheet,
            ServedContent = "a{color:red}",
            Version = "v1",
        };

    private static DefaultHttpContext CreateHttpContext()
        => new()
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
            Response = { Body = new MemoryStream() },
        };
}
