using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Realtime;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.Extensions.AI;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

public sealed partial class RealtimeVoiceConversationRunnerTests
{
    [Fact]
    public async Task ASupervisorSwitch_ChangesTheLiveSessionsProfile_InTheCallsOwnScopes()
    {
        // Arrange
        var harness = new RealtimeHarness();
        using var handoff = new CancellationTokenSource();
        harness.HandoffRequested = handoff.Token;
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var billing = new AIProfile { ItemId = "profile-billing", Name = "billing" };
        harness.ProfileManager.Setup(x => x.FindByIdAsync("profile-billing", It.IsAny<CancellationToken>())).ReturnsAsync(billing);

        var run = harness.RunAsync();

        // The session registers for commands once it holds the call.
        while (!await harness.Commands.SendAsync(harness.Activity.ItemId, new AIVoiceCallCommand(AIVoiceCallCommandKind.KeysPressed, "1")))
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        // Act
        var accepted = await harness.Commands.SendAsync(harness.Activity.ItemId, new AIVoiceCallCommand(AIVoiceCallCommandKind.SwitchProfile, "profile-billing"));
        var waitedUntil = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (harness.Orchestrator.Switches.Count == 0 && DateTime.UtcNow < waitedUntil)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        await handoff.CancelAsync();
        await run;

        // Assert
        Assert.True(accepted);
        var (request, invocation, conversation) = Assert.Single(harness.Orchestrator.Switches);
        Assert.Same(billing, request.Resource);

        // The switch runs where the call's tools read the call from, not in the supervisor's request.
        Assert.NotNull(invocation);
        Assert.Same(harness.Orchestrator.InvocationScopeAtStart, invocation);
        Assert.Same(billing, conversation?.Profile);
    }

    [Fact]
    public async Task ASwitchToAProfileThatDoesNotExist_LeavesTheSessionAsItIs()
    {
        // Arrange
        var harness = new RealtimeHarness();
        using var handoff = new CancellationTokenSource();
        harness.HandoffRequested = handoff.Token;
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var run = harness.RunAsync();

        while (!await harness.Commands.SendAsync(harness.Activity.ItemId, new AIVoiceCallCommand(AIVoiceCallCommandKind.KeysPressed, "1")))
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        // Act
        await harness.Commands.SendAsync(harness.Activity.ItemId, new AIVoiceCallCommand(AIVoiceCallCommandKind.SwitchProfile, "missing"));
        await Task.Delay(300, TestContext.Current.CancellationToken);
        await handoff.CancelAsync();
        await run;

        // Assert
        Assert.Empty(harness.Orchestrator.Switches);
    }

    [Fact]
    public async Task ASwitchAskedForDuringAReply_IsAppliedOnceTheReplyCompletes_AndTheNewProfileGreetsTheCaller()
    {
        // Arrange
        // Live, a tool on the reply asked for the switch, and the switch ran beside it on the call's document
        // session: the session was left tracking an update it could not flush, and no later turn of the call was
        // stored. The new profile then carried on as the old one without saying who it was.
        var harness = new RealtimeHarness();
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var billing = new AIProfile { ItemId = "profile-billing", Name = "billing" };
        harness.ProfileManager.Setup(x => x.FindByIdAsync("profile-billing", It.IsAny<CancellationToken>())).ReturnsAsync(billing);

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 1);

        harness.Conversation.Queue(new RealtimeConversationEvent { Type = RealtimeConversationEventType.ResponseStarted });
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        // Act
        var accepted = await harness.Commands.SendAsync(harness.Activity.ItemId, new AIVoiceCallCommand(AIVoiceCallCommandKind.SwitchProfile, "profile-billing"));
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        var switchesDuringTheReply = harness.Orchestrator.Switches.Count;

        harness.Conversation.Queue(new RealtimeConversationEvent { Type = RealtimeConversationEventType.ResponseCompleted });
        await WaitUntilAsync(() => harness.Conversation.Added.Any(message => message.Text == VoiceAgentConversationLoop.ProfileSwitchedTurn));
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count == 2);

        // Assert
        Assert.True(accepted);
        Assert.Equal(0, switchesDuringTheReply);
        Assert.Same(billing, Assert.Single(harness.Orchestrator.Switches).Request.Resource);

        // The call's own session keeps the new profile, so the next turn it saves does not put the old one back.
        Assert.Equal("profile-billing", harness.Session.ProfileId);

        // The greeting is asked for inside the conversation, with no instructions of its own: a standalone message
        // made the model repeat its opening.
        Assert.Contains(harness.Conversation.Added, message => message.Role == ChatRole.System && message.Text == VoiceAgentConversationLoop.ProfileSwitchedTurn);
        Assert.Equal(string.Empty, harness.Conversation.Unprompted[^1]);
        Assert.Empty(harness.Conversation.Standalone);

        await LetTheCallGoAsync(harness, run);
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

        public Task SwitchResourceAsync(IRealtimeConversation conversation, RealtimeOrchestrationRequest request, CancellationToken cancellationToken = default)
        {
            Switches.Add((request, AIInvocationScope.Current, AIConversationScope.Current));

            return Task.CompletedTask;
        }
    }
}
