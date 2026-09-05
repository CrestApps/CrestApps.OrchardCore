using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services.Routers;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Services;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

/// <summary>
/// <c>SmsEndpointRoutingSettings.AutoReplyMessage</c> was stored by the editor and sent by nothing: an operator
/// could configure an acknowledgement, see it saved, and watch every contact get silence. These pin that it is
/// sent, and sent once, because an auto-reply on every inbound message is a loop the contact cannot escape.
/// </summary>
public sealed class SmsAutoReplyRouterTests
{
    private static readonly DateTime _now = new(2026, 3, 4, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Sends_TheConfiguredAutoReply_OnTheFirstInboundMessage()
    {
        // Arrange
        var harness = new Harness(autoReply: "Thanks, we got your message.");

        // Act
        await harness.RouteAsync(new SmsConversation { ItemId = "c1", ServiceAddress = "+16502530000", ContactAddress = "+16502530001" });

        // Assert
        harness.Dispatcher.Verify(
            dispatcher => dispatcher.SendAsync(It.Is<SmsMessage>(message => message.Body == "Thanks, we got your message."), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Sends_Nothing_WhenNoAutoReplyIsConfigured()
    {
        // Arrange
        var harness = new Harness(autoReply: null);

        // Act
        await harness.RouteAsync(new SmsConversation { ItemId = "c1", ServiceAddress = "+16502530000", ContactAddress = "+16502530001" });

        // Assert
        harness.Dispatcher.Verify(
            dispatcher => dispatcher.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Sends_AtMostOnceADay_PerThread()
    {
        // Arrange
        // A contact who sends three messages in a row should not get three acknowledgements; that is a machine
        // talking over someone who is trying to reach a person.
        var harness = new Harness(autoReply: "Thanks, we got your message.");
        var conversation = new SmsConversation
        {
            ItemId = "c1",
            ServiceAddress = "+16502530000",
            ContactAddress = "+16502530001",
            LastAutoReplyUtc = _now.AddHours(-2),
        };

        // Act
        await harness.RouteAsync(conversation);

        // Assert
        harness.Dispatcher.Verify(
            dispatcher => dispatcher.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Sends_Again_OnceADayHasPassed()
    {
        // Arrange
        var harness = new Harness(autoReply: "Thanks, we got your message.");
        var conversation = new SmsConversation
        {
            ItemId = "c1",
            ServiceAddress = "+16502530000",
            ContactAddress = "+16502530001",
            LastAutoReplyUtc = _now.AddDays(-1).AddMinutes(-1),
        };

        // Act
        await harness.RouteAsync(conversation);

        // Assert
        harness.Dispatcher.Verify(
            dispatcher => dispatcher.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Equal(_now, conversation.LastAutoReplyUtc);
    }

    [Fact]
    public async Task Sends_Nothing_WhenTheContactHasJustOptedOut()
    {
        // Arrange
        // The keyword handler owns the one reply a STOP is allowed to receive. An auto-reply on top of it is a
        // second message to someone who has just told us to stop, which is the thing STOP exists to prevent.
        var harness = new Harness(autoReply: "Thanks, we got your message.");

        // Act
        await harness.RouteAsync(
            new SmsConversation { ItemId = "c1", ServiceAddress = "+16502530000", ContactAddress = "+16502530001" },
            body: "STOP");

        // Assert
        harness.Dispatcher.Verify(
            dispatcher => dispatcher.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Never_ClaimsTheConversation()
    {
        // Arrange
        // The auto-reply is a side effect, not an ownership decision; claiming would stop the routers that
        // actually place the thread from ever running.
        var harness = new Harness(autoReply: "Thanks, we got your message.");

        // Act
        var claimed = await harness.RouteAsync(new SmsConversation { ItemId = "c1", ServiceAddress = "+16502530000", ContactAddress = "+16502530001" });

        // Assert
        Assert.False(claimed);
    }

    private sealed class Harness
    {
        private readonly AutoReplyRouter _router;
        private readonly OmnichannelChannelEndpoint _endpoint;

        public Mock<ISmsDispatcher> Dispatcher { get; } = new();

        public Harness(string autoReply)
        {
            _endpoint = new OmnichannelChannelEndpoint
            {
                ItemId = "e1",
                Channel = "SMS",
                Value = "+16502530000",
            };

            _endpoint.Put(new SmsEndpointRoutingSettings
            {
                TargetType = SmsNumberRouteTargetType.Queue,
                TargetId = "queue-1",
                AutoReplyMessage = autoReply,
            });

            Dispatcher
                .Setup(dispatcher => dispatcher.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(SmsDispatchResult.Success("provider-1"));

            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(_now);

            _router = new AutoReplyRouter(Dispatcher.Object, clock.Object);
        }

        public Task<bool> RouteAsync(SmsConversation conversation, string body = "hello")
            => _router.TryRouteAsync(
                new SmsRoutingContext
                {
                    Trigger = SmsRoutingTrigger.Inbound,
                    Message = new OmnichannelMessage { Id = "m1", Content = body },
                    Endpoint = _endpoint,
                    Conversation = conversation,
                    IsNewConversation = true,
                },
                TestContext.Current.CancellationToken);
    }
}
