using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.AgenticCommerce.Feeds;
using Microsoft.AspNetCore.DataProtection;
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
}
