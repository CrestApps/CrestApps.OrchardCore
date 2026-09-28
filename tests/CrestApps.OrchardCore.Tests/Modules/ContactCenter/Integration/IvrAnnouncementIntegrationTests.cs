using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Integration;

/// <summary>
/// An entry point's welcome or closed message on a live call, end to end over the real store: the IVR router says it
/// once the caller is answered, the end of the speech (delivered through the digits sink, as the Telnyx
/// <c>cc-ann</c> <c>call.speak.ended</c> webhook delivers it) moves the caller on, and the progress recorded on the
/// interaction survives each commit.
/// <para>
/// The messages were saved and never spoken. Said naively they would also be cut off (the menu, the hold music or
/// the voicemail greeting started straight after them) or said twice (provider webhooks arrive at least once, and a
/// caller who goes back to the main menu starts it again), so each test pins one of those.
/// </para>
/// </summary>
public sealed class IvrAnnouncementIntegrationTests
{
    private const string Welcome = "Thanks for calling the main line.";
    private const string ClosedMessage = "We are closed for the day.";

    [Fact]
    public async Task TheWelcome_IsSaidToTheAnsweredCaller_AndTheMenuWaitsForItToEnd()
    {
        // Arrange
        await using var fixture = await WelcomeFixtureAsync(EntryPointAnnouncement.NextMenu);

        // Act
        await fixture.AnnounceAsync();

        // Assert
        Assert.Equal([$"answer:{IvrIntegrationFixture.CallId}", "speak"], fixture.Menus.Commands);
        Assert.Equal((IvrIntegrationFixture.CallId, Welcome, false), fixture.Menus.Announcements.Single());
        Assert.Empty(fixture.Menus.Prompts);
        Assert.Equal(EntryPointAnnouncement.Speaking, await StatusAsync(fixture));
    }

    [Fact]
    public async Task TheEndOfTheWelcome_StartsTheMenu()
    {
        // Arrange
        await using var fixture = await WelcomeFixtureAsync(EntryPointAnnouncement.NextMenu);
        await fixture.AnnounceAsync();

        // Act
        var handled = await fixture.EndSpeechAsync("speak-ended-1");

        // Assert
        Assert.True(handled);
        Assert.Equal("gather:12390", fixture.Menus.Commands[^1]);
        Assert.Single(fixture.Menus.Prompts);
        Assert.Equal(EntryPointAnnouncement.Played, await StatusAsync(fixture));
        Assert.Equal("main", IvrExecutionService.ReadState(await fixture.FindInteractionAsync()).CurrentNodeId);
    }

    [Fact]
    public async Task ARepeatedEndOfTheWelcome_DoesNotReplayTheMenuOrMoveTheCallerAgain()
    {
        // Arrange
        // Telnyx delivers a webhook at least once; a redelivered speak.ended restarted the menu over the caller.
        await using var fixture = await WelcomeFixtureAsync(EntryPointAnnouncement.NextMenu);
        await fixture.AnnounceAsync();
        await fixture.EndSpeechAsync("speak-ended-1");

        // Act
        var redelivered = await fixture.EndSpeechAsync("speak-ended-1");
        var another = await fixture.EndSpeechAsync("speak-ended-2");

        // Assert
        Assert.False(redelivered);
        Assert.False(another);
        Assert.Single(fixture.Menus.Prompts);
        Assert.Single(fixture.Menus.Announcements);
        Assert.Single(fixture.IvrEventTypes(), type => type == ContactCenterConstants.Events.IvrMenuEntered);
    }

    [Fact]
    public async Task TheEndOfSpeechOnACallOwedNoMessage_MovesNobody()
    {
        // Arrange
        // A queue's own announcement ends with a speak.ended too, and the cc-ann state stays on the leg.
        await using var fixture = await IvrIntegrationFixture.CreateAsync();
        await fixture.StartAsync();

        // Act
        var handled = await fixture.EndSpeechAsync("speak-ended-1");

        // Assert
        Assert.False(handled);
        Assert.Single(fixture.Menus.Prompts);
        Assert.Null(await fixture.FindQueueItemAsync());
    }

    [Fact]
    public async Task TheEndOfSpeechBeforeTheWelcomeHasStarted_DoesNotSkipIt()
    {
        // Arrange
        // Only a caller who is hearing the message is moved on by its end; one still waiting to hear it is not.
        await using var fixture = await WelcomeFixtureAsync(EntryPointAnnouncement.NextMenu);

        // Act
        var handled = await fixture.EndSpeechAsync("speak-ended-early");
        await fixture.AnnounceAsync();

        // Assert
        Assert.False(handled);
        Assert.Single(fixture.Menus.Announcements);
        Assert.Empty(fixture.Menus.Prompts);
        Assert.Equal(EntryPointAnnouncement.Speaking, await StatusAsync(fixture));
    }

