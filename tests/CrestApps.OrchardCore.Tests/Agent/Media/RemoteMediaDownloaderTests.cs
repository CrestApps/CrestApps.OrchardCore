using System.Net;
using System.Text;
using CrestApps.OrchardCore.AI.Agent;
using CrestApps.OrchardCore.AI.Agent.Media;
using CrestApps.OrchardCore.Core.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Agent.Media;

/// <summary>
/// The downloader of the media import tool fetches an address a model chose, so it must never read the site's own
/// network: it refuses an internal address before it sends anything, checks every address a host resolves to, checks
/// every redirect the same way, and stops at the size limit.
/// </summary>
public sealed class RemoteMediaDownloaderTests
{
    [Theory]
    [InlineData("https://example.com/press/logo.png")]
    [InlineData("http://example.com:8080/a/b/photo.jpg?size=2")]
    [InlineData("https://203.0.113.10/logo.png")]
    public void CheckUrl_AcceptsAPublicHttpOrHttpsAddress(string url)
    {
        // Act
        var refusal = RemoteMediaDownloader.CheckUrl(url, out var uri);

        // Assert
        Assert.Null(refusal);
        Assert.Equal(new Uri(url), uri);
    }

    [Theory]
    [InlineData("")]
    [InlineData("logo.png")]
    [InlineData("/media/logo.png")]
    [InlineData("ftp://example.com/logo.png")]
    [InlineData("file:///etc/passwd")]
    [InlineData("//example.com/logo.png")]
    [InlineData("gopher://example.com/1")]
    [InlineData("https://user:secret@example.com/logo.png")]
    [InlineData("http://localhost/logo.png")]
    [InlineData("http://127.0.0.1/logo.png")]
    [InlineData("http://127.1/logo.png")]
    [InlineData("http://2130706433/logo.png")]
    [InlineData("http://0x7f000001/logo.png")]
    [InlineData("http://0.0.0.0/logo.png")]
    [InlineData("http://10.0.0.5/logo.png")]
    [InlineData("http://172.16.4.4/logo.png")]
    [InlineData("http://192.168.1.1/logo.png")]
    [InlineData("http://100.64.0.1/logo.png")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://[::1]/logo.png")]
    [InlineData("http://[::ffff:10.0.0.1]/logo.png")]
    [InlineData("http://[fe80::1]/logo.png")]
    [InlineData("http://[fd00::1]/logo.png")]
    public void CheckUrl_RefusesWhatIsNotAPublicHttpOrHttpsAddress(string url)
    {
        // Act
        var refusal = RemoteMediaDownloader.CheckUrl(url, out _);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(refusal));
    }

