using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// SEC-P10f: the inbound media downloader followed redirects anywhere, so a media address (or a redirect from it) could
/// reach the site's own network. Its client is egress-guarded: every hop, redirects included, is checked at connect.
/// </summary>
public sealed class MessagingInboundMediaEgressTests
{
    [Fact]
    public async Task TheMediaClient_RefusesTheSitesOwnNetwork_AtConnect()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        new global::CrestApps.OrchardCore.Omnichannel.Messaging.Startup(new ShellConfiguration(new ConfigurationBuilder().Build())).ConfigureServices(services);
        using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(MessagingInboundMediaIngestor.HttpClientName);

        // Act
        var refused = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("https://127.0.0.1:1/media/abc", TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains(EgressNetwork.RefusedMessage, refused.Message, StringComparison.Ordinal);
    }
}
