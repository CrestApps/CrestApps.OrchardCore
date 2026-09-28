using System.Net;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telnyx;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.WebSockets;
using CrestApps.OrchardCore.WebSockets.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class TelnyxContactCenterVoiceMediaProviderTests
{
    private const string CallId = "v2:call-control-1";

    [Fact]
    public async Task OpenSessionAsync_WhenTelnyxConnects_StartsBidirectionalStreamAndReturnsSession()
    {
        // Arrange
        var registry = new InMemoryWebSocketConnectionRegistry();
        using var socket = new FakeWebSocket();
        var handler = CreateConnectingHandler(registry, socket, out _);
        var provider = CreateProvider(handler, registry);

        // Act
        await using var session = await provider.OpenSessionAsync(
            CreateRequest("https://cc.example"),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CallId, session.ProviderCallId);
        Assert.False(string.IsNullOrWhiteSpace(session.SessionId));
        Assert.Equal(ContactCenterVoiceMediaEncoding.MuLaw, session.IncomingFormat.Encoding);
        Assert.Equal(8_000, session.IncomingFormat.SampleRate);

        var start = GetRequestBody(handler, "streaming_start");
        using var document = JsonDocument.Parse(start);
        var root = document.RootElement;
        var streamUrl = root.GetProperty("stream_url").GetString();
        Assert.StartsWith("wss://cc.example/api/telnyx/media/stream?t=", streamUrl, StringComparison.Ordinal);
        Assert.Equal("inbound_track", root.GetProperty("stream_track").GetString());
        Assert.Equal("PCMU", root.GetProperty("stream_codec").GetString());
        Assert.Equal("rtp", root.GetProperty("stream_bidirectional_mode").GetString());
        Assert.Equal("PCMU", root.GetProperty("stream_bidirectional_codec").GetString());
        Assert.Equal("self", root.GetProperty("stream_bidirectional_target_legs").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("stream_auth_token").GetString()));
    }

    [Fact]
    public async Task OpenSessionAsync_UsesSiteBaseUrl_WhenNoMetadataOverride()
    {
        // Arrange
        var registry = new InMemoryWebSocketConnectionRegistry();
        using var socket = new FakeWebSocket();
        var handler = CreateConnectingHandler(registry, socket, out _);
        var provider = CreateProvider(handler, registry, siteBaseUrl: "https://tenant.example/");

        // Act
        await using var session = await provider.OpenSessionAsync(
            CreateRequest(publicUrl: null),
            TestContext.Current.CancellationToken);

        // Assert
        var start = GetRequestBody(handler, "streaming_start");
        using var document = JsonDocument.Parse(start);
        var streamUrl = document.RootElement.GetProperty("stream_url").GetString();
        Assert.StartsWith("wss://tenant.example/api/telnyx/media/stream?t=", streamUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenSessionAsync_StopAsync_IssuesStreamingStop()
    {
        // Arrange
        var registry = new InMemoryWebSocketConnectionRegistry();
        using var socket = new FakeWebSocket();
        var handler = CreateConnectingHandler(registry, socket, out _);
        var provider = CreateProvider(handler, registry);
        var session = await provider.OpenSessionAsync(
            CreateRequest("https://cc.example"),
            TestContext.Current.CancellationToken);

        // Act
        await session.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(handler.Requests, request =>
            request.Method == HttpMethod.Post &&
            request.RequestUri.AbsolutePath.EndsWith("/actions/streaming_stop", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OpenSessionAsync_WhenNotConfigured_Throws()
    {
        // Arrange
        var provider = CreateProvider(
            new StubHttpMessageHandler(HttpStatusCode.OK),
            new InMemoryWebSocketConnectionRegistry(),
            configured: false);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            provider.OpenSessionAsync(CreateRequest("https://cc.example"), TestContext.Current.CancellationToken));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public async Task OpenSessionAsync_WhenQuiescing_ThrowsWithoutCallingTelnyx()
    {
        // Arrange
        var workManager = new TestContactCenterFeatureWorkManager();
        workManager.Quiesce(TelnyxConstants.ContactCenterMediaWorkPartition);
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var provider = CreateProvider(handler, new InMemoryWebSocketConnectionRegistry(), workManager: workManager);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            provider.OpenSessionAsync(CreateRequest("https://cc.example"), TestContext.Current.CancellationToken));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task OpenSessionAsync_WhenStreamingStartFails_ThrowsAndReleasesLease()
    {
        // Arrange
        var workManager = new TestContactCenterFeatureWorkManager();
        var handler = new StubHttpMessageHandler(request =>
            request.RequestUri.AbsolutePath.EndsWith("streaming_start", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{}") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        var provider = CreateProvider(handler, new InMemoryWebSocketConnectionRegistry(), workManager: workManager);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            provider.OpenSessionAsync(CreateRequest("https://cc.example"), TestContext.Current.CancellationToken));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(0, workManager.ActiveLeaseCount);
        Assert.Contains(handler.Requests, request =>
            request.RequestUri.AbsolutePath.EndsWith("/actions/streaming_stop", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OpenSessionAsync_WhenTelnyxNeverConnects_TimesOutAndReleasesLease()
    {
        // Arrange
        var registry = new InMemoryWebSocketConnectionRegistry();
        var workManager = new TestContactCenterFeatureWorkManager();
        string capturedToken = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri.AbsolutePath.EndsWith("streaming_start", StringComparison.Ordinal))
            {
                capturedToken = ExtractToken(request.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        });
        var provider = CreateProvider(handler, registry, workManager: workManager, connectTimeout: TimeSpan.FromMilliseconds(200));

        // Act
        var exception = await Record.ExceptionAsync(() =>
            provider.OpenSessionAsync(CreateRequest("https://cc.example"), TestContext.Current.CancellationToken));

        // Assert
        Assert.NotNull(exception);
        Assert.Equal(0, workManager.ActiveLeaseCount);
        Assert.Null(await registry.TryClaimAsync(capturedToken, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(ContactCenterVoiceMediaEncoding.LinearPcm, 8_000, 1)]
    [InlineData(ContactCenterVoiceMediaEncoding.MuLaw, 16_000, 1)]
    [InlineData(ContactCenterVoiceMediaEncoding.MuLaw, 8_000, 2)]
    public async Task OpenSessionAsync_WhenPreferredFormatUnsupported_Throws(
        ContactCenterVoiceMediaEncoding encoding,
        int sampleRate,
        int channels)
    {
        // Arrange
        var provider = CreateProvider(new StubHttpMessageHandler(HttpStatusCode.OK), new InMemoryWebSocketConnectionRegistry());
        var request = CreateRequest("https://cc.example");
        request.PreferredIncomingFormat = new ContactCenterVoiceMediaFormat
        {
            Encoding = encoding,
            SampleRate = sampleRate,
            Channels = channels,
        };

        // Act
        var exception = await Record.ExceptionAsync(() =>
            provider.OpenSessionAsync(request, TestContext.Current.CancellationToken));

        // Assert
        Assert.IsType<NotSupportedException>(exception);
    }

    private static StubHttpMessageHandler CreateConnectingHandler(
        InMemoryWebSocketConnectionRegistry registry,
        FakeWebSocket socket,
        out string token)
    {
        token = null;
        var tokenHolder = new string[1];

        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri.AbsolutePath.EndsWith("streaming_start", StringComparison.Ordinal))
            {
                var body = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                tokenHolder[0] = ExtractToken(body);

                // The in-memory registry completes synchronously, so resolving the claim here is safe.
                var connection = registry.TryClaimAsync(tokenHolder[0]).GetAwaiter().GetResult();
                connection?.TryComplete(socket);
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        });

        return handler;
    }

    private static string ExtractToken(string streamingStartBody)
    {
        using var document = JsonDocument.Parse(streamingStartBody);
        var streamUrl = document.RootElement.GetProperty("stream_url").GetString();
        var query = new Uri(streamUrl).Query.TrimStart('?');

        return Uri.UnescapeDataString(query.Split('=', 2)[1]);
    }

    private static string GetRequestBody(StubHttpMessageHandler handler, string pathSuffix)
    {
        for (var i = 0; i < handler.Requests.Count; i++)
        {
            if (handler.Requests[i].RequestUri.AbsolutePath.EndsWith(pathSuffix, StringComparison.Ordinal))
            {
                return handler.RequestBodies[i];
            }
        }

        throw new InvalidOperationException($"No request ending in '{pathSuffix}' was recorded.");
    }

    private static ContactCenterVoiceMediaSessionRequest CreateRequest(string publicUrl)
    {
        var request = new ContactCenterVoiceMediaSessionRequest { ProviderCallId = CallId };

        if (!string.IsNullOrEmpty(publicUrl))
        {
            request.Metadata[TelnyxConstants.MediaStreamPublicUrlMetadataKey] = publicUrl;
        }

        return request;
    }

    private static TelnyxContactCenterVoiceMediaProvider CreateProvider(
        StubHttpMessageHandler handler,
        IWebSocketConnectionRegistry registry,
        bool configured = true,
        TestContactCenterFeatureWorkManager workManager = null,
        string siteBaseUrl = null,
        TimeSpan? connectTimeout = null)
    {
        var options = new TestOptionsMonitor<TelnyxOptions>(new TelnyxOptions
        {
            IsEnabled = true,
            ApiKey = configured ? "KEY" : null,
            ConnectionId = configured ? "connection-1" : null,
            ApiBaseUrl = "https://api.telnyx.test/v2/",
        });

        var site = new Mock<ISite>();
        site.SetupGet(s => s.BaseUrl).Returns(siteBaseUrl);
        var siteService = new Mock<ISiteService>();
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return new TelnyxContactCenterVoiceMediaProvider(
            siteService.Object,
            new Microsoft.AspNetCore.Http.HttpContextAccessor(),
            new StubHttpClientFactory(handler),
            workManager ?? new TestContactCenterFeatureWorkManager(),
            registry,
            options,
            NullLogger<TelnyxContactCenterVoiceMediaProvider>.Instance,
            connectTimeout);
    }
}
