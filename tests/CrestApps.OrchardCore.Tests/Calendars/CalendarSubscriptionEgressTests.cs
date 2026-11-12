using System.Net;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Core.External;
using CrestApps.OrchardCore.Calendars.Drivers;
using CrestApps.OrchardCore.Calendars.External;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Core.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Calendars;

/// <summary>
/// SEC-P5: a calendar manager subscribes a calendar to an iCalendar address the server then fetches. Bug: the address was
/// checked once against a private copy of the internal-range rules, and the fetch resolved it again on a default client
/// that followed redirects, so a redirect or a rebinding host reached the site's own network; and the body was buffered
/// whole, so a chunked multi-gigabyte answer exhausted memory.
/// </summary>
public sealed class CalendarSubscriptionEgressTests
{
    [Fact]
    public async Task TheSubscriptionClient_RefusesAHostOnTheSitesOwnNetwork_AtConnect()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        new ExternalStartup().ConfigureServices(services);
        using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(ICalendarUrlProvider));

        // Act
        var refused = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("https://127.0.0.1:9/feed.ics", TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains(EgressNetwork.RefusedMessage, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AChunkedBodyOverTheLimit_IsAbortedWithoutReadingItAll()
    {
        // Arrange: no declared length, and far more than the limit.
        var body = new CountingStream(50L * 1024 * 1024);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(new BodyHandler(body)));
        var provider = new ICalendarUrlProvider(factory.Object);

        // Act
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.PullAsync(
            new Calendar { TimeZoneId = "UTC" },
            new CalendarSubscription { Url = "https://93.184.215.14/feed.ics" },
            TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains("10 MB", refused.Message, StringComparison.Ordinal);
        Assert.True(body.BytesRead <= (10 * 1024 * 1024) + (128 * 1024), $"Read {body.BytesRead} bytes.");
    }

    [Fact]
    public async Task ReadBoundedAsync_StopsAtTheLimit_AndReturnsNull()
    {
        // Arrange
        var body = new CountingStream(5 * 1024 * 1024);
        using var content = new StreamContent(body);

        // Act
        var bytes = await content.ReadBoundedAsync(1024 * 1024, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(bytes);
        Assert.True(body.BytesRead <= (1024 * 1024) + (128 * 1024), $"Read {body.BytesRead} bytes.");
    }

    [Fact]
    public async Task ReadBoundedAsync_ReturnsABodyWithinTheLimit()
    {
        using var content = new ByteArrayContent([1, 2, 3]);

        var bytes = await content.ReadBoundedAsync(3, TestContext.Current.CancellationToken);

        Assert.Equal([1, 2, 3], bytes);
    }

    [Theory]
    [InlineData("https://calendar.example.com/feed.ics", true)]
    [InlineData("webcal://calendar.example.com/feed.ics", true)]
    [InlineData("https://127.0.0.1/feed.ics", false)]
    [InlineData("https://localhost/feed.ics", false)]
    [InlineData("https://10.0.0.5/feed.ics", false)]
    [InlineData("webcal://169.254.169.254/latest/meta-data", false)]
    [InlineData("https://[::1]/feed.ics", false)]
    [InlineData("https://user:secret@calendar.example.com/feed.ics", false)]
    [InlineData("ftp://calendar.example.com/feed.ics", false)]
    public void TheEditor_RefusesAnAddressOnTheSitesOwnNetwork(string url, bool allowed)
        => Assert.Equal(allowed, CalendarSubscriptionDisplayDriver.IsAllowedAddress(url, isDevelopment: false));

    // A body of the given length that counts what is read from it.
    private sealed class CountingStream : Stream
    {
        private readonly long _length;

        public CountingStream(long length) => _length = length;

        public long BytesRead { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => BytesRead; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = (int)Math.Min(count, _length - BytesRead);

            buffer.AsSpan(offset, read).Fill((byte)'A');
            BytesRead += read;

            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class BodyHandler : HttpMessageHandler
    {
        private readonly Stream _body;

        public BodyHandler(Stream body) => _body = body;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = new StreamContent(_body);

            // Chunked: no length is declared.
            content.Headers.ContentLength = null;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
