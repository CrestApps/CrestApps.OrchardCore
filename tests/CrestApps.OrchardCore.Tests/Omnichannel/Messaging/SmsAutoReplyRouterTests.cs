using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routers;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Services;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// <c>MessagingEndpointRoutingSettings.AutoReplyMessage</c> was stored by the editor and sent by nothing: an operator
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
        await harness.RouteAsync(new MessagingConversation { Channel = "SMS", ItemId = "c1", ServiceAddress = "+16502530000", ContactAddress = "+16502530001" });

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
        await harness.RouteAsync(new MessagingConversation { Channel = "SMS", ItemId = "c1", ServiceAddress = "+16502530000", ContactAddress = "+16502530001" });

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
        var conversation = new MessagingConversation
        {
            Channel = "SMS",
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
        var conversation = new MessagingConversation
        {
            Channel = "SMS",
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
            new MessagingConversation { Channel = "SMS", ItemId = "c1", ServiceAddress = "+16502530000", ContactAddress = "+16502530001" },
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
        var claimed = await harness.RouteAsync(new MessagingConversation { Channel = "SMS", ItemId = "c1", ServiceAddress = "+16502530000", ContactAddress = "+16502530001" });

        // Assert
        Assert.False(claimed);
    }

    [Fact]
    public async Task Sends_Nothing_ToAContactWhoHasOptedOut()
    {
        // Arrange
        // The agent-facing send path refuses an opted-out contact. An automated acknowledgement that slipped
        // past it would be the platform texting someone who asked it not to.
        var harness = new Harness(autoReply: "Thanks, we got your message.");
        harness.AddContact("contact-1", doNotSms: true);

        // Act
        await harness.RouteAsync(new MessagingConversation { Channel = "SMS", ItemId = "c1", ServiceAddress = "+16502530000", ContactAddress = "+16502530001", ContactContentItemId = "contact-1" });

        // Assert
        harness.Dispatcher.Verify(
            dispatcher => dispatcher.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Sends_TheAutoReply_ToAKnownContactWhoHasNotOptedOut()
    {
        // Arrange
        var harness = new Harness(autoReply: "Thanks, we got your message.");
        harness.AddContact("contact-1", doNotSms: false);

        // Act
        await harness.RouteAsync(new MessagingConversation { Channel = "SMS", ItemId = "c1", ServiceAddress = "+16502530000", ContactAddress = "+16502530001", ContactContentItemId = "contact-1" });

        // Assert
        harness.Dispatcher.Verify(
            dispatcher => dispatcher.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private sealed class Harness
    {
        private readonly AutoReplyRouter _router;
        private readonly SmsKeywordInboundHandler _keywordHandler;
        private readonly IMessagingChannel _channel;
        private readonly OmnichannelChannelEndpoint _endpoint;
        private readonly Dictionary<string, ContentItem> _contacts = new(StringComparer.Ordinal);

        public Mock<ISmsDispatcher> Dispatcher { get; } = new();

        public Mock<IContentManager> ContentManager { get; } = new();

        public Harness(string autoReply)
        {
            _endpoint = new OmnichannelChannelEndpoint
            {
                ItemId = "e1",
                Channel = "SMS",
                Value = "+16502530000",
            };

            _endpoint.Put(new MessagingEndpointRoutingSettings
            {
                TargetType = ConversationRouteTargetType.Queue,
                TargetId = "queue-1",
                AutoReplyMessage = autoReply,
            });

            Dispatcher
                .Setup(dispatcher => dispatcher.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(MessageDispatchResult.Success("provider-1"));

            ContentManager
                .Setup(manager => manager.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
                .ReturnsAsync((string contentItemId, VersionOptions _) => _contacts.TryGetValue(contentItemId, out var contact) ? contact : null);

            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(_now);

            _router = new AutoReplyRouter(ContentManager.Object, clock.Object);
            _channel = MessagingTestChannels.Sms(Dispatcher.Object);
            _keywordHandler = new SmsKeywordInboundHandler(
                Dispatcher.Object,
                ContentManager.Object,
                Options.Create(new SmsKeywordReplySettings()),
                clock.Object);
        }

        public void AddContact(string contentItemId, bool doNotSms)
        {
            var contact = new ContentItem { ContentItemId = contentItemId, ContentType = "Customer" };
            contact.Alter<OmnichannelContactPart>(part => part.SetDoNotSms(doNotSms, _now));
            _contacts[contentItemId] = contact;
        }

        // Runs the SMS channel's inbound handler first, as the inbound pipeline does, so a carrier keyword silences the
        // auto-reply the same way it does in production.
        public async Task<bool> RouteAsync(MessagingConversation conversation, string body = "hello")
        {
            var message = new OmnichannelMessage { Id = "m1", Channel = "SMS", Content = body };
            var inbound = new MessagingInboundContext
            {
                Channel = _channel,
                Message = message,
                Endpoint = _endpoint,
                Conversation = conversation,
                IsNewConversation = true,
            };

            await _keywordHandler.ReceivingAsync(inbound, TestContext.Current.CancellationToken);

            return await _router.TryRouteAsync(
                new MessagingRoutingContext
                {
                    Trigger = MessagingRoutingTrigger.Inbound,
                    Channel = _channel,
                    Message = message,
                    Endpoint = _endpoint,
                    Conversation = conversation,
                    IsNewConversation = true,
                    SuppressAutomatedReplies = inbound.SuppressAutomatedReplies,
                },
                TestContext.Current.CancellationToken);
        }
    }
}
