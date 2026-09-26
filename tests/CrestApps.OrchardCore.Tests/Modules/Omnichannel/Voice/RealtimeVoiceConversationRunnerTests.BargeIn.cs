using CrestApps.Core.AI.Realtime;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// A caller who talks over the assistant stops hearing it.
/// </summary>
/// <remarks>
/// Live, the caller talked over the assistant three times on one call and it kept talking every time. Their audio
/// reached the model and the provider stopped generating, but the model speaks faster than it plays, so seconds of
/// the sentence they interrupted were already waiting at the carrier and played out regardless.
/// </remarks>
public sealed partial class RealtimeVoiceConversationRunnerTests
{
    private const double TalkingDbfs = -20d;
    private const double EchoDbfs = -52d;

    [Fact]
    public async Task ACallerWhoTalksOverTheAssistant_StopsHearingIt()
    {
        // Arrange
        var harness = TalkedOverHarness(TalkingDbfs);

        // Act
        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.SentAudio.Count >= CallerFrames);
        harness.Conversation.Queue(new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserSpeechStarted });
        await WaitUntilAsync(() => harness.Media.Clears > 0);

        // Assert
        // What was queued on the line is discarded, and the model is told how much of its line was heard, so it
        // does not carry on as though the caller had heard all of it.
        Assert.Equal(1, harness.Media.Clears);
        var truncation = Assert.Single(harness.Conversation.Truncations);
        Assert.Equal("item-1", truncation.ItemId);
        Assert.InRange(truncation.AudioEndMs, 0, 4_999);

        await LetTheCallGoAsync(harness, run);
    }

    [Fact]
    public async Task TheRestOfALineTheCallerTalkedOver_IsNotPlayed_ButTheAnswerToThemIs()
    {
        // Arrange
        // The model's speech arrives in pieces. Discarding what was queued and then playing the next piece of the
        // same line restarts the assistant mid-sentence, after it had seemed to stop for the caller.
        var harness = TalkedOverHarness(TalkingDbfs);

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.SentAudio.Count >= CallerFrames);
        harness.Conversation.Queue(new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserSpeechStarted });
        await WaitUntilAsync(() => harness.Media.Clears > 0);
        var writtenWhenCleared = harness.Media.WrittenAudio.Count;

        // Act
        harness.Conversation.Queue(
            Speech("response-1", "item-1", milliseconds: 20),
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.ResponseStarted, ResponseId = "response-2" },
            Speech("response-2", "item-2", milliseconds: 40));

        await WaitUntilAsync(() => harness.Media.WrittenAudio.Count > writtenWhenCleared);

        // Assert
        // Only the answer reached the line: 40 ms of 8 kHz μ-law, not the late 20 ms of the interrupted line.
        Assert.Equal(writtenWhenCleared + 1, harness.Media.WrittenAudio.Count);
        Assert.Equal(320, harness.Media.WrittenAudio[^1].Length);

        await LetTheCallGoAsync(harness, run);
    }

    [Fact]
    public async Task ASpeechStartTheAssistantsOwnEchoCaused_DoesNotCutItOff()
    {
        // Arrange
        // The provider's detector can still fire on echo — the guard's held-back onset is released as heard, and
        // semantic detection is not level-based. The guard heard no voice over the assistant, so nobody did.
        var harness = TalkedOverHarness(EchoDbfs);

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.SentAudio.Count >= 10);

        var writtenBefore = harness.Media.WrittenAudio.Count;

        // Act
        harness.Conversation.Queue(
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserSpeechStarted },
            Speech("response-1", "item-1", milliseconds: 20));

        await WaitUntilAsync(() => harness.Media.WrittenAudio.Count > writtenBefore);

        // Assert
        // Nothing was discarded or cut, and the line carried on playing.
        Assert.Equal(0, harness.Media.Clears);
        Assert.Empty(harness.Conversation.Truncations);

        await LetTheCallGoAsync(harness, run);
    }

    [Fact]
    public async Task ACallerWhoSpeaksOnceTheAssistantHasFinished_HasNothingTakenBack()
    {
        // Arrange
        // Loud, and inside the echo tail so the guard is still watching, but the assistant's line has already
        // played out: there is nothing on the line to interrupt.
        var harness = new RealtimeHarness();
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;
        harness.Media.CallerAudioWaitsForTheAssistant = true;
        harness.Conversation.Queue(Speech("response-1", "item-1", milliseconds: 20));

        for (var i = 0; i < CallerFrames; i++)
        {
            harness.Media.QueueCallerAudio(LineTone(TalkingDbfs, i));
        }

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.SentAudio.Count >= CallerFrames);
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);

        // Act
        harness.Conversation.Queue(
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserSpeechStarted },
            Speech("response-2", "item-2", milliseconds: 20));

        await WaitUntilAsync(() => harness.Media.WrittenAudio.Count >= 2);

        // Assert
        Assert.Equal(0, harness.Media.Clears);
        Assert.Empty(harness.Conversation.Truncations);

        await LetTheCallGoAsync(harness, run);
    }

    [Fact]
    public async Task ACallerWhoTalksOverTheGoodbye_HearsItOut_AndStillKeepsTheCall()
    {
        // Arrange
        // A choice, not an accident. Goodbyes overlap on a phone — "thanks, bye" said over the assistant's own — and
        // clipping the closing line is exactly what the rest of the closing path exists to prevent. The line is
        // short, so it is allowed to finish; the caller still takes the call back, and the assistant answers once
        // it has.
        var harness = TalkedOverHarness(TalkingDbfs);
        using var endCall = new CancellationTokenSource();
        harness.EndCallRequested = endCall.Token;

        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Media.WrittenAudio.Count > 0);
        await endCall.CancelAsync();
        await WaitUntilAsync(() => harness.Conversation.SentAudio.Count >= CallerFrames);

        // Act
        harness.Conversation.Queue(
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserSpeechStarted },
            Speech("response-1", "item-1", milliseconds: 20));

        await WaitUntilAsync(() => harness.Media.WrittenAudio.Count >= 2);

        // Assert
        Assert.Equal(0, harness.Media.Clears);
        Assert.Empty(harness.Conversation.Truncations);

        // And the caller still has the call.
        await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        Assert.False(run.IsCompleted, "The caller spoke over the goodbye and was hung up on anyway.");

        await LetTheCallGoAsync(harness, run);
    }

    [Fact]
    public async Task TheAnswerToACallerWhoTalkedOverTheAssistant_FadesIn_RatherThanStartingOnAStep()
    {
        // Arrange
        // The line has just been cleared for the caller, so what they hear next starts from silence. A reply that
        // opens on a loud sample is a click at the start of the assistant's answer.
        var harness = TalkedOverHarness(TalkingDbfs);
        var run = harness.RunAsync();
        await WaitUntilAsync(() => harness.Conversation.SentAudio.Count >= CallerFrames);
        harness.Conversation.Queue(new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserSpeechStarted });
        await WaitUntilAsync(() => harness.Media.Clears > 0);
        var writtenWhenCleared = harness.Media.WrittenAudio.Count;

        var loud = new byte[RealtimeAudioConverter.RealtimeSampleRate * 2 * 40 / 1000];

        for (var i = 0; i < loud.Length; i += 2)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(loud.AsSpan(i, 2), 12_000);
        }

        // Act
        harness.Conversation.Queue(
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.ResponseStarted, ResponseId = "response-2" },
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.AssistantAudioDelta, ResponseId = "response-2", ItemId = "item-2", Audio = loud });
        await WaitUntilAsync(() => harness.Media.WrittenAudio.Count > writtenWhenCleared);

        // Assert
        var first = RealtimeAudioConverter.DecodeMuLawSample(harness.Media.WrittenAudio[writtenWhenCleared][0]);
        Assert.True(Math.Abs(first) < 1_000, $"The answer starts at {first}.");

        await LetTheCallGoAsync(harness, run);
    }

    private const int CallerFrames = 25;

    // Five seconds of the assistant's line handed over at once, the way the model delivers it, with the caller's
    // audio arriving while it plays. A short opening line goes first unless a test is about the opening itself,
    // because the opening is never cut back (see TheOpeningLine_IsTakenOffTheLine_ButNeverCutBack).
    private static RealtimeHarness TalkedOverHarness(double callerDbfs, bool afterTheOpening = true)
    {
        var harness = new RealtimeHarness();
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;
        harness.Media.CallerAudioWaitsForTheAssistant = true;

        if (afterTheOpening)
        {
            harness.Conversation.Queue(Speech("response-0", "item-0", milliseconds: 20));
        }

        harness.Conversation.Queue(Speech("response-1", "item-1", milliseconds: 5_000));

        for (var i = 0; i < CallerFrames; i++)
        {
            harness.Media.QueueCallerAudio(LineTone(callerDbfs, i));
        }

        return harness;
    }

    private static RealtimeConversationEvent Speech(string responseId, string itemId, int milliseconds)
        => new()
        {
            Type = RealtimeConversationEventType.AssistantAudioDelta,
            ResponseId = responseId,
            ItemId = itemId,
            Audio = new byte[RealtimeAudioConverter.RealtimeSampleRate * 2 * milliseconds / 1000],
        };

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var giveUpAt = DateTime.UtcNow.AddSeconds(10);

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < giveUpAt, "Timed out waiting for the call to reach the expected point.");

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    private static async Task LetTheCallGoAsync(RealtimeHarness harness, Task<bool> run)
    {
        harness.Conversation.KeepAlive = false;
        harness.Media.KeepAlive = false;

        await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
    }
}
