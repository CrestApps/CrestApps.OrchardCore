using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// An agent sends a ringing offer to voicemail. The soft phone used to decline the offer and also ask the telephony hub
/// to send the call to voicemail; the decline was its own route to voicemail for a direct line (and a re-offer for a
/// queue), so the caller was answered and greeted twice. The Contact Center now owns the whole of it: the offer is
/// rejected to voicemail, the agent is released, the work leaves its queue instead of being offered again, and one
/// voicemail command is registered. Runs the real reservation service over the harness's SQLite store.
/// </summary>
public sealed class OfferSentToVoicemailTests
{
    private const string CallId = "v3:caller-call";

    [Fact]
    public async Task AnOfferSentToVoicemail_LeavesItsQueue_ReleasesTheAgent_AndRegistersOneVoicemailCommand()
    {
        // Arrange
        await using var fixture = await QueuedWorkWithdrawalFixture.CreateAsync();
        var reservation = await OfferAsync(fixture);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var rejected = await fixture.ReservationService.RejectToVoicemailAsync(reservation.ItemId, cancellationToken);
        await fixture.Harness.CommitAsync();

        // Assert
        Assert.NotNull(rejected);
        Assert.Equal(ReservationStatus.Rejected, (await fixture.Reservations.FindByIdAsync(reservation.ItemId, cancellationToken)).Status);
        Assert.Equal(QueueItemStatus.Removed, (await fixture.FindQueueItemAsync(reservation.QueueItemId)).Status);
        Assert.Equal(AgentPresenceStatus.Available, await fixture.Harness.GetPresenceAsync("agent-1"));
        Assert.Single(fixture.Harness.PublishedEvents, e => e.EventType == ContactCenterConstants.Events.AgentReleased && e.AggregateId == reservation.ItemId);

        var command = FindVoicemailCommand(fixture, await fixture.Harness.FindInteractionByActivityAsync("activity-1"));
        Assert.Equal(ProviderCommandType.SendToVoicemail, command.CommandType);
    }

    [Fact]
    public async Task ASecondRequestToSendTheSameOfferToVoicemail_IsRefused()
    {
        // Arrange
        // Two requests for one click (a repeat click, the desktop host's notification and the page) must not send the
        // caller to voicemail twice: the second finds the offer already settled.
        await using var fixture = await QueuedWorkWithdrawalFixture.CreateAsync();
        var reservation = await OfferAsync(fixture);
        var cancellationToken = TestContext.Current.CancellationToken;
        await fixture.ReservationService.RejectToVoicemailAsync(reservation.ItemId, cancellationToken);
        await fixture.Harness.CommitAsync();

        // Act
        var second = await fixture.ReservationService.RejectToVoicemailAsync(reservation.ItemId, cancellationToken);
        await fixture.Harness.CommitAsync();

        // Assert
        Assert.Null(second);
        Assert.Single(fixture.Harness.PublishedEvents, e => e.EventType == ContactCenterConstants.Events.AgentReleased && e.AggregateId == reservation.ItemId);
    }

    [Fact]
    public async Task APlainDecline_StillReturnsTheCallerToTheQueue()
    {
        // Arrange
        await using var fixture = await QueuedWorkWithdrawalFixture.CreateAsync();
        var reservation = await OfferAsync(fixture);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await fixture.ReservationService.RejectAsync(reservation.ItemId, cancellationToken);
        await fixture.Harness.CommitAsync();

        // Assert
        Assert.Equal(QueueItemStatus.Waiting, (await fixture.FindQueueItemAsync(reservation.QueueItemId)).Status);
        var interaction = await fixture.Harness.FindInteractionByActivityAsync("activity-1");
        Assert.False(interaction.TechnicalMetadata.ContainsKey(ContactCenterConstants.CommandMetadata.CommandId));
    }

    private static async Task<ActivityReservation> OfferAsync(QueuedWorkWithdrawalFixture fixture)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var agent = await fixture.Harness.SignInAgentAsync("agent-1", "user-1");
        var (_, queueItem) = await fixture.SeedAsync("activity-1", ActivityStatus.Pending);

        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ActivityItemId = "activity-1",
            Channel = InteractionChannel.Voice,
            Direction = InteractionDirection.Inbound,
            QueueId = queueItem.QueueId,
            ProviderName = DialerModeIntegrationHarness.ProviderName,
            ProviderInteractionId = CallId,
            CreatedUtc = fixture.Harness.Clock.UtcNow,
        };
        interaction.TransitionTo(InteractionStatus.Ringing);
        await fixture.Harness.InteractionManager.CreateAsync(interaction, cancellationToken: cancellationToken);
        await fixture.Harness.CommitAsync();

        var reservation = await fixture.ReservationService.ReserveAsync(queueItem, agent, 30, cancellationToken);
        await fixture.Harness.CommitAsync();

        Assert.NotNull(reservation);

        return reservation;
    }

    private static ProviderCommand FindVoicemailCommand(QueuedWorkWithdrawalFixture fixture, Interaction interaction)
    {
        Assert.True(
            interaction.TechnicalMetadata.TryGetValue(ContactCenterConstants.CommandMetadata.CommandId, out var commandId),
            "No provider command was registered for the call.");

        var command = fixture.Harness.Services.GetRequiredService<InMemoryProviderCommandStateService>().Find(commandId?.ToString());

        Assert.NotNull(command);

        return command;
    }
}
