using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Core.Services;

/// <summary>
/// A signed link names one value for one purpose until it expires, and points at the site's own address.
/// </summary>
public sealed class SignedLinksTests
{
    private const string Purpose = "Tests.Purpose";

    private readonly EphemeralDataProtectionProvider _protection = new();
    private readonly TestClock _clock = new(DateTime.UtcNow);

    [Fact]
    public void AToken_HoldsItsValue_ForItsPurposeOnly()
    {
        // Arrange
        var tokens = SignedLinkDoubles.Tokens(_protection, _clock);

        // Act
        var token = tokens.Protect(Purpose, "order-1", TimeSpan.FromHours(1));

        // Assert
        Assert.Equal("order-1", tokens.Unprotect(Purpose, token));
        Assert.Null(tokens.Unprotect("Tests.OtherPurpose", token));
    }

    [Fact]
    public void AnExpiredAlteredOrMissingToken_HoldsNothing()
    {
        // Arrange
        var tokens = SignedLinkDoubles.Tokens(_protection, _clock);
        var token = tokens.Protect(Purpose, "order-1", TimeSpan.FromHours(1));

        // Act
        var altered = tokens.Unprotect(Purpose, string.Concat(token.AsSpan(0, token.Length - 2), token.EndsWith('A') ? "BB" : "AA"));
        _clock.UtcNow = _clock.UtcNow.AddHours(1).AddMinutes(1);

        // Assert
        Assert.Null(altered);
        Assert.Null(tokens.Unprotect(Purpose, token));
        Assert.Null(tokens.Unprotect(Purpose, null));
        Assert.Null(tokens.Unprotect(Purpose, " "));
    }

    [Fact]
    public void ATokenFromAnotherSitesKeys_HoldsNothing()
    {
        // Arrange
        var token = SignedLinkDoubles.Tokens().Protect(Purpose, "order-1", TimeSpan.FromHours(1));

        // Act
        var value = SignedLinkDoubles.Tokens(_protection, _clock).Unprotect(Purpose, token);

        // Assert
        Assert.Null(value);
    }

    [Fact]
    public async Task AUrl_StartsWithTheSitesBaseUrl()
    {
        // Arrange
        var urls = SignedLinkDoubles.Urls(SiteService("https://store.test/tenant/"), TemplateLinkGenerator.ForToken("Route", "/things/"), Request());

        // Act
        var url = await urls.GetUrlAsync("Route", new { token = "abc" });

        // Assert
        Assert.Equal("https://store.test/tenant/things/abc", url);
    }

    [Fact]
    public async Task WithoutABaseUrl_TheRequestBuildsTheUrl_UnlessOnlyTheSiteMay()
    {
        // Arrange
        var urls = SignedLinkDoubles.Urls(SiteService(baseUrl: null), TemplateLinkGenerator.ForToken("Route", "/things/"), Request());

        // Act
        var url = await urls.GetUrlAsync("Route", new { token = "abc" });
        var siteUrl = await urls.GetSiteUrlAsync("Route", new { token = "abc" });

        // Assert
        Assert.Equal("https://request.test/things/abc", url);
        Assert.Null(siteUrl);
    }

    [Fact]
    public async Task WithoutABaseUrlOrARequest_NoUrlIsMade()
    {
        // Arrange
        var urls = SignedLinkDoubles.Urls(SiteService(baseUrl: null), TemplateLinkGenerator.ForToken("Route", "/things/"));

        // Act
        var url = await urls.GetUrlAsync("Route", new { token = "abc" });

        // Assert
        Assert.Null(url);
    }

    private static ISiteService SiteService(string baseUrl)
    {
        var site = Mock.Of<ISite>(value => value.BaseUrl == baseUrl);
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site);

        return siteService.Object;
    }

    private static DefaultHttpContext Request()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("request.test");

        return httpContext;
    }
}
