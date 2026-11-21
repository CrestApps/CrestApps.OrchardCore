using System.Runtime.CompilerServices;
using System.Security.Claims;
using CrestApps.OrchardCore.AgenticCommerce.Feeds;
using CrestApps.OrchardCore.Products.Core.Feeds;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// SEC-C17k: the product feed is pushed over SFTP only to a server whose host key the store knows: without a host key
/// fingerprint, any server answering for the host name would be trusted with the catalog and the login.
/// </summary>
public sealed class AgentFeedSftpHostKeyTests
{
    [Fact]
    public async Task PushAsync_WithoutAHostKeyFingerprint_IsRefusedBeforeConnecting()
    {
        // Arrange
        var settings = new AgentFeedSettings
        {
            Sftp = new AgentFeedSftpSettings { Enabled = true, Host = "sftp.example.test", Username = "feeds" },
        };
        var site = new SiteSettings();
        var siteService = new Mock<ISiteService>();

        site.Put(settings);
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site);

        var pusher = new AgentFeedSftpPusher(
            siteService.Object,
            (OpenAIProductFeedService)RuntimeHelpers.GetUninitializedObject(typeof(OpenAIProductFeedService)),
            new InMemoryDocumentManager<AgentFeedStatusDocument>(),
            new EphemeralDataProtectionProvider(),
            Mock.Of<IClock>(clock => clock.UtcNow == new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc)),
            NullLogger<AgentFeedSftpPusher>.Instance);

        // Act
        var result = await pusher.PushAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("fingerprint", result.Message, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task Settings_TurningThePushOnWithoutAHostKeyFingerprint_IsRefusedOnSave()
    {
        // Arrange: a push without the fingerprint never runs, so saving one would look fine and silently do nothing.
        var authorization = new Mock<IAuthorizationService>();

        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

        var driver = new AgentFeedSettingsDisplayDriver(
            new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("Test")) } },
            authorization.Object,
            (OpenAIProductFeedService)RuntimeHelpers.GetUninitializedObject(typeof(OpenAIProductFeedService)),
            Mock.Of<IProductFeedService>(),
            new InMemoryDocumentManager<AgentFeedStatusDocument>(),
            new EphemeralDataProtectionProvider(),
            Mock.Of<IHostEnvironment>(),
            new PassThroughLocalizer());
        var settings = new AgentFeedSettings();
        var context = PostedFormUpdateModel.CreateContext(new AgentFeedSettingsViewModel
        {
            SftpEnabled = true,
            SftpHost = "sftp.example.test",
            SftpUsername = "feeds",
            SftpPassword = "secret",
        });

        // Act
        await driver.UpdateAsync(new SiteSettings(), settings, context);

        // Assert
        Assert.False(context.Updater.ModelState.IsValid);
        Assert.Contains(context.Updater.ModelState, entry => entry.Key.EndsWith(nameof(AgentFeedSettingsViewModel.SftpHostKeyFingerprint), StringComparison.Ordinal));
    }

    /// <summary>
    /// MD5 (fix verification, round 6): the settings kept the stored SFTP password and key when the field came back blank,
    /// whatever host the push now named, so a user who may change the settings, but never saw the password, could send it
    /// to a server of their own with the next push.
    /// </summary>
    [Theory]
    [InlineData("sftp.attacker.test", 22, "feeds", "SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU", false)]
    [InlineData("sftp.example.test", 2222, "feeds", "SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU", false)]
    [InlineData("sftp.example.test", 22, "root", "SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU", false)]
    [InlineData("sftp.example.test", 22, "feeds", "SHA256:nThbg6kXUpJWGl7E1IGOCspRomTxdCARLviKw6E5SY8", false)]
    [InlineData("SFTP.example.test", 22, "feeds", "SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU", true)]
    public async Task Settings_KeepTheStoredPasswordAndKey_OnlyForTheSameServerAndAccount(string host, int port, string username, string fingerprint, bool kept)
    {
        // Arrange
        var protection = new EphemeralDataProtectionProvider();
        var protector = protection.CreateProtector(AgentFeedSettings.ProtectionPurpose);
        var settings = new AgentFeedSettings
        {
            Sftp = new AgentFeedSftpSettings
            {
                Host = "sftp.example.test",
                Port = 22,
                Username = "feeds",
                HostKeyFingerprint = "SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU",
                Password = protector.Protect("password"),
                PrivateKey = protector.Protect("key"),
            },
        };
        var stored = settings.Sftp.Password;
        var context = PostedFormUpdateModel.CreateContext(new AgentFeedSettingsViewModel
        {
            SftpHost = host,
            SftpPort = port,
            SftpUsername = username,
            SftpHostKeyFingerprint = fingerprint,
        });

        // Act
        await Driver(protection).UpdateAsync(new SiteSettings(), settings, context);

        // Assert
        Assert.Equal(kept, settings.Sftp.Password == stored);
        Assert.Equal(kept, settings.Sftp.PrivateKey is not null);
    }

    /// <summary>
    /// F10 (fix verification, round 7): a feed API target kept its stored bearer token when the box came back blank,
    /// whatever base URL it now named, so a user who may change the settings, but never saw the token, could send it to a
    /// host of their own with the next push.
    /// </summary>
    [Theory]
    [InlineData("https://feeds.attacker.test/acp", false)]
    [InlineData("https://api.example.test:8443/acp", false)]
    [InlineData("https://API.example.test/v2/acp", true)]
    public async Task Settings_KeepAFeedApiTargetsToken_OnlyForTheSameOrigin(string baseUrl, bool kept)
    {
        // Arrange
        var protection = new EphemeralDataProtectionProvider();
        var token = protection.CreateProtector(AgentFeedSettings.ProtectionPurpose).Protect("feed-token");
        var settings = new AgentFeedSettings
        {
            FeedApiTargets = [new AcpFeedTarget { Id = "partner", Name = "Partner", BaseUrl = "https://api.example.test/acp", BearerToken = token }],
        };
        var context = PostedFormUpdateModel.CreateContext(new AgentFeedSettingsViewModel
        {
            FeedApiTargets = [new AcpFeedTargetViewModel { Id = "partner", Name = "Partner", BaseUrl = baseUrl, Enabled = true }],
        });

        // Act
        await Driver(protection).UpdateAsync(new SiteSettings(), settings, context);

        // Assert
        var target = Assert.Single(settings.FeedApiTargets);

        Assert.Equal(baseUrl, target.BaseUrl);
        Assert.Equal(kept, target.BearerToken == token);
        Assert.Equal(kept, context.Updater.ModelState.IsValid);
    }

    /// <summary>
    /// MD7 (fix verification, round 6): the push checks the server with CrestApps.Core's SftpHostKeys, so the settings
    /// accept every fingerprint form it matches and refuse one it never matches.
    /// </summary>
    [Theory]
    [InlineData("SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU", true)]
    [InlineData("MD5:16:27:ac:a5:76:28:2d:36:63:1b:56:4d:eb:df:a6:48", true)]
    [InlineData("SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU; 1627aca576282d36631b564debdfa648", true)]
    [InlineData("SHA256:not-a-fingerprint", false)]
    public async Task Settings_AcceptExactlyTheFingerprintsThePushMatches(string fingerprint, bool accepted)
    {
        // Arrange
        var settings = new AgentFeedSettings();
        var context = PostedFormUpdateModel.CreateContext(new AgentFeedSettingsViewModel
        {
            SftpEnabled = true,
            SftpHost = "sftp.example.test",
            SftpUsername = "feeds",
            SftpPassword = "secret",
            SftpHostKeyFingerprint = fingerprint,
        });

        // Act
        await Driver(new EphemeralDataProtectionProvider()).UpdateAsync(new SiteSettings(), settings, context);

        // Assert
        Assert.Equal(accepted, !context.Updater.ModelState.Any(entry => entry.Key.EndsWith(nameof(AgentFeedSettingsViewModel.SftpHostKeyFingerprint), StringComparison.Ordinal) && entry.Value.Errors.Count > 0));
    }

    private static AgentFeedSettingsDisplayDriver Driver(IDataProtectionProvider protection)
    {
        var authorization = new Mock<IAuthorizationService>();

        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

        return new AgentFeedSettingsDisplayDriver(
            new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("Test")) } },
            authorization.Object,
            (OpenAIProductFeedService)RuntimeHelpers.GetUninitializedObject(typeof(OpenAIProductFeedService)),
            Mock.Of<IProductFeedService>(),
            new InMemoryDocumentManager<AgentFeedStatusDocument>(),
            protection,
            Mock.Of<IHostEnvironment>(),
            new PassThroughLocalizer());
    }

    private sealed class PassThroughLocalizer : IStringLocalizer<AgentFeedSettingsDisplayDriver>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
