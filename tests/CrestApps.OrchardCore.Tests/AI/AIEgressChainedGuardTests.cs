using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Core.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.AI;

/// <summary>
/// AI Mild: in a tenant hierarchy, the hierarchy's guard puts a connect check on every client; the AI guard replaced the
/// whole primary handler, which dropped it. The AI guard now chains to a connect check the client already has.
/// </summary>
public sealed class AIEgressChainedGuardTests
{
    [Fact]
    public void AIGuard_KeepsTheConnectCheckAnotherGuardPutOnTheClient()
    {
        Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>> existing = (_, _) => ValueTask.FromResult<Stream>(new MemoryStream());

        var services = new ServiceCollection();
        services.AddOptions();
        services.AddAIEgressGuards();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>().Get(AIEgress.GuardedClientNames[0]);
        var builder = new TestBuilder(provider) { PrimaryHandler = new SocketsHttpHandler { ConnectCallback = existing } };

        foreach (var action in options.HttpMessageHandlerBuilderActions)
        {
            action(builder);
        }

        var handler = Assert.IsType<SocketsHttpHandler>(builder.PrimaryHandler);

        Assert.NotNull(handler.ConnectCallback);
        Assert.NotSame(existing, handler.ConnectCallback);
    }

    [Fact]
    public async Task ChainedConnect_CallsTheOtherGuard_AndRefusesTheAddressItReallyConnectedTo()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var calls = 0;

        // The other guard connects somewhere else than the name the request asked for, as a name that answers
        // differently on the second lookup would make it.
        async ValueTask<Stream> Existing(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
        {
            calls++;
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(IPAddress.Loopback, port, cancellationToken);

            return new NetworkStream(socket, ownsSocket: true);
        }

        using var handler = EgressNetwork.CreateHandler(allowLoopback: false, allowPrivateNetwork: false, allowRedirects: false, proxy: null, Existing);
        using var client = new HttpClient(handler);

        var failure = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("http://93.184.216.34/", TestContext.Current.CancellationToken));

        Assert.Equal(1, calls);
        Assert.Contains("93.184.216.34", failure.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChainedConnect_RefusesARefusedHostBeforeTheOtherGuardConnects()
    {
        var calls = 0;

        ValueTask<Stream> Existing(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
        {
            calls++;

            return ValueTask.FromResult<Stream>(new MemoryStream());
        }

        using var handler = EgressNetwork.CreateHandler(allowLoopback: false, allowPrivateNetwork: true, allowRedirects: false, proxy: null, Existing);
        using var client = new HttpClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("http://169.254.169.254/latest/meta-data/", TestContext.Current.CancellationToken));

        Assert.Equal(0, calls);
    }

    private sealed class TestBuilder : HttpMessageHandlerBuilder
    {
        public TestBuilder(IServiceProvider services)
        {
            Services = services;
        }

        public override string Name { get; set; }

        public override HttpMessageHandler PrimaryHandler { get; set; }

        public override IList<DelegatingHandler> AdditionalHandlers { get; } = [];

        public override IServiceProvider Services { get; }

        public override HttpMessageHandler Build()
            => CreateHandlerPipeline(PrimaryHandler, AdditionalHandlers);
    }
}
