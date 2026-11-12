using System.Net;
using System.Text;
using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Geo.Osrm;
using CrestApps.OrchardCore.Geo.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Geo.Providers;

/// <summary>
/// SEC-P10b: any URL was accepted as the OSRM server, the client reached the site's own network, and a refusal passed on
/// whatever the configured host answered. The client is egress-guarded with a host-level opt-in for a self-hosted server
/// on the private network (never the link-local metadata service), and refusals are described in our words.
/// </summary>
public sealed class OsrmEgressTests
{
    [Fact]
    public async Task TheClient_RefusesTheSitesOwnNetwork_WithoutTheHostsOptIn()
    {
        using var provider = Services(allowPrivateNetwork: null);
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(OsrmProvider.HttpClientName);

        var refused = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("http://127.0.0.1:1/route/v1/driving/0,0;1,1", TestContext.Current.CancellationToken));

        Assert.Contains(EgressNetwork.RefusedMessage, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithTheHostsOptIn_APrivateServerIsCalled_ButNeverTheMetadataService()
    {
        using var provider = Services(allowPrivateNetwork: "true");
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(OsrmProvider.HttpClientName);

        var metadata = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("http://169.254.169.254/latest/meta-data", TestContext.Current.CancellationToken));
        var loopback = await Record.ExceptionAsync(() => client.GetAsync("http://127.0.0.1:1/route/v1/driving/0,0;1,1", TestContext.Current.CancellationToken));

        Assert.Contains(EgressNetwork.RefusedMessage, metadata.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(EgressNetwork.RefusedMessage, loopback?.Message ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARefusal_DoesNotPassOnTheServersMessage()
    {
        // Arrange
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(new AnswerHandler("""{ "code": "NoRoute", "message": "db at 10.0.0.7 said: no such table" }""")));
        var site = new SiteSettings();
        site.Put(new OsrmSettings { BaseUrl = "https://osrm.example.com" });
        var siteService = new Mock<ISiteService>();
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site);
        var osrm = new OsrmProvider(factory.Object, siteService.Object);

        // Act
        var error = await Assert.ThrowsAsync<GeoProviderException>(() => osrm.GetRouteAsync([GeoProviderContract.CityHall, GeoProviderContract.Cambridge], TestContext.Current.CancellationToken));

        // Assert
        Assert.DoesNotContain("10.0.0.7", error.Message, StringComparison.Ordinal);
        Assert.Contains("NoRoute", error.Message, StringComparison.Ordinal);
    }

    private static ServiceProvider Services(string allowPrivateNetwork)
    {
        var values = new Dictionary<string, string>();

        if (allowPrivateNetwork is not null)
        {
            values["CrestApps:Geo:Osrm:AllowPrivateNetwork"] = allowPrivateNetwork;
        }

        var services = new ServiceCollection();
        services.AddLogging();
        new global::CrestApps.OrchardCore.Geo.Osrm.Startup(new ShellConfiguration(new ConfigurationBuilder().AddInMemoryCollection(values).Build())).ConfigureServices(services);

        return services.BuildServiceProvider();
    }


    private sealed class AnswerHandler : HttpMessageHandler
    {
        private readonly string _body;

        public AnswerHandler(string body) => _body = body;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(_body, Encoding.UTF8, "application/json") });
    }
}
