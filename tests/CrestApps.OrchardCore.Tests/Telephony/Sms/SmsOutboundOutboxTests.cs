using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Notifications;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

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
        Assert.Equal(TimeSpan.FromMinutes(1), SmsOutboundDeliveryState.GetDelay(1));
        Assert.Equal(TimeSpan.FromMinutes(5), SmsOutboundDeliveryState.GetDelay(2));
        Assert.Equal(TimeSpan.FromMinutes(15), SmsOutboundDeliveryState.GetDelay(3));
        Assert.Equal(TimeSpan.FromMinutes(60), SmsOutboundDeliveryState.GetDelay(4));
    }

    [Fact]
    public void CanRetry_IsFalse_OnceTheScheduleIsExhausted()
    {
        Assert.True(SmsOutboundDeliveryState.CanRetry(1));
        Assert.True(SmsOutboundDeliveryState.CanRetry(SmsOutboundDeliveryState.MaxAttempts - 1));
        Assert.False(SmsOutboundDeliveryState.CanRetry(SmsOutboundDeliveryState.MaxAttempts));
    }

    [Fact]
    public void Options_CarryAPerEndpointBudget_SoABacklogCannotBurstPastTheCarrierRate()
    {
        // The budget is what stops a backlog of a thousand retries on one number leaving all at once and getting
        // that number blocked by the carrier.
        var options = new SmsPortalOptions();

        Assert.True(options.MaxMessagesPerPassPerEndpoint > 0);
        Assert.True(options.OutboxBatchSize >= options.MaxMessagesPerPassPerEndpoint);
    }

    [Fact]
    public void DispatchResult_JoinsProviderErrors_ForTheBubble()
    {
        var result = SmsDispatchResult.Failed("carrier rejected the sender");

        Assert.False(result.Succeeded);
        Assert.Equal("carrier rejected the sender", result.GetErrorText());
    }

    [Fact]
    public void DispatchResult_CarriesTheProviderMessageId_OnSuccess()
    {
        var result = SmsDispatchResult.Success("msg-123");

        Assert.True(result.Succeeded);
        Assert.Equal("msg-123", result.ProviderMessageId);
        Assert.Null(result.GetErrorText());
    }

    [Fact]
    public void Outbox_IsConstructedFromItsDependencies()
    {
        // A construction guard: the outbox is resolved by a background task, so a missing dependency would only
        // surface at runtime on a schedule nobody is watching.
        var outbox = new SmsOutboundOutbox(
            Mock.Of<ISmsDispatcher>(),
            Mock.Of<ISmsRealTimeNotifier>(),
            Mock.Of<ISmsConversationStore>(),
            new OptionsWrapper<SmsPortalOptions>(new SmsPortalOptions()),
            Mock.Of<ISession>(),
            Mock.Of<IClock>(),
            NullLogger<SmsOutboundOutbox>.Instance);

        Assert.NotNull(outbox);
    }
}
