using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routers;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Telemetry;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Services;
using CrestApps.OrchardCore.Tests.Modules.Omnichannel.Sms;
using CrestApps.OrchardCore.Tests.Omnichannel.Messaging;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Infrastructure;
using OrchardCore.Modules;
using OrchardCore.Settings;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Tests.Telemetry.Messaging;

/// <summary>
/// The messaging funnels emit the instruments and spans the observability docs list under
/// <c>CrestApps.OrchardCore.Messaging</c>, tagged only with bounded values: never a phone number, a conversation id or a
/// message body. The names are a public contract that dashboards and alerts depend on.
/// </summary>
public sealed class MessagingTelemetryTests
{
    [Fact]
    public async Task SmsDispatcher_Send_RecordsTheSendDurationAndSpanTaggedWithTheProviderAndResult()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var providerName = "Provider" + Guid.NewGuid().ToString("n");
        var provider = new Mock<ISmsProvider>();
        provider.Setup(p => p.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var dispatcher = CreateDispatcher(providerName, provider.Object);

        // Act
        var result = await dispatcher.SendAsync(new SmsMessage { From = "+15553334444", To = "+15551112222", Body = "hi" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);

        var duration = Assert.Single(capture.Find("crestapps.messaging.send.duration", CrestAppsOrchardCoreTelemetry.ProviderTag, providerName));
        Assert.Equal(MessagingTelemetry.SourceName, duration.Meter);
        Assert.Equal(MessagingTelemetry.ChannelSms, duration.Tags[CrestAppsOrchardCoreTelemetry.ChannelTag]);
        Assert.Equal(MessagingTelemetry.SendResults.Sent, duration.Tags[CrestAppsOrchardCoreTelemetry.ResultTag]);
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeOk, duration.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);

        Assert.Contains(capture.Spans, span => span.OperationName == "crestapps.messaging.send" && Equals(span.GetTagItem(CrestAppsOrchardCoreTelemetry.ProviderTag), providerName));
        AssertNoPersonalData(capture);
    }

    [Fact]
    public async Task SmsDispatcher_WhenTheRecipientOptedOutWithTheProvider_RecordsAnOptedOutSendAndTheOptOut()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var providerName = "Provider" + Guid.NewGuid().ToString("n");
        var provider = new Mock<ISmsProvider>();
        provider.As<ISmsDispatchProvider>()
            .Setup(p => p.DispatchAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessageDispatchResult { Succeeded = false, ErrorCode = OmnichannelConstants.SmsErrorCodes.RecipientOptedOut });

        var dispatcher = CreateDispatcher(providerName, provider.Object);

        // Act
        await dispatcher.SendAsync(new SmsMessage { From = "+15553334444", To = "+15551112222", Body = "hi" }, TestContext.Current.CancellationToken);

