using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Realtime;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.Extensions.AI;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

public sealed partial class RealtimeVoiceConversationRunnerTests
{
    [Fact]
    public async Task ASwitch_HandsTheCallToASessionOfTheNewProfile_InTheCallsOwnScopes()
    {
        // Arrange
        // A live session cannot change its voice once it has spoken: switched in place, the new profile answered in
        // the old one's voice, and the caller could not tell they had been transferred.
        var harness = new RealtimeHarness();
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var billing = new AIProfile { ItemId = "profile-billing", Name = "billing" };
        harness.ProfileManager.Setup(x => x.FindByIdAsync("profile-billing", It.IsAny<CancellationToken>())).ReturnsAsync(billing);

        var second = new FakeRealtimeConversation { KeepAlive = true };
        harness.Orchestrator.Later.Enqueue(second);

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 1);
        var callScope = harness.Orchestrator.InvocationScopeAtStart;

        // Act
        var accepted = await harness.Commands.SendAsync(harness.Activity.ItemId, new AIVoiceCallCommand(AIVoiceCallCommandKind.SwitchProfile, "profile-billing"));
        await WaitUntilAsync(() => second.Unprompted.Count == 1);

        // Assert
        Assert.True(accepted);
        Assert.Equal(2, harness.Orchestrator.Starts);
        Assert.Same(billing, harness.Orchestrator.Requests[1].Resource);
        Assert.Same(harness.Session, harness.Orchestrator.Requests[1].ChatSession);
        Assert.NotNull(callScope);
        Assert.Same(callScope, harness.Orchestrator.InvocationScopeAtStart);

        // The old profile's session is closed, never changed in place.
        Assert.True(harness.Conversation.Disposed);
        Assert.Empty(harness.Orchestrator.Switches);

        // The new profile greets the caller from a note in its own session, with no instructions of its own.
        Assert.Contains(second.Added, message => message.Role == ChatRole.System && message.Text == VoiceAgentConversationLoop.ProfileSwitchedTurn);
        Assert.Equal(string.Empty, second.Unprompted[0]);
        Assert.Empty(second.Standalone);

        // The call follows the new profile, and speaks in its voice rather than the voice the call started with.
        Assert.Same(billing, harness.LastContext.Profile);
        Assert.Equal("profile-billing", harness.Session.ProfileId);
        Assert.Equal("profile-billing", harness.Activity.AIProfileId);
        Assert.True(harness.Activity.TryGet<AIProfileSwitch>(out var profileSwitch));
        Assert.Equal("profile-billing", profileSwitch.ProfileId);

