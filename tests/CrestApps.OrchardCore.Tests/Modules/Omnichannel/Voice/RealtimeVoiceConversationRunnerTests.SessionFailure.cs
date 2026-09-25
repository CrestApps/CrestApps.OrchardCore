using CrestApps.Core.AI.Realtime;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// What a live call does when its session goes wrong: a provider error that is only a refusal must not end the
/// conversation, a session that is really gone is brought back, and one that cannot be brought back is reported
/// rather than left holding a silent line.
/// </summary>
/// <remarks>
/// Live, the caller talked over the assistant, the provider refused the resulting truncation ("Audio content of
/// 5150ms is already shorter than 5300ms"), and that one refusal ended the session. The call stayed up with nobody
/// on it for fifty seconds until the caller hung up, and was then concluded as though it had run its course.
/// </remarks>
public sealed partial class RealtimeVoiceConversationRunnerTests
{
    [Fact]
    public async Task ATruncationTheProviderRefuses_DoesNotEndTheCall()
    {
        // Arrange
        var harness = TalkedOverHarness(TalkingDbfs);
        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.SentAudio.Count >= CallerFrames);
        harness.Conversation.Queue(new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserSpeechStarted });
        await WaitUntilAsync(() => !harness.Conversation.Truncations.IsEmpty);
        var cut = harness.Conversation.Truncations.Single();
        var writtenBefore = harness.Media.WrittenAudio.Count;

        // Act
        harness.Conversation.Queue(
            new RealtimeConversationEvent
            {
                Type = RealtimeConversationEventType.Error,
                ErrorMessage = $"Audio content of {Math.Max(1, cut.AudioEndMs - 150)}ms is already shorter than {cut.AudioEndMs}ms",
            },
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.ResponseStarted, ResponseId = "response-2" },
            Speech("response-2", "item-2", milliseconds: 40));

        await WaitUntilAsync(() => harness.Media.WrittenAudio.Count > writtenBefore || run.IsCompleted);

        // Assert
        // The answer to the caller still reached them, so the session was still there to give it.
        Assert.False(run.IsCompleted, "A refused truncation ended the call.");
        Assert.True(harness.Media.WrittenAudio.Count > writtenBefore);
        Assert.False(harness.LastContext.SessionLost);

        // And it was the same session: a refusal is not a reason to throw it away and open another.
        Assert.Equal(1, harness.Orchestrator.Starts);
        Assert.False(harness.Conversation.Disposed);

        await LetTheCallGoAsync(harness, run);
    }

    [Fact]
    public async Task ARefusedTruncation_IsSentAgain_WithinWhatTheProviderSaysTheLineHolds()
    {
        // Arrange
        // The provider's own count of a line's audio can be shorter than what was delivered for it. When it says
        // so, it also says how long the line really is, which is enough to cut it back to the same share of it.
        var harness = TalkedOverHarness(TalkingDbfs);
        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.SentAudio.Count >= CallerFrames);
        harness.Conversation.Queue(new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserSpeechStarted });
        await WaitUntilAsync(() => !harness.Conversation.Truncations.IsEmpty);
        var cut = harness.Conversation.Truncations.Single();
        var holds = Math.Max(1, cut.AudioEndMs - 100);

        // Act
        harness.Conversation.Queue(new RealtimeConversationEvent
        {
            Type = RealtimeConversationEventType.Error,
            ErrorMessage = $"Audio content of {holds}ms is already shorter than {cut.AudioEndMs}ms",
        });

        await WaitUntilAsync(() => harness.Conversation.Truncations.Count >= 2 || run.IsCompleted);

        // Assert
        var retried = harness.Conversation.Truncations.ElementAt(1);
        Assert.Equal("item-1", retried.ItemId);
        Assert.InRange(retried.AudioEndMs, 0, holds);

        // Scaled to the share of the 5 000 ms line that had played, so the caller's unheard tail stays cut.
        Assert.Equal((int)((long)holds * cut.AudioEndMs / 5_000), retried.AudioEndMs);

        await LetTheCallGoAsync(harness, run);
    }

    [Fact]
    public async Task ASessionLostMidCall_IsReconnected_WithTheConversationSoFar()
    {
        // Arrange
        var harness = new RealtimeHarness();
        harness.Media.KeepAlive = true;
        harness.Conversation.Queue(
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserTranscript, Text = "I want a pickup truck." },
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.AssistantTranscriptDone, Text = "Nice, new or used?" });

        // The first session's stream simply ends -- the provider closed it -- while the caller is still there.
        var second = new FakeRealtimeConversation { KeepAlive = true };
        harness.Orchestrator.Later.Enqueue(second);

        // Act
        var run = harness.RunAsync();
        await WaitUntilAsync(() => second.Unprompted.Count > 0 || run.IsCompleted);

        // Assert
        Assert.False(run.IsCompleted, "The call ended with the caller still on the line.");
        Assert.Equal(2, harness.Orchestrator.Requests.Count);
        Assert.True(harness.Conversation.Disposed);

        // The new session is told what has been said, so it picks up rather than starting the call over.
        var instructions = harness.Orchestrator.Contexts[1].SystemMessageBuilder.ToString();
        Assert.Contains("I want a pickup truck.", instructions);
        Assert.Contains("Nice, new or used?", instructions);
        Assert.Contains("introduce yourself again", Assert.Single(second.Unprompted));

        // And the caller's voice now goes to it.
        harness.Media.QueueCallerAudio(new byte[160]);
        await WaitUntilAsync(() => second.SentAudio.Count > 0);

        second.KeepAlive = false;
        await LetTheCallGoAsync(harness, run);
        Assert.False(harness.LastContext.SessionLost);
    }

    [Fact]
    public async Task ASessionThatCannotBeBroughtBack_IsReportedLost_RatherThanEndedQuietly()
    {
        // Arrange
        var harness = new RealtimeHarness();
        harness.Media.KeepAlive = true;
        harness.Orchestrator.FailAfterFirst = true;
        harness.Conversation.Queue(new RealtimeConversationEvent
        {
            Type = RealtimeConversationEventType.Error,
            ErrorMessage = "Your session hit the maximum duration of 60 minutes.",
        });
        harness.Conversation.KeepAlive = true;

        // Act
        var run = harness.RunAsync();
        var finished = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

        // Assert
        Assert.Same(run, finished);
        Assert.True(await run);
        Assert.True(harness.LastContext.SessionLost);
        Assert.True(harness.Orchestrator.Starts > 1, "Nothing tried to bring the session back.");
        Assert.True(harness.Media.Stopped);
    }

    [Fact]
    public async Task SendingToASessionWhoseSocketHasGone_BringsItBack_RatherThanEndingTheCall()
    {
        // Arrange
        var harness = new RealtimeHarness();
        harness.Media.KeepAlive = true;
        harness.Conversation.KeepAlive = true;
        harness.Conversation.FailSends = true;
        harness.Media.QueueCallerAudio(new byte[160]);

        var second = new FakeRealtimeConversation { KeepAlive = true };
        harness.Orchestrator.Later.Enqueue(second);

        // Act
        var run = harness.RunAsync();
        await WaitUntilAsync(() => second.Unprompted.Count > 0 || run.IsCompleted);

        // Assert
        Assert.False(run.IsCompleted, "A failed send ended the call.");
        Assert.Equal(2, harness.Orchestrator.Requests.Count);

        // The caller's pump kept reading the line through it, and now feeds the new session.
        harness.Media.QueueCallerAudio(new byte[160]);
        await WaitUntilAsync(() => second.SentAudio.Count > 0);

        second.KeepAlive = false;
        harness.Conversation.KeepAlive = false;
        await LetTheCallGoAsync(harness, run);
    }

    [Fact]
    public async Task ACallTheCallerEnded_IsNotReportedLost()
    {
        // Arrange
        var harness = new RealtimeHarness();
        harness.Conversation.KeepAlive = true;
        harness.Media.QueueCallerAudio(new byte[160]);

        // Act
        var run = harness.RunAsync();
        var finished = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

        // Assert
        Assert.Same(run, finished);
        Assert.False(harness.LastContext.SessionLost);
        Assert.Equal(1, harness.Orchestrator.Starts);
    }

    [Fact]
    public async Task ACallerWhoCutsInOnTheOpeningLine_IsAnswered_RatherThanGreetedAgain()
    {
        // Arrange
        // Live: the caller said "hello?" as they picked up, the opening was cut back to the half-second they had
        // heard, and the model -- told to open by introducing itself, and holding a record of an introduction one
        // word long -- introduced itself again, four times over.
        var harness = TalkedOverHarness(TalkingDbfs);
        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.SentAudio.Count >= CallerFrames);

        // Act
        harness.Conversation.Queue(
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserSpeechStarted },
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserTurnCommitted },
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserTranscript, Text = "Hello?" });
        await WaitUntilAsync(() => harness.StoredPrompts.Any(prompt => prompt.Content == "Hello?"));

        // Assert
        // The interrupted line is cut back rather than removed, so the model still knows it began its opening.
        var cut = Assert.Single(harness.Conversation.Truncations);
        Assert.Equal("item-1", cut.ItemId);
        Assert.True(cut.AudioEndMs > 0);

        // The caller's voice reached the model as they said it, not as silence.
        Assert.Contains(harness.Conversation.SentAudio, frame => frame.Any(sample => sample != 0));

        // Nothing asked the model to open the call a second time: its answer is to what the caller said.
        Assert.Single(harness.Conversation.Unprompted);

        // And it was told how to answer somebody who talks over it.
        var instructions = harness.Orchestrator.Contexts[0].SystemMessageBuilder.ToString();
        Assert.Contains(VoiceCallGuidance.WhenTalkedOverHeading, instructions);
        Assert.Contains(VoiceCallGuidance.WhenTalkedOver, instructions);

        await LetTheCallGoAsync(harness, run);
    }
}
