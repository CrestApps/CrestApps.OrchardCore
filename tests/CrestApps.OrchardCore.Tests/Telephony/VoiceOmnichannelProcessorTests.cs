using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// The automated AI voice dialer's last step before a real phone rings: the call is placed to the activity's number,
/// from the activity's own caller id, tagged so the webhook loop knows which conversation it belongs to, and the
/// activity waits for the customer to answer. Confirmed live end to end.
/// </summary>
public sealed class VoiceOmnichannelProcessorTests
{
    private static readonly DateTime _now = new(2026, 9, 27, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task StartAsync_DialsThePreferredDestination_FromTheEndpointOnTheActivitysChannel()
    {
        // Arrange
        var activity = CreateActivity();
        var harness = new Harness();
        harness.AddEndpoint(OmnichannelConstants.Channels.Phone, "+15550001111");

        // Act
        await harness.CreateProcessor().StartAsync(activity, TestContext.Current.CancellationToken);

        // Assert
        var call = Assert.Single(harness.Calls);
        Assert.Equal("+15555550100", call.To);
        Assert.Equal("+15550001111", call.From);
    }

    [Theory]
    [InlineData(OmnichannelConstants.Channels.Sms)]
    [InlineData(null)]
    public async Task StartAsync_WhenTheEndpointIsNotOnThePhoneChannel_LeavesTheCallerIdToTheTenantDefault(string endpointChannel)
    {
        // Arrange
        // An endpoint repointed at SMS, or deleted, is not a number this call may present: the call goes out on the
        // tenant's default outbound caller id instead (the Telnyx client fills it in for an empty one).
        var activity = CreateActivity();
        var harness = new Harness();

        if (endpointChannel is not null)
        {
            harness.AddEndpoint(endpointChannel, "+19998887777");
        }

        // Act
        await harness.CreateProcessor().StartAsync(activity, TestContext.Current.CancellationToken);

        // Assert
        var call = Assert.Single(harness.Calls);
        Assert.Equal("+15555550100", call.To);
        Assert.Null(call.From);
    }

    [Fact]
    public async Task StartAsync_TagsTheCallWithTheAiVoiceIntentAndTheActivity_AndAwaitsTheCustomer()
    {
        // Arrange
        // The webhook loop finds the conversation from the call's client state alone; without the intent and the activity
        // the call rings, and nothing ever speaks on it.
        var activity = CreateActivity();
        var harness = new Harness();

        // Act
        await harness.CreateProcessor().StartAsync(activity, TestContext.Current.CancellationToken);

        // Assert
        var call = Assert.Single(harness.Calls);
        Assert.Equal(TelnyxOutboundBridgeState.AiVoiceLegIntent, call.ClientState.Intent);
        Assert.Equal("activity-1", call.ClientState.ActivityId);
        Assert.Equal(ActivityStatus.AwaitingCustomerAnswer, activity.Status);
    }

    [Fact]
    public async Task StartAsync_OpensTheConversationsAiSession_Once()
    {
        // Arrange
        var activity = CreateActivity();
        var harness = new Harness();

        // Act
        await harness.CreateProcessor().StartAsync(activity, TestContext.Current.CancellationToken);

        // Assert
        var session = Assert.Single(harness.SavedSessions);
        Assert.Equal(session.SessionId, activity.AISessionId);
        Assert.Equal("profile-1", session.ProfileId);
        Assert.Equal(_now, session.CreatedUtc);
    }

    [Fact]
    public async Task StartAsync_KeepsTheAiSessionTheActivityAlreadyHas()
    {
        // Arrange
        // A retried activity carries on the conversation it already had; a second session would start the assistant over
        // with no memory of it.
        var activity = CreateActivity();
        activity.AISessionId = "session-existing";
        var harness = new Harness();

        // Act
        await harness.CreateProcessor().StartAsync(activity, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(harness.SavedSessions);
        Assert.Equal("session-existing", activity.AISessionId);
        Assert.Single(harness.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task StartAsync_WithNoDestination_Throws_AndPlacesNoCall(string destination)
    {
        // Arrange
        var activity = CreateActivity();
        activity.PreferredDestination = destination;
        var harness = new Harness();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.CreateProcessor().StartAsync(activity, TestContext.Current.CancellationToken));

        Assert.Empty(harness.Calls);
        Assert.Empty(harness.SavedSessions);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task StartAsync_WhenTheCallIsNotPlaced_Throws_AndDoesNotAwaitTheCustomer(string callControlId)
    {
        // Arrange
        // A call Telnyx never placed must fail the activity's attempt, not leave it waiting for an answer that never comes.
        var activity = CreateActivity();
        var harness = new Harness { CallControlId = callControlId };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.CreateProcessor().StartAsync(activity, TestContext.Current.CancellationToken));

        Assert.Equal(ActivityStatus.NotStated, activity.Status);
    }

    private static OmnichannelActivity CreateActivity()
        => new()
        {
            ItemId = "activity-1",
            Channel = OmnichannelConstants.Channels.Phone,
            ChannelEndpointId = "endpoint-1",
            PreferredDestination = "+15555550100",
            AIProfileId = "profile-1",
            InteractionType = ActivityInteractionType.Automated,
            Status = ActivityStatus.NotStated,
        };

    private sealed class Harness
    {
        private readonly Mock<ICatalog<OmnichannelChannelEndpoint>> _endpoints = new();

        public List<(string To, string From, TelnyxOutboundBridgeState ClientState)> Calls { get; } = [];

        public List<AIChatSession> SavedSessions { get; } = [];

        public string CallControlId { get; set; } = "ctrl-1";

        public void AddEndpoint(string channel, string value)
            => _endpoints
                .Setup(catalog => catalog.FindByIdAsync("endpoint-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OmnichannelChannelEndpoint { ItemId = "endpoint-1", Channel = channel, Value = value });

        public VoiceOmnichannelProcessor CreateProcessor()
        {
            var voiceClient = new Mock<ITelnyxVoiceAgentClient>();
            voiceClient
                .Setup(client => client.OriginateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TelnyxOutboundBridgeState>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, TelnyxOutboundBridgeState, CancellationToken>((to, from, clientState, _) => Calls.Add((to, from, clientState)))
                .ReturnsAsync(() => CallControlId);

            var chatSessions = new Mock<IAIChatSessionManager>();
            chatSessions
                .Setup(manager => manager.SaveAsync(It.IsAny<AIChatSession>(), It.IsAny<CancellationToken>()))
                .Callback<AIChatSession, CancellationToken>((session, _) => SavedSessions.Add(session))
                .Returns(Task.CompletedTask);

            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(_now);

            return new VoiceOmnichannelProcessor(
                voiceClient.Object,
                _endpoints.Object,
                chatSessions.Object,
                clock.Object,
                NullLogger<VoiceOmnichannelProcessor>.Instance);
        }
    }
}
