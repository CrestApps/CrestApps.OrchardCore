using System.Net;
using System.Net.Sockets;
using CrestApps.OrchardCore.Asterisk;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskContactCenterVoiceMediaProviderTests
{
    private const string BaseUrl = "http://asterisk.example/ari/";
    private const string PlainPassword = "secret";

    [Fact]
    public async Task OpenSessionAsync_WhenCallAlreadyHasBridge_ReusesBridgeAndDeletesOnlyExternalChannel()
    {
        // Arrange
        using var asteriskRtp = BindLoopback();
        var handler = CreateSuccessfulHandler(
            LocalEndpoint(asteriskRtp),
            """[{"id":"bridge-existing","channels":["call-1"]}]""");
        var provider = CreateProvider(handler);

        // Act
        await using var session = await provider.OpenSessionAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("external-1", session.SessionId);
        Assert.Equal("call-1", session.ProviderCallId);
        Assert.DoesNotContain(handler.Requests, request =>
            request.Method == HttpMethod.Post &&
            request.RequestUri.AbsolutePath.EndsWith("/bridges", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, request =>
            request.Method == HttpMethod.Post &&
            request.RequestUri.AbsoluteUri.Contains("bridges/bridge-existing/addChannel?channel=external-1", StringComparison.Ordinal));

        await session.StopAsync(TestContext.Current.CancellationToken);

        Assert.Contains(handler.Requests, request =>
            request.Method == HttpMethod.Delete &&
            request.RequestUri.AbsolutePath.EndsWith("/channels/external-1", StringComparison.Ordinal));
        Assert.DoesNotContain(handler.Requests, request =>
            request.Method == HttpMethod.Delete &&
            request.RequestUri.AbsolutePath.Contains("/bridges/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OpenSessionAsync_WhenCallHasNoBridge_CreatesBridgeAndCleansOwnedResources()
    {
        // Arrange
        using var asteriskRtp = BindLoopback();
        var handler = CreateSuccessfulHandler(LocalEndpoint(asteriskRtp), "[]");
        var provider = CreateProvider(handler);

        // Act
        await using var session = await provider.OpenSessionAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);
        await session.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(handler.Requests, request =>
            request.Method == HttpMethod.Post &&
            request.RequestUri.AbsoluteUri == $"{BaseUrl}bridges?type=mixing");
        Assert.Contains(handler.Requests, request =>
            request.Method == HttpMethod.Post &&
            request.RequestUri.AbsoluteUri.Contains("bridges/bridge-created/addChannel?channel=call-1", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, request =>
            request.Method == HttpMethod.Delete &&
            request.RequestUri.AbsolutePath.EndsWith("/channels/external-1", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, request =>
            request.Method == HttpMethod.Delete &&
            request.RequestUri.AbsolutePath.EndsWith("/bridges/bridge-created", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OpenSessionAsync_CreatesExternalMediaWithExpectedAriParametersAndAuthorization()
    {
        // Arrange
        using var asteriskRtp = BindLoopback();
        var handler = CreateSuccessfulHandler(LocalEndpoint(asteriskRtp), "[]");
        var provider = CreateProvider(handler);

        // Act
        await using var session = await provider.OpenSessionAsync(
            CreateRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        var request = Assert.Single(handler.Requests, item =>
            item.Method == HttpMethod.Post &&
            item.RequestUri.AbsolutePath.EndsWith("/channels/externalMedia", StringComparison.Ordinal));
        var query = request.RequestUri.Query;

        Assert.Contains("app=crestapps-telephony", query, StringComparison.Ordinal);
        Assert.Contains("external_host=127.0.0.1%3A", query, StringComparison.Ordinal);
        Assert.Contains("format=ulaw", query, StringComparison.Ordinal);
        Assert.Contains("encapsulation=rtp", query, StringComparison.Ordinal);
        Assert.Contains("transport=udp", query, StringComparison.Ordinal);
        Assert.Contains("connection_type=client", query, StringComparison.Ordinal);
        Assert.Equal("Basic", request.Headers.Authorization?.Scheme);
        Assert.Equal(
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"ari-user:{PlainPassword}")),
            request.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task OpenSessionAsync_WhenCancelledAfterExternalChannelCreation_UsesIndependentCleanupToken()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var handler = new StubHttpMessageHandler(request =>
        {
            var uri = request.RequestUri.AbsoluteUri;

            if (uri == $"{BaseUrl}bridges")
            {
                return Json(HttpStatusCode.OK, "[]");
            }

            if (uri == $"{BaseUrl}bridges?type=mixing")
            {
                return Json(HttpStatusCode.OK, """{"id":"bridge-created"}""");
            }

            if (uri.Contains("/addChannel?", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, "{}");
            }

            if (uri.Contains("channels/externalMedia?", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, """{"id":"external-1"}""");
            }

            if (uri.Contains("variable=UNICASTRTP_LOCAL_ADDRESS", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, """{"value":"127.0.0.1"}""");
            }

            if (uri.Contains("variable=UNICASTRTP_LOCAL_PORT", StringComparison.Ordinal))
            {
                cancellation.Cancel();

                return Json(HttpStatusCode.OK, """{"value":"invalid"}""");
            }

            if (request.Method == HttpMethod.Delete)
            {
                return Json(HttpStatusCode.NoContent, "");
            }

            throw new InvalidOperationException($"Unexpected request: {uri}");
        });
        var provider = CreateProvider(handler);

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.OpenSessionAsync(CreateRequest(), cancellation.Token));

        // Assert
        Assert.Contains(handler.Requests, request =>
            request.Method == HttpMethod.Delete &&
            request.RequestUri.AbsolutePath.EndsWith("/channels/external-1", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, request =>
            request.Method == HttpMethod.Delete &&
            request.RequestUri.AbsolutePath.EndsWith("/bridges/bridge-created", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OpenSessionAsync_WhenCleanupFails_PreservesOriginalSessionFailure()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(request =>
        {
            var uri = request.RequestUri.AbsoluteUri;

            if (request.Method == HttpMethod.Delete)
            {
                throw new HttpRequestException("Cleanup failed.");
            }

            return uri switch
            {
                $"{BaseUrl}bridges" => Json(HttpStatusCode.OK, "[]"),
                $"{BaseUrl}bridges?type=mixing" => Json(HttpStatusCode.OK, """{"id":"bridge-created"}"""),
                _ when uri.Contains("/addChannel?", StringComparison.Ordinal) => Json(HttpStatusCode.OK, "{}"),
                _ when uri.Contains("channels/externalMedia?", StringComparison.Ordinal) => Json(HttpStatusCode.OK, """{"id":"external-1"}"""),
                _ when uri.Contains("variable=UNICASTRTP_LOCAL_ADDRESS", StringComparison.Ordinal) => Json(HttpStatusCode.OK, """{"value":"invalid-address"}"""),
                _ when uri.Contains("variable=UNICASTRTP_LOCAL_PORT", StringComparison.Ordinal) => Json(HttpStatusCode.OK, """{"value":"9999"}"""),
                _ => throw new InvalidOperationException($"Unexpected request: {uri}"),
            };
        });
        var provider = CreateProvider(handler);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.OpenSessionAsync(CreateRequest(), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("Asterisk did not provide a valid RTP return endpoint.", exception.Message);
    }

    [Fact]
    public async Task OpenSessionAsync_WhenExternalHostMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        var provider = CreateProvider(new StubHttpMessageHandler(HttpStatusCode.OK));
        var request = new ContactCenterVoiceMediaSessionRequest { ProviderCallId = "call-1" };

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.OpenSessionAsync(request, TestContext.Current.CancellationToken));

        // Assert
        Assert.Contains(AsteriskConstants.ExternalMediaHostMetadataKey, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ContactCenterVoiceMediaEncoding.LinearPcm, 8_000, 1)]
    [InlineData(ContactCenterVoiceMediaEncoding.MuLaw, 16_000, 1)]
    [InlineData(ContactCenterVoiceMediaEncoding.MuLaw, 8_000, 2)]
    public async Task OpenSessionAsync_WhenPreferredFormatUnsupported_ThrowsNotSupportedException(
        ContactCenterVoiceMediaEncoding encoding,
        int sampleRate,
        int channels)
    {
        // Arrange
        var provider = CreateProvider(new StubHttpMessageHandler(HttpStatusCode.OK));
        var request = CreateRequest();
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

    [Theory]
    [InlineData("invalid-address", null)]
    [InlineData("127.0.0.1", "-1")]
    [InlineData("127.0.0.1", "65536")]
    [InlineData("127.0.0.1", "not-a-port")]
    public async Task OpenSessionAsync_WhenUdpBindingMetadataInvalid_ThrowsInvalidOperationException(
        string bindAddress,
        string bindPort)
    {
        // Arrange
        var provider = CreateProvider(new StubHttpMessageHandler(HttpStatusCode.OK));
        var request = CreateRequest();
        request.Metadata[AsteriskConstants.ExternalMediaBindAddressMetadataKey] = bindAddress;

        if (bindPort is not null)
        {
            request.Metadata[AsteriskConstants.ExternalMediaBindPortMetadataKey] = bindPort;
        }

        // Act
        var exception = await Record.ExceptionAsync(() =>
            provider.OpenSessionAsync(request, TestContext.Current.CancellationToken));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    private static AsteriskContactCenterVoiceMediaProvider CreateProvider(StubHttpMessageHandler handler)
    {
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protectedPassword = dataProtectionProvider
            .CreateProtector(AsteriskConstants.ProtectorName)
            .Protect(PlainPassword);
        var settings = new AsteriskSettings
        {
            IsEnabled = true,
            BaseUrl = BaseUrl,
            UserName = "ari-user",
            Password = protectedPassword,
            ApplicationName = "crestapps-telephony",
            TimeoutSeconds = 30,
        };

        return new AsteriskContactCenterVoiceMediaProvider(
            SiteServiceFactory.Create(settings),
            dataProtectionProvider,
            Options.Create(new DefaultAsteriskOptions()),
            new StubHttpClientFactory(handler),
            new TestContactCenterFeatureWorkManager(),
            NullLogger<AsteriskContactCenterVoiceMediaProvider>.Instance);
    }

    private static ContactCenterVoiceMediaSessionRequest CreateRequest()
    {
        return new ContactCenterVoiceMediaSessionRequest
        {
            ProviderCallId = "call-1",
            InteractionId = "interaction-1",
            Metadata =
            {
                [AsteriskConstants.ExternalMediaHostMetadataKey] = "127.0.0.1",
                [AsteriskConstants.ExternalMediaBindAddressMetadataKey] = "127.0.0.1",
            },
        };
    }

    private static StubHttpMessageHandler CreateSuccessfulHandler(
        IPEndPoint asteriskRtpEndpoint,
        string bridgesResponse)
    {
        return new StubHttpMessageHandler(request =>
        {
            var uri = request.RequestUri.AbsoluteUri;

            return uri switch
            {
                $"{BaseUrl}bridges" => Json(HttpStatusCode.OK, bridgesResponse),
                $"{BaseUrl}bridges?type=mixing" => Json(HttpStatusCode.OK, """{"id":"bridge-created"}"""),
                _ when uri.Contains("/addChannel?", StringComparison.Ordinal) => Json(HttpStatusCode.OK, "{}"),
                _ when uri.Contains("channels/externalMedia?", StringComparison.Ordinal) => Json(HttpStatusCode.OK, """{"id":"external-1"}"""),
                _ when uri.Contains("variable=UNICASTRTP_LOCAL_ADDRESS", StringComparison.Ordinal) => Json(
                    HttpStatusCode.OK,
                    $$"""{"value":"{{asteriskRtpEndpoint.Address}}"}"""),
                _ when uri.Contains("variable=UNICASTRTP_LOCAL_PORT", StringComparison.Ordinal) => Json(
                    HttpStatusCode.OK,
                    $$"""{"value":"{{asteriskRtpEndpoint.Port}}"}"""),
                _ when request.Method == HttpMethod.Delete => Json(HttpStatusCode.NoContent, ""),
                _ => throw new InvalidOperationException($"Unexpected request: {uri}"),
            };
        });
    }

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string body)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body),
        };
    }

    private static UdpClient BindLoopback()
    {
        return new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
    }

    private static IPEndPoint LocalEndpoint(UdpClient client)
    {
        return (IPEndPoint)client.Client.LocalEndPoint;
    }
}
