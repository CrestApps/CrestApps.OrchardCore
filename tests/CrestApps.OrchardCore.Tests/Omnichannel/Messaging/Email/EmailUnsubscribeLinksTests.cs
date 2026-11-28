using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email.Deliverability;
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
    public async Task TryRead_RefusesALinkPastItsLifetime()
    {
        // Arrange
        var clock = new DeliverabilityClock { UtcNow = DateTime.UtcNow };
        var links = CreateLinks("https://contoso.com", clock);
        var url = await links.CreateUrlAsync("ann@example.com", "support@contoso.com", TestContext.Current.CancellationToken);
        var token = url.Substring(url.LastIndexOf('/') + 1);

        // Act
        clock.UtcNow = clock.UtcNow.Add(EmailUnsubscribeLinks.Lifetime).AddDays(1);

        // Assert
        Assert.False(links.TryRead(token, out _, out _));
    }

    [Fact]
    public void TryRead_RefusesATokenThatIsNotBase64()
    {
        // Arrange
        var links = CreateLinks("https://contoso.com");

        // Act & Assert
        Assert.False(links.TryRead("a", out _, out _));
        Assert.False(links.TryRead("a!b@c", out _, out _));
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

    private static EmailUnsubscribeLinks CreateLinks(string baseUrl, DeliverabilityClock clock = null)
    {
        var site = new Mock<ISite>();
        site.SetupGet(value => value.BaseUrl).Returns(baseUrl);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        var signedTokens = new SignedTokenService(new EphemeralDataProtectionProvider(), clock ?? new DeliverabilityClock { UtcNow = DateTime.UtcNow });

        return new EmailUnsubscribeLinks(signedTokens, siteService.Object);
    }
}
