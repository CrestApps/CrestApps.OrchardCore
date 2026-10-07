using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telnyx;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// The leg of an agent claimed for an answered over-dialed call is tagged so the agent's phone, standing by for the
/// campaign, answers it at once instead of waiting for the push about the claim; every other agent leg is not.
/// </summary>
public sealed class TelnyxStandbyAgentLegTests
{
    [Fact]
    public async Task ConnectToAgentAsync_ForAStandbyClaim_TagsTheLegWithTheClaimAndTheUser_AndLimitsItsRing()
    {
        // Arrange
        var handler = new RecordingHandler();
        var provider = CreateProvider(handler);

        // Act
        var result = await provider.ConnectToAgentAsync(new ContactCenterConnectRequest
        {
            ProviderCallId = "caller-1",
            AgentUserId = "user-1",
            AgentEndpoint = "sip:agent@sip.telnyx.test",
            StandbyReservationId = "res-1",
            AgentLegTimeoutSeconds = 5,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);

        using var body = JsonDocument.Parse(handler.DialBody);
        var root = body.RootElement;
        var headers = root.GetProperty("custom_headers").EnumerateArray()
            .ToDictionary(header => header.GetProperty("name").GetString(), header => header.GetProperty("value").GetString());

        Assert.Equal("res-1", headers[TelnyxConstants.StandbyReservationSipHeader]);
        Assert.Equal("user-1", headers[TelnyxConstants.StandbyAgentUserSipHeader]);
        Assert.Equal(5, root.GetProperty("timeout_secs").GetInt32());

        var state = Encoding.UTF8.GetString(Convert.FromBase64String(root.GetProperty("client_state").GetString()));
        Assert.True(TelnyxOutboundBridgeState.TryParse(state, out var parsed));
        Assert.Equal(TelnyxOutboundBridgeState.ContactCenterAgentLegIntent, parsed.Intent);
        Assert.Equal("res-1", parsed.ReservationId);
        Assert.Equal("user-1", parsed.RingUserId);
    }

    [Theory]
    [InlineData(null, "user-1")]
    [InlineData("res-1", null)]
    public async Task ConnectToAgentAsync_WithoutBothTheClaimAndTheUser_DoesNotTagTheLeg(string standbyReservationId, string agentUserId)
    {
        // Arrange: an accepted inbound offer (no claim), or a claim whose user is unknown.
        var handler = new RecordingHandler();
        var provider = CreateProvider(handler);

        // Act
        var result = await provider.ConnectToAgentAsync(new ContactCenterConnectRequest
        {
            ProviderCallId = "caller-1",
            AgentUserId = agentUserId,
            AgentEndpoint = "sip:agent@sip.telnyx.test",
            StandbyReservationId = standbyReservationId,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);

        using var body = JsonDocument.Parse(handler.DialBody);
        Assert.False(body.RootElement.TryGetProperty("custom_headers", out _));
        Assert.False(body.RootElement.TryGetProperty("timeout_secs", out var timeout) && timeout.ValueKind != JsonValueKind.Null);
    }

    private static TelnyxContactCenterVoiceProvider CreateProvider(HttpMessageHandler handler)
    {
        var options = new TelnyxOptions
        {
            IsEnabled = true,
            ApiKey = "KEY",
            ConnectionId = "connection-1",
            ApiBaseUrl = "https://api.telnyx.test/v2/",
            DefaultOutboundCallerId = "+15550000000",
        };

        var monitor = new Mock<IOptionsMonitor<TelnyxOptions>>();
        monitor.SetupGet(value => value.CurrentValue).Returns(options);

        var workManager = new Mock<IContactCenterFeatureWorkManager>();
        workManager
            .Setup(value => value.TryEnter(It.IsAny<string>()))
            .Returns(new Mock<IContactCenterFeatureWorkLease>().Object);

        var localizer = new Mock<IStringLocalizer<TelnyxContactCenterVoiceProvider>>();
        localizer.Setup(value => value[It.IsAny<string>()]).Returns<string>(name => new LocalizedString(name, name));

        var apiClient = new TelnyxApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.telnyx.test/v2/") },
            new TestOptionsMonitor<TelnyxOptions>(options),
            new TelnyxApiRetryPolicy(TimeSpan.Zero),
            NullLogger<TelnyxApiClient>.Instance);

        return new TelnyxContactCenterVoiceProvider(
            new Mock<ITelephonyProviderResolver>().Object,
            workManager.Object,
            new Mock<ITelnyxAgentCredentialStore>().Object,
            new Mock<ITelnyxAgentEndpointResolver>().Object,
            apiClient,
            new Mock<IClock>().Object,
            NullLogger<TelnyxContactCenterVoiceProvider>.Instance,
            monitor.Object,
            localizer.Object);
    }

    // Answers every request and keeps the body of the agent-leg dial.
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly ConcurrentQueue<string> _dials = new();

        public string DialBody => Assert.Single(_dials);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri.AbsolutePath.EndsWith("/calls", StringComparison.Ordinal))
            {
                _dials.Enqueue(await request.Content.ReadAsStringAsync(cancellationToken));

                return Json("{\"data\":{\"call_control_id\":\"agent-leg-1\"}}");
            }

            return Json("{\"data\":{}}");
        }

        private static HttpResponseMessage Json(string body)
            => new(HttpStatusCode.OK) { Content = new StringContent(body) };
    }
}
