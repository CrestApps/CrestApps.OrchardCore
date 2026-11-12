using System.Net;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Core.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CrestApps.OrchardCore.Tests.AI;

/// <summary>
/// SEC-A6: MCP HTTP connections, A2A agents, web crawlers and AI provider endpoints took any absolute URL, so a tenant
/// could point one at the cloud metadata address or an internal service and read the answer through a tool result or the
/// knowledge base. The endpoints are now refused at save, and the HTTP clients refuse an internal address at connect.
/// </summary>
public sealed class AIEgressTests
{
    [Theory]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://10.0.0.5/")]
    [InlineData("https://[::1]/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("file:///etc/passwd")]
    public void IsAllowedEndpoint_AnInternalAddress_IsRefused(string url)
    {
        Assert.False(AIEgress.IsAllowedEndpoint(url, isDevelopment: false, new AIEgressOptions()));
    }

    [Fact]
    public void IsAllowedEndpoint_APublicHttpsAddress_IsAllowed()
    {
        Assert.True(AIEgress.IsAllowedEndpoint("https://api.openai.com/v1", isDevelopment: false, new AIEgressOptions()));
    }

    [Fact]
    public void IsAllowedEndpoint_WhenTheHostAllowsAPrivateNetwork_AllowsALocalModelHost()
    {
        var options = new AIEgressOptions { AllowPrivateNetwork = true };

        Assert.True(AIEgress.IsAllowedEndpoint("http://10.0.0.5:11434/", isDevelopment: false, options));
        Assert.True(AIEgress.IsAllowedEndpoint("http://localhost:11434/", isDevelopment: false, options));

        // The cloud metadata address is never a model host.
        Assert.False(AIEgress.IsAllowedEndpoint("http://169.254.169.254/", isDevelopment: false, options));
    }

    [Theory]
    [InlineData("10.0.0.5", false, true)]
    [InlineData("10.0.0.5", true, false)]
    [InlineData("169.254.169.254", true, true)]
    [InlineData("8.8.8.8", false, false)]
    public void IsRefused_DependsOnWhetherAPrivateNetworkIsAllowed(string address, bool allowPrivateNetwork, bool refused)
    {
        Assert.Equal(refused, EgressNetwork.IsRefused(IPAddress.Parse(address), allowLoopback: false, allowPrivateNetwork));
    }

    [Theory]
    [InlineData("CrestApps.Mcp")]
    [InlineData("CrestApps.A2A")]
    [InlineData("CrestApps.WebCrawler")]
    public async Task TheGuardedClients_RefuseALoopbackAddressAtConnect(string clientName)
    {
        var services = new ServiceCollection();
        services.AddHttpClient(clientName);
        services.AddSingleton(Options.Create(new AIEgressOptions()));
        services.AddAIEgressGuards();

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(clientName);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("http://127.0.0.1:9/", TestContext.Current.CancellationToken));

        Assert.Contains(EgressNetwork.RefusedMessage, exception.Message, StringComparison.Ordinal);
    }
}
