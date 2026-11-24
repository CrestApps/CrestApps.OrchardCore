using CrestApps.OrchardCore.Learning.Credentials.Core.Services;
using Microsoft.AspNetCore.Http;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Learning.Credentials;

/// <summary>
/// Review round 9, M7: the issuer's address is the site's base URL, never the host of the request that triggered the
/// award, which the client chooses.
/// </summary>
public sealed class CredentialIssuerRound9Tests
{
    [Fact]
    public async Task Round9M7_WithoutTheSitesBaseUrl_ThereIsNoIssuer_WhateverTheRequestsHost()
    {
        // Arrange: a learner's request with a spoofed Host header, on a site with no base URL.
        var site = new Mock<ISite>();
        site.SetupGet(value => value.BaseUrl).Returns((string)null);
        site.SetupGet(value => value.SiteName).Returns("Learning");

        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("evil.example");

        var context = new CredentialIssuerContext(siteService.Object, new HttpContextAccessor { HttpContext = httpContext });

        // Act
        var issuer = await context.GetAsync();

        // Assert
        Assert.Null(issuer);
    }

    [Fact]
    public async Task Round9M7_WithTheSitesBaseUrl_TheIssuerIsTheBaseUrl()
    {
        var site = new Mock<ISite>();
        site.SetupGet(value => value.BaseUrl).Returns("https://lms.example.com/");
        site.SetupGet(value => value.SiteName).Returns("Learning");

        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("evil.example");

        var issuer = await new CredentialIssuerContext(siteService.Object, new HttpContextAccessor { HttpContext = httpContext }).GetAsync();

        Assert.Equal("https://lms.example.com", issuer.BaseUrl);
    }
}
