using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Core.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Tests.AI;

/// <summary>
/// C13 (review 11): CrestApps.Core's <c>DefaultOAuth2TokenService.AcquireTokenWithMtlsAsync</c> sends the token request
/// through a bare <see cref="HttpClientHandler"/>, so a connection's mTLS token endpoint reached any address and followed
/// redirects. These tests resolve the token service as the AI features register it (the package's registration, then
/// <see cref="AIEgress.AddAIEgressGuards"/>) and send to a token server on this machine.
/// </summary>
public sealed class AIEgressMtlsTokenTests
{
    [Fact]
    public async Task AnMtlsTokenRequest_ToTheSiteOwnNetwork_IsRefused()
    {
        using var server = new TokenServer();
        using var services = CreateServices(allowPrivateNetwork: false);
        using var scope = services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<IOAuth2TokenService>();

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => tokens.AcquireTokenWithMtlsAsync(
            $"http://127.0.0.1:{server.Port}/token",
            "client",
            CreateCertificate(),
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains(EgressNetwork.RefusedMessage, exception.Message, StringComparison.Ordinal);
        Assert.Empty(server.Requests);
    }

    [Fact]
    public async Task AnMtlsTokenRequest_WhereTheHostAllowsAPrivateNetwork_GetsTheToken()
    {
        using var server = new TokenServer();
        using var services = CreateServices(allowPrivateNetwork: true);
        using var scope = services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<IOAuth2TokenService>();

        var token = await tokens.AcquireTokenWithMtlsAsync(
            $"http://127.0.0.1:{server.Port}/token",
            "client",
            CreateCertificate(),
            scopes: "read",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("issued-token", token);
        var request = Assert.Single(server.Requests);
        Assert.Contains("grant_type=client_credentials", request, StringComparison.Ordinal);
        Assert.Contains("client_id=client", request, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnMtlsTokenRequest_RedirectedToAnotherOrigin_IsNotFollowed()
    {
        using var other = new TokenServer();
        using var origin = new TokenServer(_ => $"HTTP/1.1 307 Temporary Redirect\r\nLocation: http://127.0.0.1:{other.Port}/token\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        using var services = CreateServices(allowPrivateNetwork: true);
        using var scope = services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<IOAuth2TokenService>();

        await Assert.ThrowsAsync<HttpRequestException>(() => tokens.AcquireTokenWithMtlsAsync(
            $"http://127.0.0.1:{origin.Port}/token",
            "client",
            CreateCertificate(),
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Empty(other.Requests);
    }

    // The package registers its token service with TryAdd; the AI features then add their guards.
    private static ServiceProvider CreateServices(bool allowPrivateNetwork)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddMemoryCache();
        services.AddSingleton(TimeProvider.System);
        services.Configure<AIEgressOptions>(options => options.AllowPrivateNetwork = allowPrivateNetwork);
        services.TryAddScoped<IOAuth2TokenService, DefaultOAuth2TokenService>();

        foreach (var name in AIEgress.GuardedClientNames)
        {
            services.AddHttpClient(name);
        }

        services.AddAIEgressGuards();

        return services.BuildServiceProvider();
    }

    private static byte[] CreateCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=mtls-client", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));

        return certificate.Export(X509ContentType.Pfx);
    }

    // A token endpoint on this machine that keeps each request it receives.
    private sealed class TokenServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly ConcurrentQueue<string> _requests = new();

        public TokenServer(Func<string, string> respond = null)
        {
            const string Token = "{\"access_token\":\"issued-token\",\"expires_in\":3600}";

            respond ??= _ => $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {Token.Length}\r\nConnection: close\r\n\r\n{Token}";
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
                    var received = new StringBuilder();
                    var buffer = new byte[4096];

                    try
                    {
                        // The head, then the form body the Content-Length announces.
                        while (true)
                        {
                            var read = await stream.ReadAsync(buffer, _stop.Token);

                            if (read == 0)
                            {
                                break;
                            }

                            received.Append(Encoding.ASCII.GetString(buffer, 0, read));
                            var text = received.ToString();
                            var headEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);

                            if (headEnd >= 0 && text.Length - headEnd - 4 >= ContentLength(text[..headEnd]))
                            {
                                break;
                            }
                        }

                        _requests.Enqueue(received.ToString());

                        var response = Encoding.ASCII.GetBytes(respond(received.ToString()));
                        await stream.WriteAsync(response, _stop.Token);
                    }
                    catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException)
                    {
                        return;
                    }
                }
            }
        }

        private static int ContentLength(string head)
        {
            foreach (var line in head.Split("\r\n"))
            {
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase) && int.TryParse(line["Content-Length:".Length..].Trim(), out var length))
                {
                    return length;
                }
            }

            return 0;
        }
    }
}
