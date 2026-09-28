using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;
using static CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration.TransferIntegrationFixture;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// Blind and warm transfers from the soft phone, end to end through the real Contact Center services on a real SQLite
/// store, with only the provider faked. Each test starts from agent A on an answered queue call and agent B Available.
/// </summary>
public sealed class TransferIntegrationTests
{
    [Fact]
    public async Task BlindToAgent_OffersTheCallerToThatAgent_AndReleasesTheTransferringAgentToWrapUp()
    {
        await using var fixture = await CreateAsync();
        var interaction = await fixture.FindInteractionAsync();

        var result = await fixture.TransferService.TransferAsync(
            BlindRequest(InteractionTransferTargetType.Agent, AgentB, interaction.ItemId),
            TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        Assert.True(result.Succeeded, result.Reason);

        // A is off the call and into after-call work; B is being rung for it.
        Assert.Equal(AgentPresenceStatus.WrapUp, await fixture.Harness.GetPresenceAsync(AgentA));
        Assert.Equal(AgentPresenceStatus.Reserved, await fixture.Harness.GetPresenceAsync(AgentB));

        var offer = (await fixture.Reservations.GetActiveByAgentAsync(AgentB, TestContext.Current.CancellationToken)).Single();
        Assert.Equal(ActivityId, offer.ActivityItemId);

        interaction = await fixture.FindInteractionAsync();
        Assert.Equal(AgentB, interaction.AgentId);
        Assert.Equal(DialerModeIntegrationHarness.QueueId, interaction.QueueId);

        var entry = Assert.Single(interaction.TransferHistory);
        Assert.Equal(AgentA, entry.FromParticipantId);
        Assert.Equal(AgentB, entry.ToParticipantId);
        Assert.Null(entry.CompletedUtc);
        Assert.Equal(InteractionTransferHistory.OfferedToAgent, entry.Result);

        // A's leg left the call before it was hung up, so its hangup cannot end the call; the caller hears music.
        var session = await fixture.FindSessionAsync();
        Assert.NotNull(session.Legs.Single(leg => leg.ProviderLegId == AgentLegA).EndedUtc);
        Assert.Equal([AgentLegA], fixture.Provider.ReleasedAgentLegs);
        Assert.Contains(fixture.Treatment.HoldMusicStarted, played => played.CallId == fixture.CallId);
        Assert.Empty(fixture.Provider.Transfers);

        var transferred = fixture.Events.Single(e => e.EventType == ContactCenterConstants.Events.InteractionTransferred);
        Assert.Equal(UserA, transferred.ActorId);
        Assert.Equal(ContactCenterActorType.Agent, transferred.ActorType);
        Assert.Equal(AgentA, transferred.GetData<CallLifecycleEventData>().AgentId);

        // B answers: the transfer is now complete, and B is on the call.
        var accepted = await fixture.CallCommands.AcceptInboundOfferAsync(offer.ItemId, UserB, TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        Assert.True(accepted.Succeeded, accepted.Reason);
        Assert.Equal(AgentPresenceStatus.Busy, await fixture.Harness.GetPresenceAsync(AgentB));

        interaction = await fixture.FindInteractionAsync();
        entry = Assert.Single(interaction.TransferHistory);
        Assert.NotNull(entry.CompletedUtc);
        Assert.Equal(InteractionTransferHistory.Answered, entry.Result);
        Assert.Equal(DialerModeIntegrationHarness.QueueId, interaction.QueueId);
    }

    [Fact]
    public async Task BlindToAgent_WhoIsNotAvailable_IsRefused_AndTheCallStaysWithTheTransferringAgent()
    {
        await using var fixture = await CreateAsync();
        await fixture.Harness.PresenceManager.SetPresenceAsync(UserB, AgentPresenceStatus.Break, "Lunch", TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();
        var interaction = await fixture.FindInteractionAsync();

        var result = await fixture.TransferService.TransferAsync(
            BlindRequest(InteractionTransferTargetType.Agent, AgentB, interaction.ItemId),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(AgentPresenceStatus.Busy, await fixture.Harness.GetPresenceAsync(AgentA));
        Assert.Empty(fixture.Provider.ReleasedAgentLegs);
        Assert.Empty((await fixture.FindInteractionAsync()).TransferHistory);
    }

    [Fact]
    public async Task BlindToQueue_SeatsTheCallerInTheQueue_WithAtLeastTheirPriority_AndOffersTheNextAgent()
    {
        await using var fixture = await CreateAsync();
        var interaction = await fixture.FindInteractionAsync();

        var result = await fixture.TransferService.TransferAsync(
            BlindRequest(InteractionTransferTargetType.Queue, SupportQueueId, interaction.ItemId),
            TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        Assert.True(result.Succeeded, result.Reason);
        Assert.Equal(AgentPresenceStatus.WrapUp, await fixture.Harness.GetPresenceAsync(AgentA));

        // The old assignment is finished and the caller is in the new queue, offered to the free agent.
        var queueItem = await fixture.QueueItems.FindByActivityIdAsync(ActivityId, TestContext.Current.CancellationToken);
        Assert.Equal(SupportQueueId, queueItem.QueueId);
        Assert.Equal(QueueItemStatus.Reserved, queueItem.Status);
        Assert.Equal(AgentB, queueItem.AgentId);
        Assert.True(queueItem.Priority >= InteractionPriority.Normal);

        interaction = await fixture.FindInteractionAsync();
        Assert.Equal(SupportQueueId, interaction.QueueId);
        var entry = Assert.Single(interaction.TransferHistory);
        Assert.Equal(nameof(InteractionTransferTargetType.Queue), entry.TargetType);
        Assert.Equal(SupportQueueId, entry.ToParticipantId);
        Assert.Equal([AgentLegA], fixture.Provider.ReleasedAgentLegs);
        Assert.Contains(fixture.Treatment.HoldMusicStarted, played => played.CallId == fixture.CallId && played.MediaId.Contains("support", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BlindToQueue_WithNobodyFree_LeavesTheCallerWaitingAndPlaysTheQueuesTreatment()
    {
        await using var fixture = await CreateAsync();
        await fixture.Harness.PresenceManager.SetPresenceAsync(UserB, AgentPresenceStatus.Break, "Lunch", TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();
        var interaction = await fixture.FindInteractionAsync();

        var result = await fixture.TransferService.TransferAsync(
            BlindRequest(InteractionTransferTargetType.Queue, SupportQueueId, interaction.ItemId),
            TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        Assert.True(result.Succeeded, result.Reason);
        var queueItem = await fixture.QueueItems.FindByActivityIdAsync(ActivityId, TestContext.Current.CancellationToken);
        Assert.Equal(QueueItemStatus.Waiting, queueItem.Status);
        Assert.Equal(SupportQueueId, queueItem.QueueId);
        Assert.Contains(fixture.Treatment.HoldMusicStarted, played => played.CallId == fixture.CallId);
    }

    [Fact]
    public async Task BlindToExternal_WhenTheTenantAllowsUnlistedNumbers_MovesTheCallOut_AndSettlesItAsTransferred()
    {
        await using var fixture = await CreateAsync();
        fixture.ExternalSettings.AllowUnlistedNumbers = true;
        var interaction = await fixture.FindInteractionAsync();

        var result = await fixture.TransferService.TransferAsync(
            BlindRequest(InteractionTransferTargetType.External, "+15557654321", interaction.ItemId),
            TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        Assert.True(result.Succeeded, result.Reason);
        var transfer = Assert.Single(fixture.Provider.Transfers);
        Assert.Equal("+15557654321", transfer.Target);
        Assert.Equal(fixture.CallId, transfer.ProviderCallId);

        var session = await fixture.FindSessionAsync();
        Assert.Equal(VoiceCallState.Transferred, session.State);
        Assert.Equal(AgentPresenceStatus.WrapUp, await fixture.Harness.GetPresenceAsync(AgentA));

        interaction = await fixture.FindInteractionAsync();
        var entry = Assert.Single(interaction.TransferHistory);
        Assert.NotNull(entry.CompletedUtc);
        Assert.Equal(InteractionTransferHistory.SentToExternalNumber, entry.Result);
        Assert.Contains(AgentLegA, fixture.Provider.ReleasedAgentLegs);
    }

    [Fact]
    public async Task BlindToExternal_WhenUnlistedNumbersAreOff_IsRefused_AndNothingMoves()
    {
        await using var fixture = await CreateAsync();
        var interaction = await fixture.FindInteractionAsync();

        var result = await fixture.TransferService.TransferAsync(
            BlindRequest(InteractionTransferTargetType.External, "+15557654321", interaction.ItemId),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Empty(fixture.Provider.Transfers);
        Assert.Equal(VoiceCallState.Connected, (await fixture.FindSessionAsync()).State);
        Assert.Equal(AgentPresenceStatus.Busy, await fixture.Harness.GetPresenceAsync(AgentA));
        Assert.Single(fixture.Events, e => e.EventType == ContactCenterConstants.Events.InteractionTransferDenied);
    }

    [Fact]
    public async Task BlindToExternal_ToTheContactCentersOwnNumber_IsRefused_EvenWhenUnlistedNumbersAreAllowed()
    {
        await using var fixture = await CreateAsync();
        fixture.ExternalSettings.AllowUnlistedNumbers = true;
        var interaction = await fixture.FindInteractionAsync();

        var result = await fixture.TransferService.TransferAsync(
            BlindRequest(InteractionTransferTargetType.External, OwnNumber, interaction.ItemId),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains("own numbers", result.Reason, StringComparison.Ordinal);
        Assert.Empty(fixture.Provider.Transfers);
        Assert.Equal(AgentPresenceStatus.Busy, await fixture.Harness.GetPresenceAsync(AgentA));
    }

    [Fact]
    public async Task WarmToAgent_Complete_HandsTheCallToTheConsultedAgent_AndReleasesTheConsultingAgent()
    {
        await using var fixture = await CreateAsync();
        var interaction = await fixture.FindInteractionAsync();

        var started = await StartConsultWithBAsync(fixture, interaction.ItemId);

        // The provider was asked to hold the caller and ring B from A's own leg.
        var begin = Assert.Single(fixture.Provider.ConsultsBegun);
        Assert.Equal(fixture.CallId, begin.ProviderCallId);
        Assert.Equal(UserB, begin.Metadata[ContactCenterConstants.AttendedTransferMetadata.AgentUserId]);
        Assert.Equal(AgentLegA, begin.Metadata[ContactCenterConstants.AttendedTransferMetadata.AgentLegId]);
        Assert.Equal("https://media.example.test/hold.mp3", begin.Metadata[ContactCenterConstants.AttendedTransferMetadata.HoldAudio]);

        // B answers the consult.
        Assert.True(await fixture.ConsultLegs.OnAnsweredAsync(DialerModeIntegrationHarness.ProviderName, fixture.CallId, started.ConsultId, fixture.Provider.ConsultLegId, TestContext.Current.CancellationToken));
        await fixture.Harness.CommitAsync();
        Assert.Equal(AgentPresenceStatus.Busy, await fixture.Harness.GetPresenceAsync(AgentB));

        var completed = await fixture.WarmTransfers.CompleteAsync(Command(interaction.ItemId, started.ConsultId), TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        Assert.True(completed.Succeeded, completed.Reason);
        Assert.Single(fixture.Provider.ConsultsCompleted);
        Assert.Equal(AgentPresenceStatus.WrapUp, await fixture.Harness.GetPresenceAsync(AgentA));
        Assert.Equal(AgentPresenceStatus.Busy, await fixture.Harness.GetPresenceAsync(AgentB));
        Assert.Equal([AgentLegA], fixture.Provider.ReleasedAgentLegs);

        var session = await fixture.FindSessionAsync();
        Assert.Equal(AgentB, session.AgentId);
        Assert.Equal(VoiceCallState.Connected, session.State);
        var bLeg = session.Legs.Single(leg => leg.ProviderLegId == fixture.Provider.ConsultLegId);
        Assert.Equal(CallPartyRole.Agent, bLeg.Role);
        Assert.Equal(AgentB, bLeg.AgentId);
        Assert.Null(bLeg.EndedUtc);
        Assert.Equal(ConsultCallStatus.Completed, session.Consults.Single().Status);

        interaction = await fixture.FindInteractionAsync();
        Assert.Equal(AgentB, interaction.AgentId);
        var entry = Assert.Single(interaction.TransferHistory);
        Assert.NotNull(entry.CompletedUtc);
        Assert.Equal(InteractionTransferHistory.HandedOverAfterConsult, entry.Result);
        Assert.Single(fixture.Events, e => e.EventType == ContactCenterConstants.Events.ConsultCompleted);
        Assert.Single(fixture.Events, e => e.EventType == ContactCenterConstants.Events.InteractionTransferred);

        // B hanging up now ends the call, which is B's to wrap up.
        Assert.Equal(
            ConsultLegEndedOutcome.CallEnded,
            await fixture.ConsultLegs.OnEndedAsync(DialerModeIntegrationHarness.ProviderName, fixture.CallId, started.ConsultId, fixture.Provider.ConsultLegId, HangupCause.NormalClearing, fixture.Harness.Clock.UtcNow, TestContext.Current.CancellationToken));
        await fixture.Harness.CommitAsync();
        Assert.True(CallSessionLifecycleIsTerminal((await fixture.FindSessionAsync()).State));
        Assert.Equal(AgentPresenceStatus.WrapUp, await fixture.Harness.GetPresenceAsync(AgentB));
    }

    [Fact]
    public async Task WarmToAgent_Cancel_DropsTheConsultedAgent_AndReturnsTheCallerToTheConsultingAgent()
    {
        await using var fixture = await CreateAsync();
        var interaction = await fixture.FindInteractionAsync();
        var started = await StartConsultWithBAsync(fixture, interaction.ItemId);
        await fixture.ConsultLegs.OnAnsweredAsync(DialerModeIntegrationHarness.ProviderName, fixture.CallId, started.ConsultId, fixture.Provider.ConsultLegId, TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        var cancelled = await fixture.WarmTransfers.CancelAsync(Command(interaction.ItemId, started.ConsultId), TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        Assert.True(cancelled.Succeeded, cancelled.Reason);
        var cancel = Assert.Single(fixture.Provider.ConsultsCancelled);
        Assert.Equal(ContactCenterConstants.AttendedTransferMetadata.EndedByAgent, cancel.Metadata[ContactCenterConstants.AttendedTransferMetadata.EndedBy]);
        Assert.Empty(fixture.Provider.ConsultsCompleted);
        Assert.Empty(fixture.Provider.ReleasedAgentLegs);

        Assert.Equal(AgentPresenceStatus.Busy, await fixture.Harness.GetPresenceAsync(AgentA));
        Assert.Equal(AgentPresenceStatus.Available, await fixture.Harness.GetPresenceAsync(AgentB));

        var session = await fixture.FindSessionAsync();
        Assert.Equal(AgentA, session.AgentId);
        Assert.Equal(ConsultCallStatus.Cancelled, session.Consults.Single().Status);

        interaction = await fixture.FindInteractionAsync();
        var entry = Assert.Single(interaction.TransferHistory);
        Assert.Null(entry.CompletedUtc);
        Assert.Equal(InteractionTransferHistory.ConsultCancelled, entry.Result);
    }

    [Fact]
    public async Task WarmToAgent_WhenTheConsultedAgentHangsUp_TheCallerReturnsToTheConsultingAgent()
    {
        await using var fixture = await CreateAsync();
        var interaction = await fixture.FindInteractionAsync();
        var started = await StartConsultWithBAsync(fixture, interaction.ItemId);
        await fixture.ConsultLegs.OnAnsweredAsync(DialerModeIntegrationHarness.ProviderName, fixture.CallId, started.ConsultId, fixture.Provider.ConsultLegId, TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        var outcome = await fixture.ConsultLegs.OnEndedAsync(DialerModeIntegrationHarness.ProviderName, fixture.CallId, started.ConsultId, fixture.Provider.ConsultLegId, HangupCause.NormalClearing, fixture.Harness.Clock.UtcNow, TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        Assert.Equal(ConsultLegEndedOutcome.ReturnedToAgent, outcome);
        var cancel = Assert.Single(fixture.Provider.ConsultsCancelled);
        Assert.Equal(ContactCenterConstants.AttendedTransferMetadata.EndedByTarget, cancel.Metadata[ContactCenterConstants.AttendedTransferMetadata.EndedBy]);
        Assert.Equal(AgentPresenceStatus.Busy, await fixture.Harness.GetPresenceAsync(AgentA));
        Assert.Equal(AgentPresenceStatus.Available, await fixture.Harness.GetPresenceAsync(AgentB));
        Assert.Equal(VoiceCallState.Connected, (await fixture.FindSessionAsync()).State);
    }

    [Fact]
    public async Task WarmToAgent_WhenTheCallerHangsUpMidConsult_TheConsultEndsAndBothAgentsAreReleased()
    {
        await using var fixture = await CreateAsync();
        var interaction = await fixture.FindInteractionAsync();
        var started = await StartConsultWithBAsync(fixture, interaction.ItemId);
        await fixture.ConsultLegs.OnAnsweredAsync(DialerModeIntegrationHarness.ProviderName, fixture.CallId, started.ConsultId, fixture.Provider.ConsultLegId, TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        await fixture.CallerHangsUpAsync();

        // The consulted agent's leg is dropped, and nobody is left handling a caller who is gone.
        var cancel = Assert.Single(fixture.Provider.ConsultsCancelled);
        Assert.Equal(ContactCenterConstants.AttendedTransferMetadata.EndedByCaller, cancel.Metadata[ContactCenterConstants.AttendedTransferMetadata.EndedBy]);
        Assert.Empty(fixture.Provider.ConsultsCompleted);

        var session = await fixture.FindSessionAsync();
        Assert.True(CallSessionLifecycleIsTerminal(session.State));
        Assert.Equal(ConsultCallStatus.Cancelled, session.Consults.Single().Status);
        Assert.Equal(AgentPresenceStatus.WrapUp, await fixture.Harness.GetPresenceAsync(AgentA));
        Assert.Equal(AgentPresenceStatus.Available, await fixture.Harness.GetPresenceAsync(AgentB));

        interaction = await fixture.FindInteractionAsync();
        var entry = Assert.Single(interaction.TransferHistory);
        Assert.Null(entry.CompletedUtc);
        Assert.Equal(InteractionTransferHistory.CallerHungUp, entry.Result);
    }

    [Fact]
    public async Task WarmTransfer_CannotBeCompletedBeforeTheDestinationAnswers()
    {
        await using var fixture = await CreateAsync();
        var interaction = await fixture.FindInteractionAsync();
        var started = await StartConsultWithBAsync(fixture, interaction.ItemId);

        var completed = await fixture.WarmTransfers.CompleteAsync(Command(interaction.ItemId, started.ConsultId), TestContext.Current.CancellationToken);

        Assert.False(completed.Succeeded);
        Assert.Empty(fixture.Provider.ConsultsCompleted);
        Assert.Equal(AgentPresenceStatus.Busy, await fixture.Harness.GetPresenceAsync(AgentA));
        Assert.Equal(AgentA, (await fixture.FindSessionAsync()).AgentId);
    }

    private static async Task<WarmTransferResult> StartConsultWithBAsync(TransferIntegrationFixture fixture, string interactionId)
    {
        var started = await fixture.WarmTransfers.StartAsync(new WarmTransferRequest
        {
            InteractionId = interactionId,
            UserId = UserA,
            Principal = Principal(UserA),
            TargetType = InteractionTransferTargetType.Agent,
            TargetId = AgentB,
        }, TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        Assert.True(started.Succeeded, started.Reason);
        Assert.True(started.IsLive);

        return started;
    }

    private static WarmTransferCommand Command(string interactionId, string consultId)
        => new()
        {
            InteractionId = interactionId,
            ConsultId = consultId,
            UserId = UserA,
            Principal = Principal(UserA),
        };

    private static bool CallSessionLifecycleIsTerminal(VoiceCallState state)
        => CallSessionLifecycle.IsTerminal(state);
}
