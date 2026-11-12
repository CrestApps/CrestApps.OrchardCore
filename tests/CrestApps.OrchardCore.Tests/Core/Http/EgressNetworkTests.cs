using System.Net;
using CrestApps.OrchardCore.Core.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CrestApps.OrchardCore.Tests.Core.Http;

/// <summary>
/// The one egress guard webhooks and connectors share. Bug: AI webhooks and the Send Signed Webhook workflow task
/// called any address, including the site's own network (server-side request forgery), while only commerce webhooks
/// were guarded.
/// </summary>
public sealed class EgressNetworkTests
{
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("10.1.2.3", true)]
    [InlineData("172.20.0.1", true)]
    [InlineData("192.168.0.10", true)]
    [InlineData("169.254.169.254", true)]
    [InlineData("100.64.0.1", true)]
    [InlineData("0.0.0.0", true)]
    [InlineData("::1", true)]
    [InlineData("fd00::1", true)]
    [InlineData("fe80::1", true)]
    [InlineData("::ffff:10.0.0.1", true)]
    [InlineData("8.8.8.8", false)]
    [InlineData("172.32.0.1", false)]
    public void AnAddressOfTheSitesOwnNetwork_IsInternal(string address, bool isInternal)
        => Assert.Equal(isInternal, EgressNetwork.IsInternal(IPAddress.Parse(address)));

    [Theory]
    [InlineData("http://localhost/hook", true)]
    [InlineData("http://127.0.0.1:8080/hook", true)]
    [InlineData("https://10.0.0.5/hook", true)]
    [InlineData("https://[::1]/hook", true)]
    [InlineData("https://hooks.example.com/hook", false)]
    public void IsInternalHost_RecognizesAnInternalAddressInTheUrl(string url, bool isInternal)
        => Assert.Equal(isInternal, EgressNetwork.IsInternalHost(new Uri(url)));

    [Theory]
    [InlineData("http://localhost:1/hook")]
    [InlineData("http://127.0.0.1:1/hook")]
    [InlineData("http://10.0.0.5:1/hook")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    public async Task AHostOnTheSitesOwnNetwork_IsRefusedWhenCalled(string url)
    {
        // A host name passes when it's saved; the connection is what refuses it, whatever it resolves to.
        using var client = new HttpClient(EgressNetwork.CreateHandler(allowLoopback: false));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(url, TestContext.Current.CancellationToken));

        Assert.Contains(EgressNetwork.RefusedMessage, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AGuardedNamedClient_RefusesThisMachineOutsideDevelopment()
    {
        // Arrange
        using var services = new ServiceCollection()
            .AddSingleton<IHostEnvironment>(new TestHostEnvironment(Environments.Production))
            .AddEgressGuardedHttpClient("guarded")
            .Services
            .BuildServiceProvider();

        var client = services.GetRequiredService<IHttpClientFactory>().CreateClient("guarded");

        // Act
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("http://127.0.0.1:1/hook", TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains(EgressNetwork.RefusedMessage, exception.Message, StringComparison.Ordinal);
    }

    // DRY-7: the stricter rules the tenant hierarchy guard had are now the one guard's rules.
    [Theory]
    [InlineData("192.0.0.8", true)]
    [InlineData("198.18.0.1", true)]
    [InlineData("198.19.255.254", true)]
    [InlineData("255.255.255.255", true)]
    [InlineData("224.0.0.1", true)]
    [InlineData("64:ff9b::a00:1", true)]
    [InlineData("64:ff9b::7f00:1", true)]
    [InlineData("64:ff9b::808:808", false)]
    [InlineData("168.63.129.16", true)]
    [InlineData("2002:a9fe:a9fe::1", true)]
    [InlineData("2002:a00:1::1", true)]
    [InlineData("2002:808:808::1", false)]
    [InlineData("198.20.0.1", false)]
    [InlineData("192.0.2.1", false)]
    [InlineData("2001:4860:4860::8888", false)]
    public void TheStricterRules_AreTheGuardsRules(string address, bool isInternal)
        => Assert.Equal(isInternal, EgressNetwork.IsInternal(IPAddress.Parse(address)));

    [Fact]
    public void TheGuardedHandler_NeverGoesThroughAProxy()
    {
        // Through a proxy, the connect check would see the proxy's address and never the destination's.
        Assert.False(EgressNetwork.CreateHandler(allowLoopback: false).UseProxy);
        Assert.False(EgressNetwork.CreateHandler(allowLoopback: false, allowPrivateNetwork: true, allowRedirects: true).UseProxy);
    }

    [Fact]
    public void A6to4AddressOfTheMetadataService_IsLinkLocal_AndRefusedEvenOnAPrivateNetwork()
    {
        var address = IPAddress.Parse("2002:a9fe:a9fe::1");

        Assert.True(EgressNetwork.IsLinkLocal(address));
        Assert.True(EgressNetwork.IsRefused(address, allowLoopback: true, allowPrivateNetwork: true));
    }

    [Theory]
    [InlineData("http://example.com/feed", true)]
    [InlineData("https://example.com/feed", true)]
    [InlineData("http://10.0.0.5/feed", false)]
    [InlineData("http://localhost/feed", false)]
    [InlineData("ftp://example.com/feed", false)]
    [InlineData("https://user:secret@example.com/feed", false)]
    public void IsExternalUrl_IsTheNetworkRule_WithoutTheHttpsRule(string url, bool external)
        => Assert.Equal(external, EgressNetwork.IsExternalUrl(url, isDevelopment: false));

    [Fact]
    public async Task AResilientGuardedClient_HasTheStandardResilienceHandler_AndRefusesThisMachine()
    {
        // Arrange
        using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IHostEnvironment>(new TestHostEnvironment(Environments.Production))
            .AddEgressGuardedHttpClient("resilient", client => client.Timeout = TimeSpan.FromSeconds(10), resilience => resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5))
            .Services
            .BuildServiceProvider();

        var handler = services.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler("resilient");
        var client = services.GetRequiredService<IHttpClientFactory>().CreateClient("resilient");

        // Act
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("http://127.0.0.1:1/hook", TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains(HandlerChain(handler), link => link is Microsoft.Extensions.Http.Resilience.ResilienceHandler);
        Assert.Contains(EgressNetwork.RefusedMessage, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AGuardedClientThatAllowsThePrivateNetwork_StillRefusesLinkLocal()
    {
        // Arrange: a self-hosted service on the site's network, opted in by the host.
        using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IHostEnvironment>(new TestHostEnvironment(Environments.Production))
            .AddEgressGuardedHttpClient("private", null, resilience => resilience.Retry.Delay = TimeSpan.Zero, allowPrivateNetwork: _ => true)
            .Services
            .BuildServiceProvider();

        var client = services.GetRequiredService<IHttpClientFactory>().CreateClient("private");

        // Act
        var metadata = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("http://169.254.169.254/latest/meta-data", TestContext.Current.CancellationToken));
        var loopback = await Record.ExceptionAsync(() => client.GetAsync("http://127.0.0.1:1/osrm", TestContext.Current.CancellationToken));

        // Assert: the metadata service is refused by the guard; the private address is tried (and nothing listens).
        Assert.Contains(EgressNetwork.RefusedMessage, metadata.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(EgressNetwork.RefusedMessage, loopback?.Message ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// The handlers a client's requests go through, outermost first.
    /// </summary>
    /// <param name="handler">The outermost handler.</param>
    internal static IEnumerable<HttpMessageHandler> HandlerChain(HttpMessageHandler handler)
    {
        for (var current = handler; current is not null; current = (current as DelegatingHandler)?.InnerHandler)
        {
            yield return current;
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
