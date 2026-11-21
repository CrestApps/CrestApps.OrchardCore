using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.AI;

/// <summary>
/// B5: the AI building blocks' guarded clients follow redirects, and .NET keeps custom headers (an MCP connection's API
/// key header) on a redirect to another origin. These tests send through the <c>CrestApps.Mcp</c> client as
/// <see cref="AIEgress.AddAIEgressGuards"/> configures it, to two servers on this machine on different ports, which are
/// different origins.
/// </summary>
public sealed class AIEgressRedirectCredentialTests
{
    private const string ApiKeyHeader = "X-Api-Key";

    [Fact]
    public async Task ARedirectToAnotherOrigin_CarriesNoCredential()
    {
        using var other = new RecordingServer(_ => Ok());
        using var origin = new RecordingServer(_ => $"HTTP/1.1 302 Found\r\nLocation: http://127.0.0.1:{other.Port}/landing\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        using var services = CreateServices();
        using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient("CrestApps.Mcp");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{origin.Port}/mcp");
        request.Headers.Add(ApiKeyHeader, "secret-key");
        request.Headers.Add("Cookie", "session=secret");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        // The redirect is still followed (crawled sites move), but nothing of the credential reaches the other origin.
        var received = Assert.Single(other.Requests);
        Assert.DoesNotContain("secret", received, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ARedirectWithABodyToAnotherOrigin_IsNotFollowed()
    {
        using var other = new RecordingServer(_ => Ok());
        using var origin = new RecordingServer(_ => $"HTTP/1.1 307 Temporary Redirect\r\nLocation: http://127.0.0.1:{other.Port}/token\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        using var services = CreateServices();
        using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient("DefaultOAuth2TokenService");

        using var response = await client.PostAsync(
            $"http://127.0.0.1:{origin.Port}/token",
            new FormUrlEncodedContent([new("client_secret", "secret-value")]),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.Empty(other.Requests);
    }

    [Fact]
    public async Task ARedirectOnTheSameOrigin_KeepsTheConfiguredHeader()
    {
        using var server = new RecordingServer(path => path == "/mcp"
            ? "HTTP/1.1 301 Moved Permanently\r\nLocation: /mcp/\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
            : Ok());
        using var services = CreateServices();
        using var client = services.GetRequiredService<IHttpClientFactory>().CreateClient("CrestApps.Mcp");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{server.Port}/mcp");
        request.Headers.Add(ApiKeyHeader, "secret-key");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, server.Requests.Length);
        Assert.All(server.Requests, received => Assert.Contains("secret-key", received, StringComparison.Ordinal));
    }

    private static string Ok() => "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok";

    // The guarded clients as the AI features register them, on a host that allows a private network (this machine).
    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.Configure<AIEgressOptions>(options => options.AllowPrivateNetwork = true);

        foreach (var name in AIEgress.GuardedClientNames)
        {
            services.AddHttpClient(name);
        }

        services.AddAIEgressGuards();

        return services.BuildServiceProvider();
    }

    // Answers each connection on this machine with the response the request's path picks, and keeps each request's head.
    private sealed class RecordingServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly ConcurrentQueue<string> _requests = new();

        public RecordingServer(Func<string, string> respond)
        {
            _listener.Start();
            _ = Task.Run(() => ServeAsync(respond), CancellationToken.None);
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public string[] Requests => _requests.ToArray();

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Stop();
            _stop.Dispose();
        }

        private async Task ServeAsync(Func<string, string> respond)
        {
            while (!_stop.IsCancellationRequested)
            {
                TcpClient client;

                try
                {
                    client = await _listener.AcceptTcpClientAsync(_stop.Token);
                }
                catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
                {
                    return;
                }

                using (client)
                {
                    var stream = client.GetStream();
                    var request = new StringBuilder();
                    var buffer = new byte[1024];

                    while (!request.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
                    {
                        var read = await stream.ReadAsync(buffer, _stop.Token);

                        if (read == 0)
                        {
                            break;
                        }

                        request.Append(Encoding.ASCII.GetString(buffer, 0, read));
                    }

                    _requests.Enqueue(request.ToString());
                    var path = request.ToString().Split(' ', 3) is { Length: > 1 } parts ? parts[1] : "/";

                    await stream.WriteAsync(Encoding.UTF8.GetBytes(respond(path)), _stop.Token);
                }
            }
        }
    }
}
