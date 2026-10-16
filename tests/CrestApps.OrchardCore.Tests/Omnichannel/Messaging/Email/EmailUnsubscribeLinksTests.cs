using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using Microsoft.AspNetCore.DataProtection;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email;

public sealed class EmailUnsubscribeLinksTests
{
    [Fact]
    public async Task CreateUrlAsync_BuildsALinkOnThePublicBaseUrlThatReadsBackTheAddresses()
    {
        // Arrange
        var links = CreateLinks("https://contoso.com/");

        // Act
        var url = await links.CreateUrlAsync("Ann@Example.com", "support@contoso.com", TestContext.Current.CancellationToken);
        var token = url.Substring(url.LastIndexOf('/') + 1);
        var read = links.TryRead(token, out var contact, out var service);

        // Assert
        Assert.StartsWith("https://contoso.com/omnichannel/email/unsubscribe/", url);
        Assert.True(read);
        Assert.Equal("ann@example.com", contact);
        Assert.Equal("support@contoso.com", service);
    }

    [Fact]
    public async Task TryRead_RefusesATamperedToken()
    {
        // Arrange
        var links = CreateLinks("https://contoso.com");
        var url = await links.CreateUrlAsync("ann@example.com", "support@contoso.com", TestContext.Current.CancellationToken);
        var token = url.Substring(url.LastIndexOf('/') + 1);
        var tampered = string.Concat(token.AsSpan(0, token.Length - 2), token.EndsWith("AA", StringComparison.Ordinal) ? "BB" : "AA");

        // Act & Assert
        Assert.False(links.TryRead(tampered, out _, out _));
        Assert.False(links.TryRead("not-a-token", out _, out _));
    }

    [Fact]
    public async Task CreateUrlAsync_WithoutAPublicBaseUrl_GivesNoLink()
    {
        // Arrange
        var links = CreateLinks(baseUrl: null);

        // Act
        var url = await links.CreateUrlAsync("ann@example.com", "support@contoso.com", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(url);
    }

    private static EmailUnsubscribeLinks CreateLinks(string baseUrl)
    {
        var site = new Mock<ISite>();
        site.SetupGet(value => value.BaseUrl).Returns(baseUrl);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return new EmailUnsubscribeLinks(new EphemeralDataProtectionProvider(), siteService.Object);
    }
}
