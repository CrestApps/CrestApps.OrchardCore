using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// A caller working an entry point's phone menu end to end over the real store: the menu is answered and played, key
/// presses move them through it and survive being committed and read back, and each choice lands the caller where
/// they chose — a queue and its agent, a named agent, voicemail, an external number, or the fallback.
/// <para>
/// Only a queue choice used to do anything, and only half of it: the caller was enqueued but never offered. Every
/// other choice, and every timeout, left the caller on the line.
/// </para>
/// </summary>
public sealed class IvrIntegrationTests
{
    [Fact]
    public async Task AnswersTheCaller_AndPlaysTheFirstMenuWithItsKeys()
    {
        // Arrange
        await using var fixture = await IvrIntegrationFixture.CreateAsync();

        // Act
        await fixture.StartAsync();

        // Assert
        Assert.Equal([$"answer:{IvrIntegrationFixture.CallId}", "gather:12390"], fixture.Menus.Commands);
        Assert.Equal("main", IvrExecutionService.ReadState(await fixture.FindInteractionAsync()).CurrentNodeId);
        Assert.Equal([ContactCenterConstants.Events.IvrMenuEntered], fixture.IvrEventTypes());
    }

    [Fact]
    public async Task ASubMenuThenAQueue_PutsTheCallerInThatQueue_AndOffersThemToTheAgent()
    {
        // Arrange
        await using var fixture = await IvrIntegrationFixture.CreateAsync();
        await fixture.StartAsync();

        // Act
        await fixture.PressAsync("2", "gather-1");
        await fixture.PressAsync("1", "gather-2");

        // Assert
        Assert.Equal("gather:12", fixture.Menus.Commands[2]);

        var queueItem = await fixture.FindQueueItemAsync();
        Assert.Equal(IvrIntegrationFixture.SupportQueueId, queueItem.QueueId);
        Assert.Equal(QueueItemStatus.Reserved, queueItem.Status);
        Assert.Equal(IvrIntegrationFixture.AgentId, (await fixture.FindReservationsAsync()).Single().AgentId);
        Assert.Equal(AgentPresenceStatus.Reserved, await fixture.Harness.GetPresenceAsync(IvrIntegrationFixture.AgentId));

        var interaction = await fixture.FindInteractionAsync();
        Assert.Equal(IvrIntegrationFixture.SupportQueueId, interaction.QueueId);

        // Answered to hear the menu, the offered caller hears the queue's music rather than silence while it rings.
        Assert.Equal((IvrIntegrationFixture.CallId, "https://media.example.test/support.mp3"), fixture.Treatment.HoldMusicStarted.Single());

        Assert.Equal(
            [
                ContactCenterConstants.Events.IvrMenuEntered,
                ContactCenterConstants.Events.IvrDigitsReceived,
                ContactCenterConstants.Events.IvrMenuEntered,
                ContactCenterConstants.Events.IvrDigitsReceived,
                ContactCenterConstants.Events.IvrActionTaken,
            ],
            fixture.IvrEventTypes());
        Assert.Equal(
            ["Menu", "Menu:support", $"RouteToQueue:{IvrIntegrationFixture.SupportQueueId}"],
            IvrExecutionService.ReadState(interaction).Path.Select(entry => entry.Result));
    }

    [Fact]
    public async Task AQueueWithNobodyFree_KeepsTheCallerWaiting_WithTheQueuesTreatment()
    {
        // Arrange
        // The only agent has gone offline.
        await using var fixture = await IvrIntegrationFixture.CreateAsync();
        await fixture.StartAsync();
        await fixture.TakeAgentOfflineAsync();

        // Act
        await fixture.PressAsync("1", "gather-1");

        // Assert
        var queueItem = await fixture.FindQueueItemAsync();
        Assert.Equal(IvrIntegrationFixture.SalesQueueId, queueItem.QueueId);
        Assert.Equal(QueueItemStatus.Waiting, queueItem.Status);
        Assert.Contains(fixture.Treatment.HoldMusicStarted, entry => entry.MediaId == "https://media.example.test/sales.mp3");
        Assert.Empty(await fixture.FindReservationsAsync());
    }

    [Fact]
    public async Task AnAgentChoice_RingsThatAgentOnTheDirectLine()
    {
        // Arrange
        await using var fixture = await IvrIntegrationFixture.CreateAsync();
        await fixture.StartAsync();

        // Act
        await fixture.PressAsync("3", "gather-1");

        // Assert
        var queueItem = await fixture.FindQueueItemAsync();
        Assert.Equal(ContactCenterConstants.DirectRouting.QueueId, queueItem.QueueId);
        Assert.Equal(IvrIntegrationFixture.AgentId, (await fixture.FindReservationsAsync()).Single().AgentId);

        var interaction = await fixture.FindInteractionAsync();
        Assert.Equal(IvrIntegrationFixture.AgentId, interaction.TechnicalMetadata[ContactCenterConstants.DirectRouting.TargetAgentMetadataKey]?.ToString());
        Assert.Equal("40", interaction.TechnicalMetadata[ContactCenterConstants.DirectRouting.RingTimeoutMetadataKey]?.ToString());
    }

