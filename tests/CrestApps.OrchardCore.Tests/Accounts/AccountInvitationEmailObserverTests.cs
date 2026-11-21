using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Email;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Accounts;

/// <summary>
/// The invitation e-mail carries an absolute link, because it is opened from a mail client: the site's configured
/// address when there is one, and the address of the request that sent it when there is none.
/// </summary>
public sealed class AccountInvitationEmailObserverTests
{
    [Theory]
    [InlineData("", "https://learn.example.com/account/invitations/tok")]
    [InlineData("https://site.example.com/", "https://site.example.com/account/invitations/tok")]
    public async Task TheLink_IsAbsolute(string baseUrl, string expected)
    {
        // Arrange
        var site = new Mock<ISite>();
        site.Setup(value => value.BaseUrl).Returns(baseUrl);
        site.Setup(value => value.SiteName).Returns("Learning");

        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        MailMessage sent = null;
        var email = new Mock<IEmailService>();
        email.Setup(service => service.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<MailMessage, string, CancellationToken>((message, _, _) => sent = message)
            .ReturnsAsync(global::OrchardCore.Infrastructure.Result.Success());

        var content = new Mock<IContentManager>();
        content.Setup(manager => manager.GetAsync("acme", It.IsAny<VersionOptions>())).ReturnsAsync(new ContentItem { ContentItemId = "acme", DisplayText = "Acme" });

        var localClock = new Mock<ILocalClock>();
        localClock.Setup(clock => clock.ConvertToLocalAsync(It.IsAny<DateTimeOffset>())).Returns<DateTimeOffset>(Task.FromResult);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("learn.example.com");

        var observer = new AccountInvitationEmailObserver(email.Object, siteService.Object, content.Object, localClock.Object, new HttpContextAccessor { HttpContext = httpContext }, NullLogger<AccountInvitationEmailObserver>.Instance, new Localizer());

        // Act
        await observer.InvitedAsync(new AccountInvitation { AccountId = "acme", Email = "new@example.com", ExpiresUtc = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc) }, "tok", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(sent);
        Assert.Contains($"href=\"{expected}\"", sent.HtmlBody, StringComparison.Ordinal);
    }

    private sealed class Localizer : IStringLocalizer<AccountInvitationEmailObserver>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
