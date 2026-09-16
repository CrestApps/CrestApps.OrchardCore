using System.Text.Json;
using CrestApps.OrchardCore.Dialpad.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Both of these handlers declare <c>GuardedByDurableStore</c> — that a redelivered webhook is absorbed by the
/// inbox rather than by the handler itself. That is a claim about where the guarantee lives, and it is only true
/// if the handler really does hand the same work to the same guarded path both times rather than doing something
/// extra on the second pass. These deliver the same payload twice and check exactly that.
/// </summary>
public sealed class WebhookInboxHandlerReplayTests
{
    [Fact]
    public async Task SmsInboundInboxHandler_ARedeliveredMessage_SavesUnderTheSameIdentityBothTimes()
    {
        // Arrange
        // The durable store deduplicates on the document identity, so replay safety depends on the handler
        // deriving that identity from the payload rather than minting a new one per delivery. If it minted one,
        // the second delivery would store a second copy of the customer's message and the agent would see it
        // twice in the thread.
        var saved = new List<OmnichannelMessage>();

        var session = new Mock<ISession>();
        session
            .Setup(value => value.SaveAsync(It.IsAny<OmnichannelMessage>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<object, bool, string, CancellationToken>((message, _, _, _) => saved.Add((OmnichannelMessage)message));

        var eventHandler = new RecordingOmnichannelEventHandler();

        var handler = new SmsInboundInboxHandler(
            [eventHandler],
            session.Object,
            NullLogger<SmsInboundInboxHandler>.Instance);

        var payload = JsonSerializer.Serialize(new OmnichannelMessage
        {
            Id = "msg-1",
            ProviderMessageId = "provider-msg-1",
            Channel = OmnichannelConstants.Channels.Sms,
            Content = "hello",
            CustomerAddress = "+16502530001",
            ServiceAddress = "+16502530000",
            IsInbound = true,
        });

        // Act
        await handler.HandleAsync(payload, TestContext.Current.CancellationToken);
        await handler.HandleAsync(payload, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, saved.Count);
        Assert.Equal(saved[0].Id, saved[1].Id);

        // And the event it raises carries the provider's own identifier, which is what lets every downstream
        // handler recognise the second delivery as the same event rather than a second message.
        Assert.Equal(2, eventHandler.Events.Count);
        Assert.Equal("provider-msg-1", eventHandler.Events[0].Id);
        Assert.Equal(eventHandler.Events[0].Id, eventHandler.Events[1].Id);
    }

    [Fact]
    public async Task SmsInboundInboxHandler_RefusesAPayloadItCannotRead()
    {
        // Arrange
        // Throwing is correct: the durable inbox retries in a fresh scope, and swallowing here would drop a
        // customer's message silently. Returning quietly is how an inbound text disappears.
        var handler = new SmsInboundInboxHandler(
            [],
            new Mock<ISession>().Object,
            NullLogger<SmsInboundInboxHandler>.Instance);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(() => handler.HandleAsync("null", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DialpadWebhookInboxHandler_ARedeliveredEvent_ReachesTheGuardedServiceBothTimes()
    {
        // Arrange
        // This handler holds no state of its own; its replay contract is entirely that it forwards the parsed
        // event to the webhook service, which is the thing that deduplicates. A handler that short-circuited or
        // mutated on the second pass would break that contract silently.
        var webhookService = new Mock<IDialpadWebhookService>();
        var handler = new DialpadWebhookInboxHandler(webhookService.Object);

        // Serialized with the same options the handler reads it back with, so the test exercises the real
        // wire shape rather than a PascalCase body Dialpad would never send.
        var payload = JsonSerializer.Serialize(
            new DialpadCallEvent
            {
                CallId = "call-1",
                State = "connected",
            },
            DialpadJsonSerializerOptions.Default);

        // Act
        await handler.HandleAsync(payload, TestContext.Current.CancellationToken);
        await handler.HandleAsync(payload, TestContext.Current.CancellationToken);

        // Assert
        webhookService.Verify(
            service => service.ProcessAsync(It.Is<DialpadCallEvent>(value => value.CallId == "call-1"), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task DialpadWebhookInboxHandler_RefusesAPayloadItCannotRead()
    {
        // Arrange
        var handler = new DialpadWebhookInboxHandler(new Mock<IDialpadWebhookService>().Object);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(() => handler.HandleAsync("null", TestContext.Current.CancellationToken));
    }


    [Fact]
    public async Task TelnyxWebhookInboxHandler_ARedeliveredEvent_ReachesTheGuardedServiceBothTimes()
    {
        // Arrange
        // Like the Dialpad handler, this one holds no state: its replay contract is that it parses and forwards,
        // and the webhook service is what deduplicates. A handler that short-circuited the second delivery would
        // break that contract silently, and every call in a Telnyx tenant flows through here.
        var webhookService = new Mock<ITelnyxWebhookService>();
        var handler = new TelnyxWebhookInboxHandler(webhookService.Object);

        var payload = JsonSerializer.Serialize(
            new TelnyxCallEvent
            {
                EventType = "call.answered",
                CallControlId = "ctrl-1",
            },
            TelnyxJsonSerializerOptions.Default);

        // Act
        await handler.HandleAsync(payload, TestContext.Current.CancellationToken);
        await handler.HandleAsync(payload, TestContext.Current.CancellationToken);

        // Assert
        webhookService.Verify(
            service => service.ProcessAsync(It.Is<TelnyxCallEvent>(value => value.CallControlId == "ctrl-1"), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task TelnyxWebhookInboxHandler_RefusesAPayloadItCannotRead()
    {
        // Arrange
        var handler = new TelnyxWebhookInboxHandler(new Mock<ITelnyxWebhookService>().Object);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(() => handler.HandleAsync("null", TestContext.Current.CancellationToken));
    }

    private sealed class RecordingOmnichannelEventHandler : IOmnichannelEventHandler
    {
        public List<OmnichannelEvent> Events { get; } = [];

        public Task HandleAsync(OmnichannelEvent omnichannelEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(omnichannelEvent);

            return Task.CompletedTask;
        }
    }
}