        // Assert
        var duration = Assert.Single(capture.Find("crestapps.messaging.send.duration", CrestAppsOrchardCoreTelemetry.ProviderTag, providerName));
        Assert.Equal(MessagingTelemetry.SendResults.OptedOut, duration.Tags[CrestAppsOrchardCoreTelemetry.ResultTag]);
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeError, duration.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);

        Assert.Contains(
            capture.Find("crestapps.messaging.optout", MessagingTelemetry.SourceTag, MessagingTelemetry.OptOutSources.ProviderRefusal),
            measurement => Equals(measurement.Tags[MessagingTelemetry.ActionTag], "opt_out"));
        AssertNoPersonalData(capture);
    }

    [Fact]
    public async Task ConversationRouter_RecordsTheRouterThatClaimedTheConversationAndTheAssignment()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var channelName = "Channel" + Guid.NewGuid().ToString("n");
        var conversationId = "conversation-" + Guid.NewGuid().ToString("n");
        var channel = new Mock<IMessagingChannel>();
        channel.SetupGet(c => c.Name).Returns(channelName);

        var router = new MessagingConversationRouter(
            [new FallbackRouter()],
            Mock.Of<IMessagingChannelResolver>(),
            MessagingTestChannels.RoutingResolver(),
            NullLogger<MessagingConversationRouter>.Instance);

        // Act
        await router.RouteAsync(
            new MessagingRoutingContext
            {
                Trigger = MessagingRoutingTrigger.Inbound,
                Channel = channel.Object,
                IsNewConversation = true,
                Conversation = new MessagingConversation
                {
                    ItemId = conversationId,
                    Channel = channelName,
                    ContactAddress = "+15551112222",
                },
            },
            TestContext.Current.CancellationToken);

        // Assert
        var channelTag = MessagingTelemetry.GetChannelTag(channelName);

        var route = Assert.Single(capture.Find("crestapps.messaging.route", CrestAppsOrchardCoreTelemetry.ChannelTag, channelTag));
        Assert.Equal("fallback", route.Tags[MessagingTelemetry.RouterTag]);
        Assert.Equal("inbound", route.Tags[CrestAppsOrchardCoreTelemetry.TriggerTag]);

        var assignment = Assert.Single(capture.Find("crestapps.messaging.assignment", CrestAppsOrchardCoreTelemetry.ChannelTag, channelTag));
        Assert.Equal("unassigned", assignment.Tags[MessagingTelemetry.StatusTag]);

        // The conversation id goes on the span, never on a metric.
        var span = Assert.Single(capture.Spans, span => span.OperationName == "crestapps.messaging.route" && Equals(span.GetTagItem(MessagingTelemetry.ConversationIdSpanTag), conversationId));
        Assert.Equal("fallback", span.GetTagItem(MessagingTelemetry.RouterTag));
        AssertNoPersonalData(capture);
    }

    [Fact]
    public void InboundMeasurement_RecordsTheResultAndTheOutcomeOnTheDuration()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var channelName = "Channel" + Guid.NewGuid().ToString("n");
        var channelTag = MessagingTelemetry.GetChannelTag(channelName);
        var conversationId = "conversation-" + Guid.NewGuid().ToString("n");

        // Act
        using (var measurement = MessagingTelemetry.StartInbound(channelName))
        {
            measurement.SetSpanTag(MessagingTelemetry.ConversationIdSpanTag, conversationId);
            measurement.Complete(MessagingTelemetry.InboundResults.LockTimeout, CrestAppsOrchardCoreTelemetry.OutcomeTimeout);
        }

        // Assert
        var duration = Assert.Single(capture.Find("crestapps.messaging.inbound.duration", CrestAppsOrchardCoreTelemetry.ChannelTag, channelTag));
        Assert.Equal(MessagingTelemetry.InboundResults.LockTimeout, duration.Tags[CrestAppsOrchardCoreTelemetry.ResultTag]);
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeTimeout, duration.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);

        Assert.Contains(capture.Spans, span => span.OperationName == "crestapps.messaging.inbound" && Equals(span.GetTagItem(MessagingTelemetry.ConversationIdSpanTag), conversationId));
        AssertNoPersonalData(capture);
    }

    [Fact]
    public void AITurnMeasurement_WhenCancelled_RecordsACancelledTurn()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var channelName = "Channel" + Guid.NewGuid().ToString("n");
        var channelTag = MessagingTelemetry.GetChannelTag(channelName);

        // Act
        using (var measurement = MessagingTelemetry.StartAITurn(channelName))
        {
            measurement.Fail(new OperationCanceledException());
        }

        // Assert
        var duration = Assert.Single(capture.Find("crestapps.messaging.ai.turn.duration", CrestAppsOrchardCoreTelemetry.ChannelTag, channelTag));
        Assert.Equal(MessagingTelemetry.ResultCancelled, duration.Tags[CrestAppsOrchardCoreTelemetry.ResultTag]);
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeCancelled, duration.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);
    }

    [Fact]
    public void RecordFirstResponse_TagsWhetherTheTargetWasMet()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var channelName = "Channel" + Guid.NewGuid().ToString("n");
        var createdUtc = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

        var conversation = new MessagingConversation
        {
            ItemId = "conversation-1",
            Channel = channelName,
            CreatedUtc = createdUtc,
            FirstResponseDueUtc = createdUtc.AddMinutes(5),
            FirstResponseBreached = true,
        };

        // Act
        MessagingTelemetry.RecordFirstResponse(conversation, createdUtc.AddMinutes(7));

        // A conversation that was already answered is not measured again.
        conversation.FirstRespondedUtc = createdUtc.AddMinutes(7);
        MessagingTelemetry.RecordFirstResponse(conversation, createdUtc.AddMinutes(9));

        // Assert
        var measurement = Assert.Single(capture.Find("crestapps.messaging.first_response.duration", CrestAppsOrchardCoreTelemetry.ChannelTag, MessagingTelemetry.GetChannelTag(channelName)));
        Assert.Equal(420d, measurement.Value);
        Assert.Equal("breached", measurement.Tags[MessagingTelemetry.SlaTag]);
        AssertNoPersonalData(capture);
    }

    [Fact]
    public void BoundedTagHelpers_MapFreeValuesToTheDocumentedSets()
    {
        Assert.Equal(MessagingTelemetry.ChannelSms, MessagingTelemetry.GetChannelTag(OmnichannelConstants.Channels.Sms));
        Assert.Equal(CrestAppsOrchardCoreTelemetry.Unknown, MessagingTelemetry.GetChannelTag(null));
        Assert.Equal(CrestAppsOrchardCoreTelemetry.Unknown, MessagingTelemetry.GetProviderTag(string.Empty));

        Assert.Equal("none", MessagingTelemetry.GetRouterTag(null));
        Assert.Equal("fallback", MessagingTelemetry.GetRouterTag(new FallbackRouter()));
        Assert.Equal(CrestAppsOrchardCoreTelemetry.Other, MessagingTelemetry.GetRouterTag(Mock.Of<IMessagingInboundRouter>()));

        Assert.Equal("manual_transfer", MessagingTelemetry.GetTriggerTag(MessagingRoutingTrigger.ManualTransfer));
        Assert.Equal("pooled", MessagingTelemetry.GetAssignmentStatusTag(ConversationAssignmentStatus.Pooled));
        Assert.Equal("undelivered", MessagingTelemetry.GetDeliveryStatusTag(MessageDeliveryStatus.Undelivered));

        Assert.Equal(MessagingTelemetry.SendResults.Sent, MessagingTelemetry.GetSendResult(MessageDispatchResult.Success()));
        Assert.Equal(MessagingTelemetry.SendResults.Failed, MessagingTelemetry.GetSendResult(MessageDispatchResult.Failed("refused")));
        Assert.Equal(
            MessagingTelemetry.SendResults.MediaNotSupported,
            MessagingTelemetry.GetSendResult(new MessageDispatchResult { Succeeded = false, ErrorCode = OmnichannelConstants.SmsErrorCodes.MediaNotSupported }));

        Assert.Equal(MessagingTelemetry.OutboxResults.RetriesExhausted, MessagingTelemetry.GetOutboxFailureReason(OutboundDeliveryState.MaxAttempts));
        Assert.Equal(MessagingTelemetry.OutboxResults.NotRetryable, MessagingTelemetry.GetOutboxFailureReason(1));
    }

    private static void AssertNoPersonalData(TelemetryCapture capture)
    {
        Assert.Empty(capture.FindBannedTagKeys());

        // A phone number or a conversation id must never be a metric tag value either, whatever its key.
        Assert.DoesNotContain(
            capture.Measurements.Where(measurement => measurement.Meter == MessagingTelemetry.SourceName),
            measurement => measurement.Tags.Values.Any(value => value is string text && (text.Contains("+1555", StringComparison.Ordinal) || text.StartsWith("conversation-", StringComparison.Ordinal))));
    }

    private static SmsDispatcher CreateDispatcher(string providerName, ISmsProvider provider)
    {
        var endpointManager = new Mock<IOmnichannelChannelEndpointManager>();
        endpointManager.Setup(m => m.GetByServiceAddressAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelChannelEndpoint { Channel = "SMS", Value = "+15553334444", ProviderName = providerName });

        var providerResolver = new Mock<ISmsProviderResolver>();
        providerResolver.Setup(r => r.GetAsync(It.IsAny<string>()))
            .ReturnsAsync((string name) => name == providerName ? provider : null);

        var site = new Mock<ISite>();
        site.Setup(s => s.GetOrCreate<SmsSettings>()).Returns(new SmsSettings());

        var siteService = new Mock<ISiteService>();
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc));

        var providerRouter = new SmsProviderRouter(
            endpointManager.Object,
            providerResolver.Object,
            siteService.Object,
            NullLogger<SmsProviderRouter>.Instance);

        return new SmsDispatcher(
            providerRouter,
            Mock.Of<IMessagingContactResolver>(),
            Mock.Of<IContentManager>(),
            new SharedNumberOptOutResolver(),
            clock.Object,
            RedactorProviderFactory.Create(),
            [],
            NullLogger<SmsDispatcher>.Instance);
    }
}
