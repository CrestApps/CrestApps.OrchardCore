using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// Guards the call that ended twice. Sending a ringing caller to voicemail hands the call to the provider's
/// recording: the platform answers the caller's leg, plays the greeting and records the message, and the call ends
/// when the caller hangs up. The voicemail command's success was projected as the call's end, so a voicemail call
/// recorded a <c>CallEnded</c> (and its interaction's end) while the caller was still leaving the message, and a
/// second <c>CallEnded</c> when they hung up.
/// </summary>
public sealed class VoicemailCallEndsAtHangupTests
{
    private const string ActivityId = "activity-1";
    private const string InteractionId = "interaction-1";
    private const string CallId = "v3:caller-call";

    [Fact]
    public async Task SendingACallToVoicemail_RecordsOneCallEnded_AtTheCallersHangup()
    {
        // Arrange: an inbound call ringing a direct-to-agent line.
        await using var harness = await DialerModeIntegrationHarness.CreateAsync();
        var cancellationToken = TestContext.Current.CancellationToken;
        var createdUtc = harness.Clock.UtcNow;

        var interaction = new Interaction
        {
            ItemId = InteractionId,
            ActivityItemId = ActivityId,
            Channel = InteractionChannel.Voice,
            Direction = InteractionDirection.Inbound,
            QueueId = ContactCenterConstants.DirectRouting.QueueId,
            ProviderName = DialerModeIntegrationHarness.ProviderName,
            ProviderInteractionId = CallId,
            CreatedUtc = createdUtc,
        };
        interaction.TransitionTo(InteractionStatus.Ringing);
        await harness.InteractionManager.CreateAsync(interaction, cancellationToken: cancellationToken);
        await harness.CommitAsync();

        var executor = CreateVoicemailExecutor(harness);
        var command = new ProviderCommand
        {
            CommandId = "command-1",
            CommandType = ProviderCommandType.SendToVoicemail,
            ProviderName = DialerModeIntegrationHarness.ProviderName,
            InteractionId = InteractionId,
            ActivityItemId = ActivityId,
            RequestPayload = JsonSerializer.Serialize(new ProviderCallActionCommandRequest
            {
                Initiator = CallControlInitiator.System,
                ActivityItemId = ActivityId,
                InteractionId = InteractionId,
                ProviderCallId = CallId,
            }),
        };

        // Act: the offer timed out, so routing sends the caller to voicemail and the provider accepts it.
        harness.Clock.Advance(TimeSpan.FromSeconds(30));
        var sentToVoicemailUtc = harness.Clock.UtcNow;
        var result = await executor.ExecuteAsync(
            command,
            new ProviderCommandClaim { CommandId = command.CommandId, FenceToken = 1, OwnerToken = "owner-1", LeaseExpiresUtc = sentToVoicemailUtc.AddMinutes(5) },
            cancellationToken);
        Assert.True(result.Succeeded);
        await executor.ProjectSuccessAsync(command, result, cancellationToken);
        await harness.CommitAsync();

        // The platform answers the caller's leg to record the message; the caller speaks, then hangs up.
        harness.Clock.Advance(TimeSpan.FromMilliseconds(400));
        await IngestAsync(harness, VoiceCallState.Connected, "answered", hangupCause: null);
        harness.Clock.Advance(TimeSpan.FromSeconds(11));
        var callerHungUpUtc = harness.Clock.UtcNow;
        await IngestAsync(harness, VoiceCallState.Ended, "hangup", HangupCause.NormalClearing);

        // Assert: one call, one end, when the caller hung up.
        var callEnded = Assert.Single(
            harness.PublishedEvents,
            value => value.EventType == ContactCenterConstants.Events.CallEnded && value.InteractionId == InteractionId);
        Assert.Equal(callerHungUpUtc, callEnded.OccurredUtc);
        Assert.Single(harness.PublishedEvents, value => value.EventType == ContactCenterConstants.Events.CallSentToVoicemail);

        var stored = await harness.InteractionManager.FindByIdAsync(InteractionId, cancellationToken);
        Assert.Equal(InteractionStatus.Ended, stored.Status);
        Assert.Equal(callerHungUpUtc, stored.EndedUtc);

        // However long the message took, it was a voicemail: nobody talked to the caller and nobody handled the call.
        var outcomes = InteractionOutcomeClassifier.FromEvents(harness.PublishedEvents);
        Assert.Equal(InteractionOutcome.Voicemail, outcomes.Classify(stored));
        Assert.Equal(0d, outcomes.GetTalkSeconds(stored));
        Assert.Equal((sentToVoicemailUtc - createdUtc).TotalSeconds, outcomes.GetWaitSeconds(stored));
    }

    private static Task IngestAsync(DialerModeIntegrationHarness harness, VoiceCallState state, string suffix, HangupCause? hangupCause)
        => IngestAndCommitAsync(harness, new ProviderVoiceEvent
        {
            ProviderName = DialerModeIntegrationHarness.ProviderName,
            ProviderCallId = CallId,
            State = state,
            HangupCause = hangupCause,
            OccurredUtc = harness.Clock.UtcNow,
            IdempotencyKey = $"{CallId}:{suffix}",
        });

    private static async Task IngestAndCommitAsync(DialerModeIntegrationHarness harness, ProviderVoiceEvent providerEvent)
    {
        await harness.Services.GetRequiredService<IProviderVoiceEventService>().IngestAsync(providerEvent, TestContext.Current.CancellationToken);
        await harness.CommitAsync();
    }

    private static SendToVoicemailProviderCommandTypeExecutor CreateVoicemailExecutor(DialerModeIntegrationHarness harness)
    {
        var telephonyService = new Mock<ITelephonyService>();
        telephonyService
            .Setup(value => value.SendToVoicemailAsync(It.IsAny<CallReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelephonyResult.Success(new TelephonyCall { CallId = CallId }));

        var authorization = new Mock<ICallControlAuthorizationService>();
        authorization
            .Setup(value => value.AuthorizeAsync(It.IsAny<CallControlAuthorizationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CallControlAuthorizationResult.Success(agentId: null, providerCallId: CallId));

        var services = harness.Services;

        return new SendToVoicemailProviderCommandTypeExecutor(
            [telephonyService.Object],
            services.GetRequiredService<IInteractionManager>(),
            services.GetRequiredService<IAgentProfileManager>(),
            services.GetRequiredService<IActivityQueueService>(),
            services.GetRequiredService<IContactCenterWorkStateService>(),
            services.GetRequiredService<IContactCenterActivityWriter>(),
            services.GetRequiredService<IContactCenterEventPublisher>(),
            harness.Clock,
            authorization.Object);
    }
}
