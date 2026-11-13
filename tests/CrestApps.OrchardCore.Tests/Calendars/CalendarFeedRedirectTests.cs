using System.Net;
using System.Net.Sockets;
using System.Text;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Core.External;
using CrestApps.OrchardCore.Calendars.Drivers;
using CrestApps.OrchardCore.Calendars.External;
using CrestApps.OrchardCore.Calendars.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CrestApps.OrchardCore.Tests.Calendars;

/// <summary>
/// SEC-P5 (R16): calendar feeds commonly redirect (an old Google Calendar address, an http:// feed moved to https), and
/// the guarded client followed none, so every sync of such a feed failed after the upgrade. Redirects are followed now,
/// each hop checked at connect, and a public http:// feed can be saved again.
/// </summary>
public sealed class CalendarFeedRedirectTests
{
    private const string Feed = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nBEGIN:VEVENT\r\nUID:one\r\nDTSTART:20261201T090000Z\r\nDTEND:20261201T100000Z\r\nSUMMARY:Stand-up\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";

    [Fact]
    public async Task AFeedThatRedirects_IsFollowed_AndRead()
    {
        // Arrange: the feed answers its old address with a redirect to the new one.
        using var server = new TinyHttpServer(path => path == "/old.ics"
            ? "HTTP/1.1 301 Moved Permanently\r\nLocation: /new.ics\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
            : $"HTTP/1.1 200 OK\r\nContent-Type: text/calendar\r\nContent-Length: {Encoding.UTF8.GetByteCount(Feed)}\r\nConnection: close\r\n\r\n{Feed}");
        using var services = CreateServices();

        // Act
        var events = await Provider(services).PullAsync(
            new Calendar { TimeZoneId = "UTC" },
            new CalendarSubscription { Url = $"http://127.0.0.1:{server.Port}/old.ics" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(events);
    }

    [Fact]
    public async Task ARedirectToTheMetadataAddress_IsRefusedAtConnect()
    {
        // Arrange
        using var server = new TinyHttpServer(_ => "HTTP/1.1 302 Found\r\nLocation: http://169.254.169.254/latest/meta-data\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        using var services = CreateServices();

        // Act
        var refused = await Assert.ThrowsAsync<HttpRequestException>(() => Provider(services).PullAsync(
            new Calendar { TimeZoneId = "UTC" },
            new CalendarSubscription { Url = $"http://127.0.0.1:{server.Port}/feed.ics" },
            TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains(global::CrestApps.OrchardCore.Core.Http.EgressNetwork.RefusedMessage, refused.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://calendar.example.com/feed.ics", true)]
    [InlineData("http://10.0.0.5/feed.ics", false)]
    public void TheEditor_AcceptsAPublicHttpFeed_AsTheSyncDoes(string url, bool allowed)
        => Assert.Equal(allowed, CalendarSubscriptionDisplayDriver.IsAllowedAddress(url, isDevelopment: false));

    // The subscription client as the feature registers it, on a development host, where this machine may be reached.
    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new DevelopmentEnvironment());
        new ExternalStartup().ConfigureServices(services);

        return services.BuildServiceProvider();
    }

    private static ICalendarUrlProvider Provider(IServiceProvider services)
        => new(services.GetRequiredService<IHttpClientFactory>());

    private sealed class DevelopmentEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    // Answers each connection on this machine with the response the request's path picks.
    private sealed class TinyHttpServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();

        public TinyHttpServer(Func<string, string> respond)
        {
            _listener.Start();
            _ = Task.Run(() => ServeAsync(respond), CancellationToken.None);
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

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

                    var path = request.ToString().Split(' ', 3) is { Length: > 1 } parts ? parts[1] : "/";
                    var response = Encoding.UTF8.GetBytes(respond(path));

                    await stream.WriteAsync(response, _stop.Token);
                }
            }
        }
    }
}
