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