    [Fact]
    public async Task AnnouncingTwice_SaysTheMessageOnce()
    {
        // Arrange
        // The after-commit work that starts the message can run again (a retried scope); only a caller still marked
        // as owed the message is announced to.
        await using var fixture = await WelcomeFixtureAsync(EntryPointAnnouncement.NextMenu);
        await fixture.AnnounceAsync();

        // Act
        await fixture.AnnounceAsync();
        await fixture.EndSpeechAsync("speak-ended-1");
        await fixture.AnnounceAsync();

        // Assert
        Assert.Single(fixture.Menus.Announcements);
        Assert.Single(fixture.Menus.Prompts);
        Assert.Equal(EntryPointAnnouncement.Played, await StatusAsync(fixture));
    }

    [Fact]
    public async Task ACallerWhoHangsUpDuringTheWelcome_IsNotMovedOnWhenItEnds()
    {
        // Arrange
        await using var fixture = await WelcomeFixtureAsync(EntryPointAnnouncement.NextTarget);
        await fixture.AnnounceAsync();
        await fixture.HangUpAsync();

        // Act
        var handled = await fixture.EndSpeechAsync("speak-ended-1");

        // Assert
        Assert.False(handled);
        Assert.Null(await fixture.FindQueueItemAsync());
        Assert.Empty(fixture.Menus.Prompts);
    }

    [Fact]
    public async Task ACallerWhoHungUpBeforeTheWelcomeStarted_HearsNothing()
    {
        // Arrange
        await using var fixture = await WelcomeFixtureAsync(EntryPointAnnouncement.NextMenu);
        await fixture.HangUpAsync();

        // Act
        await fixture.AnnounceAsync();

        // Assert
        Assert.Empty(fixture.Menus.Commands);
        Assert.Equal(EntryPointAnnouncement.Scheduled, await StatusAsync(fixture));
    }

    [Fact]
    public async Task GoingBackToTheMainMenu_DoesNotReplayTheWelcome()
    {
        // Arrange
        // The main menu is a menu step, not a new call: the caller who returns to it hears the menu, not the welcome.
        await using var fixture = await WelcomeFixtureAsync(EntryPointAnnouncement.NextMenu);
        fixture.EntryPoint.IvrFlow.Nodes.Single(node => node.NodeId == "support").Options.Add(
            new IvrOption { Digit = "9", Action = new IvrAction { Kind = IvrActionKind.SubMenu, TargetId = "main" } });
        await fixture.AnnounceAsync();
        await fixture.EndSpeechAsync("speak-ended-1");

        // Act
        await fixture.PressAsync("2", "gather-1");
        await fixture.PressAsync("9", "gather-2");
        await fixture.PressAsync("1", "gather-3");

        // Assert
        Assert.Single(fixture.Menus.Announcements);
        Assert.Equal(3, fixture.Menus.Prompts.Count);
        Assert.Equal(IvrIntegrationFixture.SalesQueueId, (await fixture.FindQueueItemAsync()).QueueId);
        Assert.Equal(EntryPointAnnouncement.Played, await StatusAsync(fixture));
    }

    [Fact]
    public async Task TheEndOfTheWelcomeOnALineWithNoMenu_PutsTheCallerThroughToItsQueue()
    {
        // Arrange
        await using var fixture = await WelcomeFixtureAsync(EntryPointAnnouncement.NextTarget);
        fixture.EntryPoint.IvrFlow = null;
        await fixture.AnnounceAsync();

        // Queued now, the queue's hold music would have cut the welcome off.
        Assert.Null(await fixture.FindQueueItemAsync());

        // Act
        var handled = await fixture.EndSpeechAsync("speak-ended-1");

        // Assert
        Assert.True(handled);
        Assert.Equal(IvrIntegrationFixture.SupportQueueId, (await fixture.FindQueueItemAsync()).QueueId);
        Assert.Equal(IvrIntegrationFixture.AgentId, (await fixture.FindReservationsAsync()).Single().AgentId);
        Assert.Empty(fixture.Menus.Prompts);
    }

    [Fact]
    public async Task TheEndOfTheClosedMessage_PutsAHeldCallerInTheQueueThatHoldsThem()
    {
        // Arrange
        // Hold in queue and overflow both record the queue the caller waits in once the message has been said.
        await using var fixture = await ClosedFixtureAsync(EntryPointAnnouncement.NextQueue, IvrIntegrationFixture.SalesQueueId);
        await fixture.AnnounceAsync();

        Assert.Equal((IvrIntegrationFixture.CallId, ClosedMessage, false), fixture.Menus.Announcements.Single());
        Assert.Null(await fixture.FindQueueItemAsync());

        // Act
        await fixture.EndSpeechAsync("speak-ended-1");

        // Assert
        Assert.Equal(IvrIntegrationFixture.SalesQueueId, (await fixture.FindQueueItemAsync()).QueueId);
        Assert.Empty(fixture.Menus.Prompts);
    }

    [Fact]
    public async Task TheEndOfTheClosedMessage_SendsAVoicemailCallerToVoicemail()
    {
        // Arrange
        await using var fixture = await ClosedFixtureAsync(EntryPointAnnouncement.NextVoicemail);
        await fixture.AnnounceAsync();

        // The voicemail greeting would have played over the message.
        Assert.Empty(fixture.Voicemails);

        // Act
        await fixture.EndSpeechAsync("speak-ended-1");
        await fixture.EndSpeechAsync("speak-ended-1");

        // Assert
        Assert.Equal((IvrIntegrationFixture.ActivityId, EntryPointAnnouncement.ClosedVoicemailReasonCode), fixture.Voicemails.Single());
    }

