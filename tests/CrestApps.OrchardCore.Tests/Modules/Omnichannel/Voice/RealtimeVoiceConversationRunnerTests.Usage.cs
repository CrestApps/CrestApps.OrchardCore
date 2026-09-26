using CrestApps.Core.AI.Realtime;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// What a live session measures for the AI usage report, taken from the audio it actually plays and the voice
/// activity the provider reports, not from the transcript.
/// </summary>
public sealed partial class RealtimeVoiceConversationRunnerTests
{
    [Fact]
    public async Task TheSession_IsMeasuredFromWhenItHeldTheCall_ToWhenItEnded()
    {
        // Arrange
        var harness = new RealtimeHarness();
        var before = DateTime.UtcNow;

        // Act
        await harness.RunAsync();

        // Assert
        var measured = harness.Meter.Measure(endTicks: DateTime.MaxValue.Ticks);
        Assert.NotNull(measured);
        Assert.InRange(measured.StartedUtc, before, DateTime.UtcNow);

        // Stopped when the session ended, not whenever somebody happens to ask.
        Assert.InRange(measured.EndedUtc, measured.StartedUtc, DateTime.UtcNow);
    }

    [Fact]
    public async Task ASessionThatNeverHeldTheCall_MeasuresNothing()
    {
        // Arrange
        var harness = new RealtimeHarness(hasMediaProvider: false);

        // Act
        await harness.RunAsync();

        // Assert
        Assert.Null(harness.Meter.Measure());
    }

    [Fact]
    public async Task TheAssistantsSpeech_IsMeasuredAsItPlaysToTheCaller()
    {
        // Arrange
        // 300 ms of speech, handed over at once: it is 300 ms of talk time because that is how long it plays.
        var harness = new RealtimeHarness();
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;
        harness.Conversation.Queue(Speech("response-1", "item-1", milliseconds: 300));

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Media.WrittenAudio.Count > 0);
        await Task.Delay(TimeSpan.FromMilliseconds(600), TestContext.Current.CancellationToken);

        // Act
        await LetTheCallGoAsync(harness, run);

        // Assert
        var measured = harness.Meter.Measure();
        Assert.Equal(300, measured.AssistantSpeakingMs);
        Assert.NotNull(measured.TimeToFirstAssistantAudioMs);
    }

    [Fact]
    public async Task TheCallersSpeech_IsMeasuredFromWhenTheirVoiceWasDetected_ToWhenTheirTurnWasCommitted()
    {
        // Arrange
        var harness = new RealtimeHarness();
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.Unprompted.Count > 0);

        // Act
        harness.Conversation.Queue(new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserSpeechStarted });
        await Task.Delay(TimeSpan.FromMilliseconds(400), TestContext.Current.CancellationToken);
        harness.Conversation.Queue(new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserTurnCommitted, ItemId = "user-1" });
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        await LetTheCallGoAsync(harness, run);

        // Assert
        var measured = harness.Meter.Measure();
        Assert.InRange(measured.CallerSpeakingMs.Value, 300, 2_000);
        Assert.NotNull(measured.MutualSilenceMs);
    }

    [Fact]
    public async Task ACallerWhoTalksOverTheAssistant_IsCountedAsABargeIn_AndTheLineTheyNeverHeardIsNotTalkTime()
    {
        // Arrange
        // Five seconds of speech are queued, and the caller cuts in within the first second or two.
        var harness = TalkedOverHarness(TalkingDbfs);

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.SentAudio.Count >= CallerFrames);

        // Act
        harness.Conversation.Queue(new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserSpeechStarted });
        await WaitUntilAsync(() => harness.Media.Clears > 0);
        await LetTheCallGoAsync(harness, run);

        // Assert
        var measured = harness.Meter.Measure();
        Assert.Equal(1, measured.BargeIns);
        Assert.InRange(measured.AssistantSpeakingMs.Value, 0, 4_999);
    }

    [Fact]
    public async Task EchoTheGuardHeldBack_IsMeasured()
    {
        // Arrange
        // Quiet line audio while the assistant speaks is its own voice coming back, and is held back.
        var harness = TalkedOverHarness(EchoDbfs);

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.SentAudio.Count >= 10);

        // Act
        await LetTheCallGoAsync(harness, run);

        // Assert
        Assert.True(harness.Meter.Measure().EchoHeldMs > 0);
    }

    [Fact]
    public async Task AnErrorFromTheModel_IsRememberedAsAFailedSession()
    {
        // Arrange
        var harness = new RealtimeHarness();
        harness.Conversation.Queue(new RealtimeConversationEvent
        {
            Type = RealtimeConversationEventType.Error,
            ErrorMessage = "The session was closed by the provider.",
        });

        // Act
        await harness.RunAsync();

        // Assert
        Assert.True(harness.Meter.HasFailed);
    }
}