    [Fact]
    public async Task AVoicemailChoice_SendsTheCallerToVoicemail()
    {
        // Arrange
        await using var fixture = await IvrIntegrationFixture.CreateAsync();
        await fixture.StartAsync();

        // Act
        await fixture.PressAsync("9", "gather-1");

        // Assert
        Assert.Equal((IvrIntegrationFixture.ActivityId, IvrCallRouter.VoicemailReasonCode), fixture.Voicemails.Single());
        Assert.Null(await fixture.FindQueueItemAsync());
    }

    [Fact]
    public async Task AnExternalChoice_TransfersTheCaller()
    {
        // Arrange
        await using var fixture = await IvrIntegrationFixture.CreateAsync();
        await fixture.StartAsync();

        // Act
        await fixture.PressAsync("2", "gather-1");
        await fixture.PressAsync("2", "gather-2");

        // Assert
        Assert.Equal("dest-partner", fixture.ExternalTransfers.Single());
        Assert.Null(await fixture.FindQueueItemAsync());
    }

    [Fact]
    public async Task ACallerWhoNeverPresses_IsReprompted_ThenSentToTheEntryPointsTarget()
    {
        // Arrange
        // No fallback is configured, which means the entry point's own target: the support queue.
        await using var fixture = await IvrIntegrationFixture.CreateAsync();
        await fixture.StartAsync();

        // Act
        await fixture.PressAsync(null, "gather-1", InboundVoiceDigitsOutcome.TimedOut);
        await fixture.PressAsync("7", "gather-2", InboundVoiceDigitsOutcome.Invalid);
        await fixture.PressAsync(null, "gather-3", InboundVoiceDigitsOutcome.TimedOut);

        // Assert
        // The first hearing and two re-prompts, then the fallback: three tries.
        Assert.Equal(3, fixture.Menus.Prompts.Count);
        Assert.Equal(IvrIntegrationFixture.SupportQueueId, (await fixture.FindQueueItemAsync()).QueueId);
        Assert.Equal(ContactCenterConstants.Events.IvrFallbackTaken, fixture.IvrEventTypes()[^1]);
    }

    [Fact]
    public async Task AskingToHearTheMenuAgain_CountsAsATry()
    {
        // Arrange
        await using var fixture = await IvrIntegrationFixture.CreateAsync();
        await fixture.StartAsync();

        // Act
        await fixture.PressAsync("0", "gather-1");
        await fixture.PressAsync("0", "gather-2");
        await fixture.PressAsync("0", "gather-3");

        // Assert
        Assert.Equal(3, fixture.Menus.Prompts.Count);
        Assert.NotNull(await fixture.FindQueueItemAsync());
    }

    [Fact]
    public async Task ARedeliveredKeyPress_MovesTheCallerOnce()
    {
        // Arrange
        await using var fixture = await IvrIntegrationFixture.CreateAsync();
        await fixture.StartAsync();

        // Act
        await fixture.PressAsync("2", "gather-1");
        await fixture.PressAsync("2", "gather-1");

        // Assert
        // Still on the support menu: the redelivery replays it rather than pressing "2" on it, which would have
        // transferred the caller out of the building.
        var state = IvrExecutionService.ReadState(await fixture.FindInteractionAsync());
        Assert.Equal("support", state.CurrentNodeId);
        Assert.False(state.Completed);
        Assert.Empty(fixture.ExternalTransfers);
        Assert.Single(fixture.IvrEventTypes(), type => type == ContactCenterConstants.Events.IvrDigitsReceived);
    }

    [Fact]
    public async Task ACallerWhoHangsUpInTheMenu_IsAnAbandon_AndNothingAfterMovesThem()
    {
        // Arrange
        await using var fixture = await IvrIntegrationFixture.CreateAsync();
        await fixture.StartAsync();

        // Act
        await fixture.PressAsync(null, "gather-1", InboundVoiceDigitsOutcome.CallerHungUp);
        var late = await fixture.PressAsync("1", "gather-2");

        // Assert
        Assert.False(late);
        Assert.Null(await fixture.FindQueueItemAsync());
        Assert.Single(fixture.Events, e => e.EventType == ContactCenterConstants.Events.CallAbandoned && e.InteractionId == IvrIntegrationFixture.InteractionId);
        Assert.True(IvrExecutionService.ReadState(await fixture.FindInteractionAsync()).Completed);
    }

    [Fact]
    public async Task OnceRouted_AQueuesCallbackOfferKeyPress_DoesNotMoveTheCaller()
    {
        // Arrange
        await using var fixture = await IvrIntegrationFixture.CreateAsync();
        await fixture.StartAsync();
        await fixture.PressAsync("1", "gather-1");

        // Act
        var handled = await fixture.PressAsync("1", "gather-callback");

        // Assert
        Assert.False(handled);
        Assert.Equal(IvrIntegrationFixture.SalesQueueId, (await fixture.FindQueueItemAsync()).QueueId);
    }
}