    [Fact]
    public async Task AClosedMessageBeforeReject_IsSaidByAProviderThatHangsUpAfterIt()
    {
        // Arrange
        // Rejecting the call as the message started cut it off; the provider now ends the call once it has been said,
        // and the platform only records the end.
        await using var fixture = await ClosedFixtureAsync(EntryPointAnnouncement.NextReject);

        // Act
        await fixture.AnnounceAsync();
        var handled = await fixture.EndSpeechAsync("speak-ended-1");

        // Assert
        Assert.Equal((IvrIntegrationFixture.CallId, ClosedMessage, true), fixture.Menus.Announcements.Single());
        Assert.Equal((IvrIntegrationFixture.ActivityId, EntryPointAnnouncement.ClosedRejectReasonCode, true), fixture.EndedCalls.Single());
        Assert.Equal(EntryPointAnnouncement.Played, await StatusAsync(fixture));
        Assert.False(handled);
    }

    [Fact]
    public async Task AProviderThatCannotSpeak_StartsTheMenuAtOnce_AndSaysWhy()
    {
        // Arrange
        // Waiting for a speak.ended that never comes would leave the caller on a silent line forever.
        await using var fixture = await WelcomeFixtureAsync(EntryPointAnnouncement.NextMenu);
        fixture.Menus.CanAnnounce = false;

        // Act
        await fixture.AnnounceAsync();

        // Assert
        Assert.Empty(fixture.Menus.Announcements);
        Assert.Single(fixture.Menus.Prompts);
        Assert.Equal(EntryPointAnnouncement.Failed, await StatusAsync(fixture));
        Assert.Contains(fixture.RouterLog.At(LogLevel.Warning), message => message.Contains("could not be said", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AProviderThatCannotSpeak_SendsAClosedVoicemailCallerToVoicemailAtOnce()
    {
        // Arrange
        await using var fixture = await ClosedFixtureAsync(EntryPointAnnouncement.NextVoicemail);
        fixture.Menus.CanAnnounce = false;

        // Act
        await fixture.AnnounceAsync();

        // Assert
        Assert.Equal((IvrIntegrationFixture.ActivityId, EntryPointAnnouncement.ClosedVoicemailReasonCode), fixture.Voicemails.Single());
        Assert.Equal(EntryPointAnnouncement.Failed, await StatusAsync(fixture));
        Assert.Contains(fixture.RouterLog.At(LogLevel.Warning), message => message.Contains("could not be said", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AProviderThatCannotSpeak_RejectsAClosedCallerItself()
    {
        // Arrange
        // With no message said, nothing on the provider's side will hang up, so the platform has to reject the call.
        await using var fixture = await ClosedFixtureAsync(EntryPointAnnouncement.NextReject);
        fixture.Menus.CanAnnounce = false;

        // Act
        await fixture.AnnounceAsync();

        // Assert
        Assert.Equal((IvrIntegrationFixture.ActivityId, EntryPointAnnouncement.ClosedRejectReasonCode, false), fixture.EndedCalls.Single());
        Assert.Equal(EntryPointAnnouncement.Failed, await StatusAsync(fixture));
    }

    [Fact]
    public async Task AWelcomeRemovedAfterTheCallArrived_IsSkipped_AndTheMenuStarts()
    {
        // Arrange
        await using var fixture = await WelcomeFixtureAsync(EntryPointAnnouncement.NextMenu);
        fixture.EntryPoint.WelcomeMessage = "  ";

        // Act
        await fixture.AnnounceAsync();

        // Assert
        Assert.Empty(fixture.Menus.Announcements);
        Assert.Single(fixture.Menus.Prompts);
        Assert.Equal(EntryPointAnnouncement.Skipped, await StatusAsync(fixture));
    }

    private static async Task<IvrIntegrationFixture> WelcomeFixtureAsync(string next)
    {
        var fixture = await IvrIntegrationFixture.CreateAsync();
        fixture.EntryPoint.WelcomeMessage = Welcome;
        fixture.EntryPoint.ClosedMessage = ClosedMessage;
        await fixture.ScheduleAnnouncementAsync(EntryPointAnnouncement.Welcome, next);

        return fixture;
    }

    private static async Task<IvrIntegrationFixture> ClosedFixtureAsync(string next, string queueId = null)
    {
        var fixture = await IvrIntegrationFixture.CreateAsync();
        fixture.EntryPoint.WelcomeMessage = Welcome;
        fixture.EntryPoint.ClosedMessage = ClosedMessage;
        await fixture.ScheduleAnnouncementAsync(EntryPointAnnouncement.Closed, next, queueId);

        return fixture;
    }

    private static async Task<string> StatusAsync(IvrIntegrationFixture fixture)
        => EntryPointAnnouncement.Read(await fixture.FindInteractionAsync()).Status;
}
