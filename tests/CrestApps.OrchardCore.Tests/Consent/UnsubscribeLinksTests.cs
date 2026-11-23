using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Consent.Core.Services;
using CrestApps.OrchardCore.Consent.Unsubscribe;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.AspNetCore.DataProtection;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Consent;

/// <summary>
/// An unsubscribe link opens the e-mail preferences page with a token that holds the normalized address and the scope,
/// which only this site can make, only for unsubscribing, and only until it expires.
/// </summary>
public sealed class UnsubscribeLinksTests
{
    // The data protection keys check a link's expiry against the real time too, so the clock starts at it.
    private readonly DateTime _now = DateTime.UtcNow;
    private readonly EphemeralDataProtectionProvider _protection = new();
    private readonly TestClock _clock;

    public UnsubscribeLinksTests()
    {
        _clock = new TestClock(_now);
    }

    [Fact]
    public async Task CreateUrlAsync_ThenTryRead_GivesBackTheNormalizedAddressAndTheScope()
    {
        // Arrange
        var links = Links("https://shop.example.com/tenant/");
        var scope = new UnsubscribeScope { Kind = "MailingList", Id = "list-1" };

        // Act
        var url = await links.CreateUrlAsync(" Sam@Example.com ", scope, TestContext.Current.CancellationToken);
        var read = links.TryRead(TokenOf(url), out var ticket);

        // Assert
        Assert.StartsWith("https://shop.example.com/tenant/" + ConsentConstants.PreferencesPath + "?token=", url, StringComparison.Ordinal);
        Assert.True(read);
        Assert.Equal("sam@example.com", ticket.Address);
        Assert.Equal("MailingList", ticket.Scope.Kind);
        Assert.Equal("list-1", ticket.Scope.Id);
    }

    [Fact]
    public async Task TryRead_AllMarketing_HasNoId()
    {
        // Arrange
        var links = Links("https://shop.example.com");
        var url = await links.CreateUrlAsync("sam@example.com", UnsubscribeScope.AllMarketing, TestContext.Current.CancellationToken);

        // Act
        var read = links.TryRead(TokenOf(url), out var ticket);

        // Assert
        Assert.True(read);
        Assert.Equal(UnsubscribeScopeKinds.AllMarketing, ticket.Scope.Kind);
        Assert.Null(ticket.Scope.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task CreateUrlAsync_WithoutABaseUrl_ReturnsNull(string baseUrl)
    {
        // Act
        var url = await Links(baseUrl).CreateUrlAsync("sam@example.com", UnsubscribeScope.AllMarketing, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(url);
    }

    [Fact]
    public async Task CreateUrlAsync_WithoutAnAddress_ReturnsNull()
    {
        // Act
        var url = await Links("https://shop.example.com").CreateUrlAsync(" ", UnsubscribeScope.AllMarketing, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(url);
    }

    [Fact]
    public async Task TryRead_ATamperedToken_IsRefused()
    {
        // Arrange
        var links = Links("https://shop.example.com");
        var token = TokenOf(await links.CreateUrlAsync("sam@example.com", UnsubscribeScope.AllMarketing, TestContext.Current.CancellationToken));
        var tampered = string.Concat(token[0] == 'A' ? "B" : "A", token.AsSpan(1));

        // Act
        var read = links.TryRead(tampered, out var ticket);

        // Assert
        Assert.False(read);
        Assert.Null(ticket);
        Assert.False(links.TryRead("forged", out _));
        Assert.False(links.TryRead(null, out _));
    }

    [Fact]
    public void TryRead_ATokenSignedForAnotherPurpose_IsRefused()
    {
        // Arrange
        var links = Links("https://shop.example.com");
        var payload = """{"v":1,"a":"sam@example.com","k":"AllMarketing","i":null}""";
        var token = new SignedTokenService(_protection, _clock).Protect("CrestApps.Marketing.Unsubscribe", payload, TimeSpan.FromDays(1));

        // Act
        var read = links.TryRead(token, out _);

        // Assert
        Assert.False(read);
    }

    [Fact]
    public void TryRead_APayloadOfAnotherVersion_IsRefused()
    {
        // Arrange
        var links = Links("https://shop.example.com");
        var payload = """{"v":2,"a":"sam@example.com","k":"AllMarketing"}""";
        var token = new SignedTokenService(_protection, _clock).Protect(UnsubscribeLinks.TokenPurpose, payload, TimeSpan.FromDays(1));

        // Act
        var read = links.TryRead(token, out _);

        // Assert
        Assert.False(read);
    }

    [Fact]
    public async Task TryRead_AnExpiredLink_IsRefused()
    {
        // Arrange
        var links = Links("https://shop.example.com");
        var token = TokenOf(await links.CreateUrlAsync("sam@example.com", UnsubscribeScope.AllMarketing, TestContext.Current.CancellationToken));

        // Act
        _clock.UtcNow = _now + UnsubscribeLinks.Lifetime - TimeSpan.FromMinutes(1);
        var stillValid = links.TryRead(token, out _);
        _clock.UtcNow = _now + UnsubscribeLinks.Lifetime + TimeSpan.FromMinutes(1);
        var expired = links.TryRead(token, out _);

        // Assert
        Assert.True(stillValid);
        Assert.False(expired);
    }

    [Fact]
    public async Task TryRead_ALinkOfAnotherSite_IsRefused()
    {
        // Arrange
        var token = TokenOf(await Links("https://shop.example.com").CreateUrlAsync("sam@example.com", UnsubscribeScope.AllMarketing, TestContext.Current.CancellationToken));
        var otherSite = new UnsubscribeLinks(new SignedTokenService(new EphemeralDataProtectionProvider(), _clock), SiteService("https://other.example.com"));

        // Act
        var read = otherSite.TryRead(token, out _);

        // Assert
        Assert.False(read);
    }

    private UnsubscribeLinks Links(string baseUrl)
        => new(new SignedTokenService(_protection, _clock), SiteService(baseUrl));

    private static string TokenOf(string url)
        => Uri.UnescapeDataString(url.AsSpan(url.IndexOf("?token=", StringComparison.Ordinal) + "?token=".Length));

    private static ISiteService SiteService(string baseUrl)
    {
        var site = new Mock<ISite>();
        site.SetupGet(value => value.BaseUrl).Returns(baseUrl);
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return siteService.Object;
    }
}
