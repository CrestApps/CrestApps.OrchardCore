using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// What a caller the phone menu answered hears and gets once they have made their choice, end to end over the real
/// store: something to listen to while an agent rings, a callback offer that does what it says, and a transfer to an
/// outside number that puts them somewhere else when the number does not answer.
/// </summary>
public sealed class IvrCallerWaitIntegrationTests
{
    [Fact]
    public async Task AnAgentChoice_PlaysTheLinesMusicWhileTheAgentRings_AndStopsItWhenTheCallGoesToVoicemail()
    {
        // Arrange
        // The menu answered the caller, so the network's ringback was gone; they sat in silence for the whole ring
        // window before being sent to voicemail.
        await using var fixture = await IvrIntegrationFixture.CreateAsync();
        await fixture.StartAsync();

        // Act
        await fixture.PressAsync("3", "gather-1");
        var heardWhileRinging = fixture.Treatment.HoldMusicStarted.ToArray();

        fixture.Harness.Clock.Advance(TimeSpan.FromSeconds(41));
        await fixture.Reservations.ExpireDueAsync(TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        // Assert
        Assert.Equal((IvrIntegrationFixture.CallId, "https://media.example.test/support.mp3"), heardWhileRinging.Single());
        Assert.Contains(IvrIntegrationFixture.CallId, fixture.Treatment.HoldMusicStopped);
    }

    [Fact]
    public async Task AnAgentChoiceOnAPersonalLine_PlaysARingingTone()
    {
        // Arrange
        await using var fixture = await IvrIntegrationFixture.CreateAsync();
        fixture.EntryPoint.TargetType = EntryPointTargetType.Agent;
        fixture.EntryPoint.TargetAgentId = IvrIntegrationFixture.AgentId;
        fixture.EntryPoint.TargetQueueId = null;
        await fixture.StartAsync();

        // Act
        await fixture.PressAsync("3", "gather-1");

        // Assert
        Assert.Equal([IvrIntegrationFixture.CallId], fixture.Treatment.RingbackStarted);
        Assert.Empty(fixture.Treatment.HoldMusicStarted);
    }

    [Fact]
    public async Task PressingTheCallbackKey_SchedulesTheCallback_KeepsTheirPlace_AndEndsTheCallWithAConfirmation()
    {
        // Arrange
        // The offer's key press used to reach nothing: the caller who pressed 1 to be called back stayed on hold.
        await using var fixture = await OfferedACallbackAsync();
        var enteredUtc = (await fixture.FindQueueItemAsync()).QueueEnteredUtc;

        // Act
        var handled = await fixture.PressAsync("1", "gather-callback");

        // Assert
        Assert.True(handled);
        var callback = fixture.Callbacks.Single();
        Assert.Equal("+17025550100", callback.Destination);
        Assert.Equal(IvrIntegrationFixture.SalesQueueId, callback.QueueId);
        Assert.Equal(enteredUtc, callback.RequestedUtc);

        var item = await fixture.FindQueueItemAsync();
        Assert.Equal(QueueItemStatus.Removed, item.Status);
        Assert.NotNull(item.CallbackAcceptedUtc);

        Assert.Equal((IvrIntegrationFixture.CallId, QueueCallbackOfferResponder.ConfirmationMessage), fixture.Treatment.EndedWithMessage.Single());
        Assert.Equal(ActivityStatus.Completed, fixture.FindActivity().Status);
        Assert.Equal(QueueCallbackOfferResponder.ReasonCode, fixture.FindActivity().TerminalReasonCode);
    }

    [Fact]
    public async Task PressingTheCallbackKeyTwice_SchedulesOneCallback()
    {
        // Arrange
        await using var fixture = await OfferedACallbackAsync();

        // Act
        await fixture.PressAsync("1", "gather-callback");
        var again = await fixture.PressAsync("1", "gather-callback");

        // Assert
        Assert.True(again);
        Assert.Single(fixture.Callbacks);
        Assert.Single(fixture.Treatment.EndedWithMessage);
    }

    [Theory]
    [InlineData("2", InboundVoiceDigitsOutcome.Collected)]
    [InlineData(null, InboundVoiceDigitsOutcome.TimedOut)]
    [InlineData("9", InboundVoiceDigitsOutcome.Invalid)]
    public async Task AnyOtherAnswer_PutsTheCallerBackToTheirMusic_AndTheyKeepWaiting(string digits, InboundVoiceDigitsOutcome outcome)
    {
        // Arrange
        await using var fixture = await OfferedACallbackAsync();

        // Act
        var handled = await fixture.PressAsync(digits, "gather-callback", outcome);

        // Assert
        Assert.True(handled);
        Assert.Empty(fixture.Callbacks);
        Assert.Empty(fixture.Treatment.EndedWithMessage);
        Assert.Equal(QueueItemStatus.Waiting, (await fixture.FindQueueItemAsync()).Status);
        Assert.Equal("music:https://media.example.test/sales.mp3", fixture.Treatment.Commands[^1]);
    }

    [Fact]
    public async Task ACallbackTheTenantCannotMake_IsNotPromised_AndTheCallerKeepsWaiting()
    {
        // Arrange
        await using var fixture = await OfferedACallbackAsync();
        fixture.CallbacksEnabled = false;

        // Act
        await fixture.PressAsync("1", "gather-callback");

        // Assert
        Assert.Empty(fixture.Treatment.EndedWithMessage);
        Assert.Equal(QueueItemStatus.Waiting, (await fixture.FindQueueItemAsync()).Status);
        Assert.Equal("music:https://media.example.test/sales.mp3", fixture.Treatment.Commands[^1]);
        Assert.NotEqual(ActivityStatus.Completed, fixture.FindActivity().Status);
    }

    [Fact]
    public async Task AnExternalTransferTheDestinationNeverAnswers_PutsTheCallerThroughToTheLinesTarget()
    {
        // Arrange
        // No fallback on the menu, so the entry point's own target: the support queue, where the agent is offered them.
        await using var fixture = await IvrIntegrationFixture.CreateAsync();
        await fixture.StartAsync();
        await fixture.PressAsync("2", "gather-1");
        await fixture.PressAsync("2", "gather-2");

        // Act
        var handled = await fixture.TransferOutcomes.HandleAsync(new ExternalTransferOutcome
        {
            InteractionId = IvrIntegrationFixture.InteractionId,
            Answered = false,
            HangupCause = "user_busy",
        }, TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        // Assert
        Assert.True(handled);
        var item = await fixture.FindQueueItemAsync();
        Assert.Equal(IvrIntegrationFixture.SupportQueueId, item.QueueId);
        Assert.Equal(QueueItemStatus.Reserved, item.Status);
        Assert.Equal(IvrIntegrationFixture.AgentId, (await fixture.FindReservationsAsync()).Single().AgentId);
    }

    [Fact]
    public async Task AnExternalTransferTheDestinationNeverAnswers_TakesTheMenusFallback()
    {
        // Arrange
        await using var fixture = await IvrIntegrationFixture.CreateAsync();
        fixture.EntryPoint.IvrFlow.FallbackAction = new IvrAction { Kind = IvrActionKind.RouteToQueue, TargetId = IvrIntegrationFixture.SalesQueueId };
        await fixture.StartAsync();
        await fixture.PressAsync("2", "gather-1");
        await fixture.PressAsync("2", "gather-2");

        // Act
        await fixture.TransferOutcomes.HandleAsync(new ExternalTransferOutcome
        {
            InteractionId = IvrIntegrationFixture.InteractionId,
            Answered = false,
            HangupCause = "timeout",
        }, TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        // Assert
        Assert.Equal(IvrIntegrationFixture.SalesQueueId, (await fixture.FindQueueItemAsync()).QueueId);
    }

    [Fact]
    public async Task AnExternalTransferTheCallerAbandonedWhileItRang_RoutesNothing()
    {
        // Arrange
        await using var fixture = await IvrIntegrationFixture.CreateAsync();
        await fixture.StartAsync();
        await fixture.PressAsync("2", "gather-1");
        await fixture.PressAsync("2", "gather-2");

        // Act
        await fixture.TransferOutcomes.HandleAsync(new ExternalTransferOutcome
        {
            InteractionId = IvrIntegrationFixture.InteractionId,
            Answered = false,
            CallerLeft = true,
            HangupCause = "originator_cancel",
        }, TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        // Assert
        Assert.Null(await fixture.FindQueueItemAsync());
    }

    // A caller who chose sales from the menu with nobody free, waiting with the queue's music, who has just been
    // offered a callback.
    private static async Task<IvrIntegrationFixture> OfferedACallbackAsync()
    {
        var fixture = await IvrIntegrationFixture.CreateAsync();
        var sales = fixture.Queues[IvrIntegrationFixture.SalesQueueId];
        sales.Treatment.CallbackDtmfKey = "1";
        sales.Treatment.CallbackOfferAfterSeconds = 0;

        await fixture.StartAsync();
        await fixture.TakeAgentOfflineAsync();
        await fixture.PressAsync("1", "gather-1");

        await fixture.TreatmentService.RunDueAsync(sales, TestContext.Current.CancellationToken);
        await fixture.Harness.CommitAsync();

        Assert.Equal(["stop", "offer:1"], fixture.Treatment.Commands[^2..]);

        return fixture;
    }
}