        second.KeepAlive = false;
        await LetTheCallGoAsync(harness, run);
    }

    [Fact]
    public async Task ASwitchToAProfileThatDoesNotExist_LeavesTheCallAsItIs()
    {
        // Arrange
        var harness = new RealtimeHarness();
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 1);

        // Act
        await harness.Commands.SendAsync(harness.Activity.ItemId, new AIVoiceCallCommand(AIVoiceCallCommandKind.SwitchProfile, "missing"));
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, harness.Orchestrator.Starts);
        Assert.False(harness.Conversation.Disposed);
        Assert.False(harness.Runner.IsBusy);

        await LetTheCallGoAsync(harness, run);
    }

    [Fact]
    public async Task ASwitchAskedForDuringAReply_WaitsForTheOldProfileToFinish_AndTheCallerIsNotAskedIfTheyAreThere()
    {
        // Arrange
        // Live, the switch ran beside the tool that asked for it, on the call's document session, and no later turn
        // was stored. The old profile's "connecting you" comes in a reply of its own after the tool's, and is let
        // finish; meanwhile the idle watchdog asked the caller whether they were still there.
        var harness = new RealtimeHarness();
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;
        harness.ProfileManager.Setup(x => x.FindByIdAsync("profile-billing", It.IsAny<CancellationToken>())).ReturnsAsync(new AIProfile { ItemId = "profile-billing" });

        var second = new FakeRealtimeConversation { KeepAlive = true };
        harness.Orchestrator.Later.Enqueue(second);

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 1);

        harness.Conversation.Queue(new RealtimeConversationEvent { Type = RealtimeConversationEventType.ResponseStarted });
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        // Act
        await harness.Commands.SendAsync(harness.Activity.ItemId, new AIVoiceCallCommand(AIVoiceCallCommandKind.SwitchProfile, "profile-billing"));
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        var startsDuringTheReply = harness.Orchestrator.Starts;

        harness.Conversation.Queue(
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.ResponseCompleted },
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.ResponseStarted });
        await Task.Delay(RealtimeVoiceConversationRunner.HandoverSettle + TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        var startsDuringTheOldProfilesLastLine = harness.Orchestrator.Starts;
        var busyDuringTheSwitch = harness.Runner.IsBusy;

        harness.Conversation.Queue(new RealtimeConversationEvent { Type = RealtimeConversationEventType.ResponseCompleted });
        await WaitUntilAsync(() => second.Unprompted.Count == 1);
        await WaitUntilAsync(() => !harness.Runner.IsBusy);

        // Assert
        Assert.Equal(1, startsDuringTheReply);
        Assert.Equal(1, startsDuringTheOldProfilesLastLine);
        Assert.True(busyDuringTheSwitch);
        Assert.Equal(2, harness.Orchestrator.Starts);

        second.KeepAlive = false;
        await LetTheCallGoAsync(harness, run);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheTransferTone_RingsDuringTheHandover_AndStopsWhenTheNewProfileSpeaksOrTheCallerTalks(bool callerTalks)
    {
        // Arrange
        // Live, the caller heard silence while the call was put through, and then the idle prompt.
        var harness = new RealtimeHarness();
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;
        harness.ProfileManager.Setup(x => x.FindByIdAsync("profile-billing", It.IsAny<CancellationToken>())).ReturnsAsync(new AIProfile { ItemId = "profile-billing" });

        var second = new FakeRealtimeConversation { KeepAlive = true };
        harness.Orchestrator.Later.Enqueue(second);

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 1);
        var writtenBefore = harness.Media.WrittenAudio.Count;

        // Act
        await harness.Commands.SendAsync(harness.Activity.ItemId, new AIVoiceCallCommand(AIVoiceCallCommandKind.SwitchProfile, "profile-billing"));
        await WaitUntilAsync(() => second.Unprompted.Count == 1);
        await Task.Delay(TimeSpan.FromMilliseconds(400), TestContext.Current.CancellationToken);
        var ringing = harness.Media.WrittenAudio.Count - writtenBefore;

        if (callerTalks)
        {
            second.Queue(new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserSpeechStarted });
        }
        else
        {
            second.Queue(
                new RealtimeConversationEvent { Type = RealtimeConversationEventType.ResponseStarted },
                new RealtimeConversationEvent { Type = RealtimeConversationEventType.AssistantAudioDelta, Audio = new byte[320] });
        }

        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        var afterStopping = harness.Media.WrittenAudio.Count;
        await Task.Delay(TimeSpan.FromMilliseconds(400), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(ringing >= 5, $"Expected the transfer tone on the line, but {ringing} frames were written.");
        Assert.Equal(afterStopping, harness.Media.WrittenAudio.Count);

        second.KeepAlive = false;
        await LetTheCallGoAsync(harness, run);
    }

    [Fact]
    public async Task TheNewProfilesSession_HasTheCallsOwnGuidance_AndKnowsItWasTransferred()
    {
        // Arrange
        // Built from the new profile alone, the switched-to assistant lost the call's rules, among them never telling
        // the caller something is booked unless a tool booked it, and told the caller their oil change was booked.
        var harness = new RealtimeHarness();
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;
        harness.ProfileManager.Setup(x => x.FindByIdAsync("profile-billing", It.IsAny<CancellationToken>())).ReturnsAsync(new AIProfile { ItemId = "profile-billing" });

        var second = new FakeRealtimeConversation { KeepAlive = true };
        harness.Orchestrator.Later.Enqueue(second);

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 1);

        // Act
        await harness.Commands.SendAsync(harness.Activity.ItemId, new AIVoiceCallCommand(AIVoiceCallCommandKind.SwitchProfile, "profile-billing"));
        await WaitUntilAsync(() => harness.Orchestrator.Contexts.Count == 2);

        // Assert
        var instructions = harness.Orchestrator.Contexts[1].SystemMessageBuilder.ToString();
        Assert.Contains("## Who you are calling", instructions, StringComparison.Ordinal);
        Assert.Contains("## Ending the call", instructions, StringComparison.Ordinal);
        Assert.Contains(VoiceCallGuidance.EndingTheCall, instructions, StringComparison.Ordinal);
        Assert.Contains("## The call so far", instructions, StringComparison.Ordinal);
        Assert.Contains("transferred to you from another assistant", instructions, StringComparison.Ordinal);
        Assert.Contains("already been told the call is recorded", instructions, StringComparison.Ordinal);

        second.KeepAlive = false;
        await LetTheCallGoAsync(harness, run);
    }

    [Fact]
    public async Task AHandoverWhoseSessionCannotOpen_GoesBackToTheProfileTheCallWasOn()
    {
        // Arrange
        var harness = new RealtimeHarness();
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;
        harness.Orchestrator.FailAfterFirst = true;
        harness.ProfileManager.Setup(x => x.FindByIdAsync("profile-billing", It.IsAny<CancellationToken>())).ReturnsAsync(new AIProfile { ItemId = "profile-billing" });

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 1);

        // Act
        await harness.Commands.SendAsync(harness.Activity.ItemId, new AIVoiceCallCommand(AIVoiceCallCommandKind.SwitchProfile, "profile-billing"));
        await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

        // Assert
        Assert.True(run.IsCompleted);
        Assert.Same(harness.Profile, harness.LastContext.Profile);
        Assert.Equal(harness.Profile.ItemId, harness.Session.ProfileId);
        Assert.True(harness.LastContext.SessionLost);
        Assert.False(harness.Runner.IsBusy);
    }

    [Fact]
    public async Task OnceTheCallsSessionRefusesACommit_LaterTurnsAreStillStored()
    {
        // Arrange
        // A refused flush leaves what it could not write queued, so every later commit on the call's session failed
        // the same way, and the transcript stopped at the turn where it happened.
        var harness = new RealtimeHarness();
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var storedElsewhere = new System.Collections.Concurrent.ConcurrentQueue<AIChatSessionPrompt>();
        harness.Runner.StoreTurnElsewhere = (prompt, _) =>
        {
            storedElsewhere.Enqueue(prompt);

            return Task.CompletedTask;
        };

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 1);

        harness.DocumentSession.Invocations.Clear();
        harness.DocumentSession.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Incorrect attempt to update an object that does not exist."));

        // Act
        harness.Conversation.Queue(new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserTranscript, Text = "I need to book a service." });
        await WaitUntilAsync(() => storedElsewhere.Count == 1);

        harness.Conversation.Queue(new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserTranscript, Text = "For my car, on Friday." });
        await WaitUntilAsync(() => storedElsewhere.Count == 2);

        // Assert
        Assert.Equal(["I need to book a service.", "For my car, on Friday."], storedElsewhere.Select(prompt => prompt.Content));
        Assert.All(storedElsewhere, prompt => Assert.Equal(harness.Session.SessionId, prompt.SessionId));

        // The broken session is not asked to commit again.
        harness.DocumentSession.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        await LetTheCallGoAsync(harness, run);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AHandoff_RecordsWhetherTheAssistantToldTheCallerFirst(bool assistantSpoke)
    {
        // Arrange
        // Live, the model called the transfer tool straight after the caller's words without saying anything, and
        // the handoff, told the model had announced it, said nothing either.
        var harness = new RealtimeHarness();
        using var handoff = new CancellationTokenSource();
        harness.HandoffRequested = handoff.Token;
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 1);

        harness.Conversation.Queue(new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserTranscript, Text = "Just confirm the slot any other way." });
        await WaitUntilAsync(() => harness.StoredPrompts.Any(prompt => prompt.Content == "Just confirm the slot any other way."));

        if (assistantSpoke)
        {
            QueueResponse(harness, "Let me connect you with someone who can do that.");
            await WaitUntilAsync(() => harness.StoredPrompts.Any(prompt => prompt.Role == ChatRole.Assistant && prompt.Content.StartsWith("Let me connect", StringComparison.Ordinal)));
        }

        // Act
        await handoff.CancelAsync();
        await LetTheCallGoAsync(harness, run);

        // Assert
        Assert.Equal(assistantSpoke, harness.LastContext.HandoffAnnounced);
    }

    private sealed partial class RealtimeHarness
    {
        /// <summary>
        /// Finds the profile a supervisor switches the call to.
        /// </summary>
        public Mock<IAIProfileManager> ProfileManager { get; } = new();
    }

    private sealed partial class RecordingRealtimeOrchestrator
    {
        /// <summary>
        /// The switches asked for, with the invocation and conversation that were current for each.
        /// </summary>
        public List<(RealtimeOrchestrationRequest Request, AIInvocationContext Invocation, AIConversationContext Conversation)> Switches { get; } = [];

        /// <summary>
        /// The contexts each switch was configured into, as the real orchestrator builds them.
        /// </summary>
        public List<OrchestrationContext> SwitchContexts { get; } = [];

        public Task SwitchResourceAsync(IRealtimeConversation conversation, RealtimeOrchestrationRequest request, CancellationToken cancellationToken = default)
        {
            Switches.Add((request, AIInvocationScope.Current, AIConversationScope.Current));

            var context = new OrchestrationContext
            {
                CompletionContext = new AICompletionContext(),
            };

            request.ConfigureContext?.Invoke(context);
            SwitchContexts.Add(context);

            return Task.CompletedTask;
        }
    }
}