    [Fact]
    public async Task DownloadAsync_ReturnsTheFileInATemporaryFileThatIsDeletedOnDispose()
    {
        // Arrange
        var harness = new MediaToolHarness
        {
            Respond = _ => MediaToolHarness.CreateResponse("the-file", "image/png"),
        };

        var downloader = harness.CreateDownloader();

        // Act
        var download = await downloader.DownloadAsync(new Uri("https://example.com/logo.png"), 1000, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(download.Error);
        Assert.Equal(8, download.Length);
        Assert.Equal("image/png", download.ContentType);
        Assert.Equal(0, download.Content.Position);

        using (var reader = new StreamReader(download.Content, Encoding.UTF8, leaveOpen: true))
        {
            Assert.Equal("the-file", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
        }

        var tempFile = Assert.Single(harness.TempFiles);

        Assert.True(File.Exists(tempFile));

        await download.DisposeAsync();

        Assert.False(File.Exists(tempFile));
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.5")]
    [InlineData("169.254.169.254")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    public async Task DownloadAsync_WhenAnyAddressOfTheHostIsInternal_SendsNoRequest(string internalAddress)
    {
        // Arrange
        var harness = new MediaToolHarness
        {
            // A public address first: every address is checked, not only the first one.
            ResolveHost = _ => [MediaToolHarness.PublicAddress, IPAddress.Parse(internalAddress)],
        };

        var downloader = harness.CreateDownloader();

        // Act
        await using var download = await downloader.DownloadAsync(new Uri("https://rebind.example.com/logo.png"), 1000, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("on the site's own network", download.Error, StringComparison.Ordinal);
        Assert.Null(download.Content);
        Assert.Empty(harness.Requests);
        Assert.Empty(harness.TempFiles);
    }

    [Fact]
    public async Task DownloadAsync_WhenTheHostCannotBeResolved_SaysSo()
    {
        // Arrange
        var harness = new MediaToolHarness
        {
            ResolveHost = _ => [],
        };

        var downloader = harness.CreateDownloader();

        // Act
        await using var download = await downloader.DownloadAsync(new Uri("https://nowhere.example.com/logo.png"), 1000, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("could not be found", download.Error, StringComparison.Ordinal);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task DownloadAsync_FollowsARedirectToAPublicAddress()
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Respond = request => request.RequestUri.AbsolutePath == "/logo.png"
            ? MediaToolHarness.CreateRedirect("https://cdn.example.net/assets/logo.png")
            : MediaToolHarness.CreateResponse("the-file", "image/png");

        var downloader = harness.CreateDownloader();

        // Act
        await using var download = await downloader.DownloadAsync(new Uri("http://example.com/logo.png"), 1000, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(download.Error);
        Assert.Equal(new Uri("https://cdn.example.net/assets/logo.png"), download.FinalUri);
        Assert.Equal(
            new[] { "http://example.com/logo.png", "https://cdn.example.net/assets/logo.png" },
            harness.Requests.Select(uri => uri.AbsoluteUri));
    }

    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data/iam/")]
    [InlineData("http://127.0.0.1:5000/admin")]
    [InlineData("http://[::1]/admin")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.com/logo.png")]
    [InlineData("https://user:secret@example.com/logo.png")]
    public async Task DownloadAsync_DoesNotFollowARedirectToAnAddressItWouldRefuse(string location)
    {
        // Arrange
        var harness = new MediaToolHarness
        {
            Respond = _ => MediaToolHarness.CreateRedirect(location),
        };

        var downloader = harness.CreateDownloader();

        // Act
        await using var download = await downloader.DownloadAsync(new Uri("https://example.com/logo.png"), 1000, TestContext.Current.CancellationToken);

        // Assert
        Assert.StartsWith("The address redirects to", download.Error, StringComparison.Ordinal);
        Assert.Null(download.Content);
        Assert.Equal("https://example.com/logo.png", Assert.Single(harness.Requests).AbsoluteUri);
    }

    [Fact]
    public async Task DownloadAsync_DoesNotFollowARedirectToAHostThatResolvesToAnInternalAddress()
    {
        // Arrange
        var harness = new MediaToolHarness
        {
            Respond = _ => MediaToolHarness.CreateRedirect("https://intranet.example.com/logo.png"),
            ResolveHost = host => host == "intranet.example.com"
                ? [IPAddress.Parse("192.168.10.20")]
                : [MediaToolHarness.PublicAddress],
        };

        var downloader = harness.CreateDownloader();

        // Act
        await using var download = await downloader.DownloadAsync(new Uri("https://example.com/logo.png"), 1000, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("on the site's own network", download.Error, StringComparison.Ordinal);
        Assert.Single(harness.Requests);
    }

    [Fact]
    public async Task DownloadAsync_StopsAfterTheRedirectLimit()
    {
        // Arrange
        var hop = 0;
        var harness = new MediaToolHarness
        {
            Respond = _ => MediaToolHarness.CreateRedirect($"https://example.com/hop-{++hop}.png"),
        };

        var downloader = harness.CreateDownloader();

        // Act
        await using var download = await downloader.DownloadAsync(new Uri("https://example.com/logo.png"), 1000, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains($"redirects more than {RemoteMediaDownloader.MaxRedirects} times", download.Error, StringComparison.Ordinal);
        Assert.Equal(RemoteMediaDownloader.MaxRedirects + 1, harness.Requests.Count);
    }

    [Fact]
    public async Task DownloadAsync_WhenTheAnnouncedSizeIsOverTheLimit_ReadsNothing()
    {
        // Arrange
        var harness = new MediaToolHarness
        {
            Respond = _ => MediaToolHarness.CreateResponse(new string('a', 2000), "image/png"),
        };

        var downloader = harness.CreateDownloader();

        // Act
        await using var download = await downloader.DownloadAsync(new Uri("https://example.com/logo.png"), 1000, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("larger than the site's upload limit of 1000 bytes", download.Error, StringComparison.Ordinal);
        Assert.Empty(harness.TempFiles);
    }

    [Fact]
    public async Task DownloadAsync_WhenTheBodyIsOverTheLimitWithoutAnAnnouncedSize_StopsAndDeletesTheTemporaryFile()
    {
        // Arrange
        var harness = new MediaToolHarness
        {
            Respond = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new UnannouncedContent(new byte[5000]),
            },
        };

        var downloader = harness.CreateDownloader();

        // Act
        await using var download = await downloader.DownloadAsync(new Uri("https://example.com/logo.png"), 1000, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("larger than the site's upload limit of 1000 bytes", download.Error, StringComparison.Ordinal);
        Assert.Null(download.Content);
        Assert.False(File.Exists(Assert.Single(harness.TempFiles)));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "status 404")]
    [InlineData(HttpStatusCode.Forbidden, "status 403")]
    [InlineData(HttpStatusCode.InternalServerError, "status 500")]
    public async Task DownloadAsync_WhenTheAddressAnswersWithAnError_SaysTheStatus(HttpStatusCode statusCode, string expected)
    {
        // Arrange
        var harness = new MediaToolHarness
        {
            Respond = _ => new HttpResponseMessage(statusCode),
        };

        var downloader = harness.CreateDownloader();

        // Act
        await using var download = await downloader.DownloadAsync(new Uri("https://example.com/logo.png"), 1000, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(expected, download.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DownloadAsync_WhenTheEgressGuardRefusesTheConnection_SaysTheAddressIsInternal()
    {
        // Arrange
        var harness = new MediaToolHarness
        {
            // What the guarded handler throws when a host that resolved to a public address connects to an internal one.
            Respond = _ => throw new HttpRequestException(
                "Connection failed (rebind.example.com:443)",
                new HttpRequestException($"The host 'rebind.example.com' {EgressNetwork.RefusedMessage}.")),
        };

        var downloader = harness.CreateDownloader();

        // Act
        await using var download = await downloader.DownloadAsync(new Uri("https://rebind.example.com/logo.png"), 1000, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("on the site's own network", download.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DownloadAsync_WhenTheAddressCannotBeReached_SaysSoWithoutTheDetails()
    {
        // Arrange
        var harness = new MediaToolHarness
        {
            Respond = _ => throw new HttpRequestException("No connection could be made because the target machine actively refused it. (10.9.8.7:443)"),
        };

        var downloader = harness.CreateDownloader();

        // Act
        await using var download = await downloader.DownloadAsync(new Uri("https://example.com/logo.png"), 1000, TestContext.Current.CancellationToken);

        // Assert
        Assert.StartsWith("The address could not be reached", download.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("10.9.8.7", download.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheRegisteredClient_FollowsNoRedirectAndRefusesToConnectToThisMachine()
    {
        // Arrange
        var services = new ServiceCollection();

        new MediaToolsStartup(new PassThroughLocalizer()).ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        var handler = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(RemoteMediaDownloader.HttpClientName);

        while (handler is DelegatingHandler { InnerHandler: not null } delegating)
        {
            handler = delegating.InnerHandler;
        }

        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(RemoteMediaDownloader.HttpClientName);

        // Act
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAsync("http://127.0.0.1:9/logo.png", TestContext.Current.CancellationToken));

        // Assert
        var socketsHandler = Assert.IsType<SocketsHttpHandler>(handler);

        Assert.False(socketsHandler.AllowAutoRedirect);
        Assert.NotNull(socketsHandler.ConnectCallback);
        Assert.Contains(EgressNetwork.RefusedMessage, $"{exception.Message} {exception.InnerException?.Message}", StringComparison.Ordinal);
    }

    private sealed class PassThroughLocalizer : IStringLocalizer<MediaToolsStartup>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }

    /// <summary>
    /// A response body that does not say how long it is, as a chunked response does.
    /// </summary>
    private sealed class UnannouncedContent : HttpContent
    {
        private readonly byte[] _content;

        public UnannouncedContent(byte[] content)
        {
            _content = content;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
            => stream.WriteAsync(_content, 0, _content.Length);

        protected override Task<Stream> CreateContentReadStreamAsync()
            => Task.FromResult<Stream>(new MemoryStream(_content, writable: false));

        protected override bool TryComputeLength(out long length)
        {
            length = 0;

            return false;
        }
    }
}
