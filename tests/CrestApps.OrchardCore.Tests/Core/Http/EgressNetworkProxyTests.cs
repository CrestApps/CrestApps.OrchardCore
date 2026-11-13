using System.Net;
using System.Net.Sockets;
using System.Text;
using CrestApps.OrchardCore.Core.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.Tests.Core.Http;

/// <summary>
/// DRY-7 / SEC-A20 j, second review: the guarded handler never used a proxy, so on a host whose only way out is a proxy
/// every guarded client stopped connecting. A host can now let the guarded clients go through the system proxy; the
/// proxy is connected to whatever its address, and each destination is still checked before the proxy is asked to reach
/// it, on every request.
/// </summary>
public sealed class EgressNetworkProxyTests
{
    [Fact]
    public async Task ThroughAProxy_APublicDestinationIsSentToTheProxy_AndAnInternalOneIsRefused()
    {
        // Arrange: a proxy on this machine, which the guard would refuse as a destination.
        await using var proxy = await FakeProxy.StartAsync();
        using var client = new HttpClient(EgressNetwork.CreateHandler(allowLoopback: false, allowPrivateNetwork: false, allowRedirects: false, new WebProxy(proxy.Address)));

        // Act: the second request comes after a kept-alive first one, so a reused proxy connection would carry it unchecked.
        using var allowed = await client.GetAsync("http://93.184.215.14/feed", TestContext.Current.CancellationToken);
        var refused = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("http://10.0.0.5/admin", TestContext.Current.CancellationToken));
        var metadata = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("http://169.254.169.254/latest/meta-data", TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Contains(EgressNetwork.RefusedMessage, refused.Message, StringComparison.Ordinal);
        Assert.Contains(EgressNetwork.RefusedMessage, metadata.Message, StringComparison.Ordinal);
        Assert.Equal("GET http://93.184.215.14/feed HTTP/1.1", Assert.Single(proxy.RequestLines));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("false", false)]
    [InlineData("true", true)]
    public void TheSystemProxy_IsUsedOnlyWhenTheHostSetsIt(string value, bool used)
    {
        // Arrange
        var values = new Dictionary<string, string> { [EgressNetwork.UseSystemProxyKey] = value };
        using var services = new ServiceCollection()
            .AddSingleton<IShellConfiguration>(new ShellConfiguration(new ConfigurationBuilder().AddInMemoryCollection(values).Build()))
            .BuildServiceProvider();

        // Act
        var proxy = EgressNetwork.GetProxy(services);

        // Assert
        Assert.Equal(used, proxy is not null);
        Assert.Equal(used, EgressNetwork.CreateHandler(allowLoopback: false, allowPrivateNetwork: false, allowRedirects: false, proxy).UseProxy);
    }

    // An HTTP proxy that answers every request itself, keeps connections open, and records the request lines it got.
    private sealed class FakeProxy : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stopping = new();
        private readonly List<string> _requestLines = [];
        private Task _accepting;

        public Uri Address => new($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/");

        public IReadOnlyList<string> RequestLines
        {
            get
            {
                lock (_requestLines)
                {
                    return [.. _requestLines];
                }
            }
        }

        public static Task<FakeProxy> StartAsync()
        {
            var proxy = new FakeProxy();

            proxy._listener.Start();
            proxy._accepting = proxy.AcceptAsync();

            return Task.FromResult(proxy);
        }

        public async ValueTask DisposeAsync()
        {
            await _stopping.CancelAsync();
            _listener.Dispose();

            try
            {
                await _accepting;
            }
            catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException)
            {
                // Stopped.
            }

            _stopping.Dispose();
        }

        private async Task AcceptAsync()
        {
            while (!_stopping.IsCancellationRequested)
            {
                var connection = await _listener.AcceptTcpClientAsync(_stopping.Token);

                _ = ServeAsync(connection);
            }
        }

        private async Task ServeAsync(TcpClient connection)
        {
            using (connection)
            {
                var stream = connection.GetStream();
                var buffer = new byte[8192];
                var received = new StringBuilder();

                try
                {
                    while (!_stopping.IsCancellationRequested)
                    {
                        var read = await stream.ReadAsync(buffer, _stopping.Token);

                        if (read == 0)
                        {
                            return;
                        }

                        received.Append(Encoding.ASCII.GetString(buffer, 0, read));

                        var text = received.ToString();
                        var end = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);

                        if (end < 0)
                        {
                            continue;
                        }

                        lock (_requestLines)
                        {
                            _requestLines.Add(text[..text.IndexOf("\r\n", StringComparison.Ordinal)]);
                        }

                        received.Clear().Append(text[(end + 4)..]);
                        await stream.WriteAsync("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok"u8.ToArray(), _stopping.Token);
                    }
                }
                catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException)
                {
                    // The client or the test closed the connection.
                }
            }
        }
    }
}
