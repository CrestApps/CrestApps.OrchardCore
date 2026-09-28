using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Notifications;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using Moq;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// The retry schedule an outbound message follows when a provider refuses it. Losing a message because the
/// carrier was unreachable for a minute is the failure these pin against.
/// </summary>
public sealed class SmsOutboundOutboxTests
{
    [Fact]
    public void RetrySchedule_WidensAndIsBounded()
    {
        // A fixed, widening schedule: retrying immediately hammers a provider that is already refusing, and
        // retrying forever hides a permanently bad number from the agent who sent it.
        Assert.Equal(TimeSpan.FromMinutes(1), OutboundDeliveryState.GetDelay(1));
        Assert.Equal(TimeSpan.FromMinutes(5), OutboundDeliveryState.GetDelay(2));
        Assert.Equal(TimeSpan.FromMinutes(15), OutboundDeliveryState.GetDelay(3));
        Assert.Equal(TimeSpan.FromMinutes(60), OutboundDeliveryState.GetDelay(4));
    }

    [Fact]
    public void CanRetry_IsFalse_OnceTheScheduleIsExhausted()
    {
        Assert.True(OutboundDeliveryState.CanRetry(1));
        Assert.True(OutboundDeliveryState.CanRetry(OutboundDeliveryState.MaxAttempts - 1));
        Assert.False(OutboundDeliveryState.CanRetry(OutboundDeliveryState.MaxAttempts));
    }

    [Fact]
    public void CanRetry_IsFalse_WhenTheRecipientOptedOut()
    {
        // A recipient who opted out is refused on every attempt; retrying only spends the number's reputation.
        Assert.False(OutboundDeliveryState.CanRetry(1, OmnichannelConstants.SmsErrorCodes.RecipientOptedOut));
        Assert.True(OutboundDeliveryState.CanRetry(1, errorCode: null));
        Assert.False(OutboundDeliveryState.CanRetry(OutboundDeliveryState.MaxAttempts, errorCode: null));
    }

    [Fact]
    public void Options_CarryAPerEndpointBudget_SoABacklogCannotBurstPastTheCarrierRate()
    {
        // The budget is what stops a backlog of a thousand retries on one number leaving all at once and getting
        // that number blocked by the carrier.
        var options = new MessagingWorkspaceOptions();

        Assert.True(options.MaxMessagesPerPassPerEndpoint > 0);
        Assert.True(options.OutboxBatchSize >= options.MaxMessagesPerPassPerEndpoint);
    }

    [Fact]
    public void DispatchResult_JoinsProviderErrors_ForTheBubble()
    {
        var result = MessageDispatchResult.Failed("carrier rejected the sender");

        Assert.False(result.Succeeded);
        Assert.Equal("carrier rejected the sender", result.GetErrorText());
    }

    [Fact]
    public void DispatchResult_CarriesTheProviderMessageId_OnSuccess()
    {
        var result = MessageDispatchResult.Success("msg-123");

        Assert.True(result.Succeeded);
        Assert.Equal("msg-123", result.ProviderMessageId);
        Assert.Null(result.GetErrorText());
    }

    [Fact]
    public void Outbox_IsConstructedFromItsDependencies()
    {
        // A construction guard: the outbox is resolved by a background task, so a missing dependency would only
        // surface at runtime on a schedule nobody is watching.
        var outbox = new MessagingOutbox(
            Mock.Of<IMessagingChannelResolver>(),
            Mock.Of<IMessagingRealTimeNotifier>(),
            Mock.Of<IMessagingConversationStore>(),
            new OptionsWrapper<MessagingWorkspaceOptions>(new MessagingWorkspaceOptions()),
            Mock.Of<ISession>(),
            Mock.Of<IClock>(),
            NullLogger<MessagingOutbox>.Instance);

        Assert.NotNull(outbox);
    }
}
