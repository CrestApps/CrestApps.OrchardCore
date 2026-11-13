using CrestApps.OrchardCore.ThemeDesigner.Services;

namespace CrestApps.OrchardCore.Tests.ThemeDesigner;

public sealed class ThemeDraftPagePreviewerTests
{
    [Theory]
    [InlineData("/", true)]
    [InlineData("/blog/my-post?page=2", true)]
    [InlineData("//evil.example/x", false)]
    [InlineData("/\\evil.example", false)]
    [InlineData("https://evil.example/", false)]
    [InlineData("blog", false)]
    [InlineData("", false)]
    public void IsLocalPath_OnlyAcceptsPathsOnThisSite(string path, bool expected)
        => Assert.Equal(expected, ThemeDraftPagePreviewer.IsLocalPath(path));

    [Fact]
    public void Parse_ReadsTheTitleResourcesAndACompactBody()
    {
        // Arrange
        const string Html =
            """
            <html><head><title>Bakery &amp; Co</title>
            <link href="/site-resources/site-theme.css?v=1" rel="stylesheet" />
            <link rel="stylesheet" href="/TheBlogTheme/css/styles.css">
            <style>body { color: red; }</style></head>
            <body>
              <!-- a comment -->
              <header class="site-header">   <h1>Bakery</h1> </header>
              <svg viewBox="0 0 10 10"><path d="M0 0"/></svg>
              <script src="/scripts/app.js"></script>
              <script>console.log('inline');</script>
              <div role="status" data-theme-draft-bar style="x"><span>Previewing</span><form method="post"><button>Stop</button></form></div>
            </body></html>
            """;

        // Act
        var preview = ThemeDraftPagePreviewer.Parse(Html);

        // Assert
        Assert.Equal("Bakery & Co", preview.Title);
        Assert.Equal(["/site-resources/site-theme.css?v=1", "/TheBlogTheme/css/styles.css"], preview.Stylesheets);
        Assert.Equal(["/scripts/app.js"], preview.Scripts);
        Assert.Equal("<header class=\"site-header\"> <h1>Bakery</h1> </header> <svg/>", preview.BodyMarkup);
        Assert.False(preview.Truncated);
    }
}
