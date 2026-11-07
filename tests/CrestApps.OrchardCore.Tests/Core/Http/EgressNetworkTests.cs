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
