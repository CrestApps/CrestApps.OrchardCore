using System.Runtime.CompilerServices;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Chat.Models;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Realtime;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// The live speech-to-speech call: the caller's audio reaching the model as it arrives, the model's voice
/// reaching the caller as it is produced, and the transcript of both landing where everything downstream expects
/// to read it.
/// </summary>
public sealed class RealtimeVoiceConversationRunnerTests
{
    [Fact]
    public async Task CallerAudio_ReachesTheModel()
    {
        // Arrange
        var harness = new RealtimeHarness();
        harness.Media.QueueCallerAudio(new byte[160]);

        // Act
        await harness.RunAsync();

        // Assert
        Assert.Single(harness.Conversation.SentAudio);
    }

    [Fact]
    public async Task CallerAudio_IsConvertedToWhatTheModelSpeaks()
    {
        // Arrange
        // The line carries 8 kHz μ-law; the model expects 16-bit PCM at 24 kHz. Sending the line's bytes straight
        // through produces static the model transcribes as nothing.
        var harness = new RealtimeHarness();
        harness.Media.QueueCallerAudio(new byte[160]);

        // Act
        await harness.RunAsync();

        // Assert
        Assert.Equal(960, harness.Conversation.SentAudio.Single().Length);
    }

    [Fact]
    public async Task TheAssistantsVoice_ReachesTheCaller_InTheirLinesFormat()
    {
        // Arrange
        var harness = new RealtimeHarness();
        harness.Conversation.Queue(new RealtimeConversationEvent
        {
            Type = RealtimeConversationEventType.AssistantAudioDelta,
            Audio = new byte[960],
        });

        // Act
        await harness.RunAsync();

        // Assert
        Assert.Equal(160, harness.Media.WrittenAudio.Single().Length);
    }

    [Fact]
    public async Task WhatTheCallerSaid_IsRecordedOnTheTranscript()
    {
        // Arrange
        // Everything after the call — the summary, the disposition, the subject write-back — reads the transcript
        // rather than the audio, so a realtime call that recorded nothing would conclude as an empty call.
        var harness = new RealtimeHarness();
        harness.Conversation.Queue(new RealtimeConversationEvent
        {
            Type = RealtimeConversationEventType.UserTranscript,
            Text = "I am looking for a small SUV.",
        });

        // Act
        await harness.RunAsync();

        // Assert
        Assert.Contains(harness.StoredPrompts, prompt => prompt.Role == ChatRole.User && prompt.Content == "I am looking for a small SUV.");
    }

    [Fact]
    public async Task WhatTheAssistantSaid_IsRecordedOnTheTranscript()
    {
        // Arrange
        var harness = new RealtimeHarness();
        harness.Conversation.Queue(
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.AssistantTranscriptDelta, Text = "We have " },
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.AssistantTranscriptDelta, Text = "a few of those." },
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.AssistantTranscriptDone });

        // Act
        await harness.RunAsync();

        // Assert
        // The deltas are joined rather than stored one word at a time, so the transcript reads as sentences.
        Assert.Contains(harness.StoredPrompts, prompt => prompt.Role == ChatRole.Assistant && prompt.Content == "We have a few of those.");
    }

    [Fact]
    public async Task EveryTranscriptTurn_IsStampedWithTheTimeItHappened()
    {
        // Arrange
        // The transcript is read back in CreatedUtc order. An unstamped turn defaults to DateTime.MinValue, so a
        // whole realtime call collapses onto one instant and the call review is handed a conversation whose
        // speakers are interleaved arbitrarily — it then dispositions the call off that scrambled reading.
        var harness = new RealtimeHarness();
        harness.Conversation.Queue(
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserTranscript, Text = "I am looking for a small SUV." },
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.AssistantTranscriptDelta, Text = "We have a few of those." },
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.AssistantTranscriptDone });

        // Act
        await harness.RunAsync();

        // Assert
        Assert.Equal(2, harness.StoredPrompts.Count);
        Assert.All(harness.StoredPrompts, prompt => Assert.Equal(RealtimeHarness.Now, prompt.CreatedUtc));
    }

    [Fact]
    public async Task EachTurn_IsCommittedAsItHappensRatherThanAtTheEndOfTheCall()
    {
        // Arrange
        // A realtime session holds the call for its whole duration inside one scope. Without a per-turn flush the
        // writes sit uncommitted until the call ends, holding a write transaction open for minutes — which on
        // SQLite stalls the whole tenant, and cost us agent presence heartbeats during a two-minute call.
        var harness = new RealtimeHarness();
        harness.Conversation.Queue(
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserTranscript, Text = "I am looking for a small SUV." },
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserTranscript, Text = "Something under thirty grand." });

        // Act
        await harness.RunAsync();

        // Assert
        // One flush per turn, each seeing exactly the turns stored so far — not a single flush at the end.
        Assert.Equal([1, 2], harness.FlushedPromptCounts);
    }

    [Fact]
    public async Task TheSessionIsToldItIsOnATelephone_NotAHeadset()
    {
        // Arrange
        // This was never configured, so every call ran on provider defaults tuned for clean close-mic audio.
        // On an 8 kHz companded line those defaults had the model answering phantom turns and restarting its own
        // sentences — the caller's experience is an assistant talking to itself that never lets them speak.
        var harness = new RealtimeHarness();

        // Act
        await harness.RunAsync();

        // Assert
        var applied = Assert.Single(harness.Conversation.TurnDetectionUpdates);

        Assert.True(applied.AllowInterruption, "Being talked over is the other half of sounding like a machine.");
        Assert.True(applied.SilenceDurationMs >= 800, "A caller must be allowed to pause mid-sentence.");

        // Not raised above the default. A harder-to-trigger detector is what clips the onset of short quiet
        // answers — a live "yeah" reached the model as a fragment and came back transcribed as an unrelated
        // sentence, which the assistant read as a brush-off and ended the call on. Phantom turns are held back
        // before the detector instead, by the echo guard, which leaves the caller alone while the assistant is
        // quiet. (Under the default semantic detector the provider ignores this value anyway.)
        Assert.True(applied.VadThreshold <= 0.55f, "A short 'yeah' must not be clipped before the model hears it.");

        // The detector type belongs to the provider; naming one here would be a guess that fails closed.
        Assert.Null(applied.TurnDetectionType);
    }

    [Fact]
    public async Task WhenTheModelAsksToTransfer_TheSessionLetsGoOfTheCaller()
    {
        // Arrange
        // The whole point of a transfer is that the caller stops talking to the assistant. Before this, the
        // session ran until the caller hung up: they were told a person was coming and then kept chatting to the
        // bot, and the enqueue only happened when the call would have ended anyway (19s on one live call, 40s on
        // the next). The session must end on its own once the transfer is requested.
        var harness = new RealtimeHarness();
        using var handoff = new CancellationTokenSource();
        harness.HandoffRequested = handoff.Token;

        // The line and the model session both stay open, exactly as on a live call, so the only thing that can
        // end this call is the transfer itself.
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;
        harness.Conversation.Queue(new RealtimeConversationEvent
        {
            Type = RealtimeConversationEventType.AssistantTranscriptDone,
            Text = "Alright, connecting you with an agent right now.",
        });

        // Act
        var run = harness.RunAsync();
        await handoff.CancelAsync();

        // Assert
        // It returns without anyone hanging up, and within the closing grace rather than never.
        var completed = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

        Assert.Same(run, completed);
        Assert.True(await run);
        Assert.True(harness.Media.Stopped);
    }

    [Fact]
    public async Task ATransferStillLetsTheAssistantFinishSayingItIsTransferring()
    {
        // Arrange
        // The model invokes the tool and announces the transfer in the same breath, so cutting the audio the
        // instant the tool fires would clip "connecting you now" mid-word and drop the caller into silence.
        var harness = new RealtimeHarness();
        using var handoff = new CancellationTokenSource();
        harness.HandoffRequested = handoff.Token;
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        // Act
        var run = harness.RunAsync();
        await handoff.CancelAsync();

        // The closing line arrives after the transfer was requested, as it does on a real call.
        harness.Conversation.Queue(new RealtimeConversationEvent
        {
            Type = RealtimeConversationEventType.AssistantTranscriptDone,
            Text = "Alright, connecting you with an agent right now.",
        });

        await run;

        // Assert
        Assert.Contains(harness.StoredPrompts, prompt => prompt.Content == "Alright, connecting you with an agent right now.");
    }

    // Ending a call the model knows is over. A realtime session has no hangup marker to write — it is speaking,
    // not returning text — so before this it said goodbye and then held the line until the customer worked out
    // that nobody was going to hang up. Observed on a live call: the assistant finished, and the line stayed open
    // until the customer disconnected.

    [Fact]
    public async Task WhenTheModelEndsTheCall_TheLineIsClosedRatherThanLeftOpen()
    {
        // Arrange
        var harness = new RealtimeHarness();
        using var endCall = new CancellationTokenSource();
        harness.EndCallRequested = endCall.Token;

        // Both stay open, as on a live call, so the only thing that can end this is the closing itself.
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        // Act
        var run = harness.RunAsync();

        // The goodbye, then the tool: the assistant has spoken, so the session has something to wait for the end of.
        harness.Conversation.Queue(new RealtimeConversationEvent
        {
            Type = RealtimeConversationEventType.AssistantAudioDelta,
            Audio = new byte[320],
        });

        await endCall.CancelAsync();

        // Assert
        var completed = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

        Assert.Same(run, completed);
        Assert.True(await run);
        Assert.True(harness.Media.Stopped);
    }

    [Fact]
    public async Task ATransferredCall_StillEnds_ThoughTheModelNeverAskedToEndIt()
    {
        // Arrange
        // The closing watchdog waited on the end-call signal alone. On a transfer that signal never comes — the
        // model asked for a person, not for the call to end — so the watchdog never finished, and the teardown
        // waits for it. The session therefore never returned, and everything after it never ran: the caller was
        // told a person was coming and then left on a line nobody was ever going to be seated on. Every existing
        // test passed straight through this, because none of them supplied an end-call token at all.
        var harness = new RealtimeHarness();
        using var handoff = new CancellationTokenSource();
        using var endCall = new CancellationTokenSource();
        harness.HandoffRequested = handoff.Token;
        harness.EndCallRequested = endCall.Token;
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        // Act
        var run = harness.RunAsync();
        await handoff.CancelAsync();

        // Assert
        // It has to come back, so the handoff that follows it can happen at all.
        var completed = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

        Assert.Same(run, completed);
        Assert.True(await run);
    }

    [Fact]
    public async Task ACallTheCallerEnded_StillReturns_WithAnEndCallTokenThatNeverFired()
    {
        // Arrange
        // The same hazard by the commonest route of all: the caller simply hangs up. Nothing signals the end-call
        // token then either, and a session that cannot finish tearing down is a call whose outcome is never
        // written.
        var harness = new RealtimeHarness();
        using var endCall = new CancellationTokenSource();
        harness.EndCallRequested = endCall.Token;

        // Act
        var run = harness.RunAsync();

        // Assert
        var completed = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

        Assert.Same(run, completed);
        Assert.True(await run);
        Assert.True(harness.Media.Stopped);
    }

    [Fact]
    public async Task TheGoodbyeIsNotCutOff_WhileTheAssistantIsStillSayingIt()
    {
        // Arrange
        // The tool call and the closing line are one action to the model, and the tool usually lands first. Cutting
        // on the tool call would drop the caller into silence mid-word — the thing this whole path exists to avoid.
        var harness = new RealtimeHarness();
        using var endCall = new CancellationTokenSource();
        harness.EndCallRequested = endCall.Token;
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        // Act
        var run = harness.RunAsync();
        await endCall.CancelAsync();

        // Still talking: audio keeps arriving for longer than the listening grace would otherwise allow.
        for (var i = 0; i < 12; i++)
        {
            harness.Conversation.Queue(new RealtimeConversationEvent
            {
                Type = RealtimeConversationEventType.AssistantAudioDelta,
                Audio = new byte[320],
            });

            await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);

            // Assert
            // The call is still up at every point while the assistant is speaking.
            Assert.False(run.IsCompleted);
        }

        // Then it ends, once the speaking stops — bounded, so a regression that never closes the call fails here
        // rather than hanging the suite.
        var completed = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

        Assert.Same(run, completed);
        await run;
    }

    [Fact]
    public async Task TheGoodbyeIsNotCutOff_WhileItIsStillPlaying_ThoughItArrivedFasterThanItPlays()
    {
        // Arrange
        // The model delivers speech far faster than it plays: on a live call a seven-second goodbye arrived in
        // under three seconds, the end-call tool fired, and the line was closed four seconds after the last audio
        // *arrived* -- with the final second of the goodbye still queued, unheard. The test above streams audio
        // slower than it plays, so it could never see this. Here the whole closing line lands at once.
        const int ClosingLineSeconds = 6;
        var harness = new RealtimeHarness();
        using var endCall = new CancellationTokenSource();
        harness.EndCallRequested = endCall.Token;
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        // Act
        var run = harness.RunAsync();
        var spokenAt = DateTime.UtcNow;
        harness.Conversation.Queue(new RealtimeConversationEvent
        {
            Type = RealtimeConversationEventType.AssistantAudioDelta,

            // Sixteen-bit PCM at the realtime rate: two bytes per sample.
            Audio = new byte[RealtimeAudioConverter.RealtimeSampleRate * 2 * ClosingLineSeconds],
        });

        // The goodbye has been handed over in full before the tool call lands, as it was on the live call.
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        await endCall.CancelAsync();

        // Assert
        // Still up while the goodbye is playing: nothing may close the line before the caller has heard it out.
        var stillPlayingUntil = spokenAt.AddSeconds(ClosingLineSeconds - 1) - DateTime.UtcNow;
        await Task.Delay(stillPlayingUntil, TestContext.Current.CancellationToken);
        Assert.False(run.IsCompleted, "The call was closed while the goodbye was still playing to the caller.");

        // And it still ends once the goodbye and the caller's moment after it are over.
        var completed = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

        Assert.Same(run, completed);
        await run;
    }

    [Fact]
    public async Task AVoicemail_IsHungUpOnOnceTheMessageHasPlayed_RatherThanLeftRecordingSilence()
    {
        // Arrange
        // A person is given a moment after the goodbye to add something. A recording has nobody to give it to, and
        // on a live call that moment -- and the wait before it -- ended up as silence at the end of the voicemail
        // the customer listened to.
        const int MessageSeconds = 2;
        var harness = new RealtimeHarness();
        using var endCall = new CancellationTokenSource();
        harness.EndCallRequested = endCall.Token;
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        // Act
        var run = harness.RunAsync();
        var spokenAt = DateTime.UtcNow;
        harness.Conversation.Queue(new RealtimeConversationEvent
        {
            Type = RealtimeConversationEventType.AssistantAudioDelta,
            Audio = new byte[RealtimeAudioConverter.RealtimeSampleRate * 2 * MessageSeconds],
        });

        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        harness.ReachedVoicemail = true;
        await endCall.CancelAsync();

        // Assert
        // The message itself is not cut short...
        await Task.Delay(spokenAt.AddSeconds(MessageSeconds - 0.5) - DateTime.UtcNow, TestContext.Current.CancellationToken);
        Assert.False(run.IsCompleted, "The voicemail message was cut off before it had played.");

        // ...but the line is closed well before a person's moment to answer would have run out after it.
        var completed = await Task.WhenAny(run, Task.Delay(spokenAt.AddSeconds(MessageSeconds + 2.5) - DateTime.UtcNow, TestContext.Current.CancellationToken));

        Assert.Same(run, completed);
        await run;
    }

    [Fact]
    public async Task AGoodbyeAlreadySaid_IsNotWaitedForAgain_WhenTheCallIsEndedLater()
    {
        // Arrange
        // When nothing is being said at the moment the model ends the call, the goodbye is behind it and anything
        // the model says next is suppressed as a repeat. The closing watchdog still waited for a goodbye to begin
        // -- one that could never be heard -- and held the line open on silence for the full wait. Live, that was
        // four more seconds of nothing recorded at the end of a voicemail.
        var harness = new RealtimeHarness();
        using var endCall = new CancellationTokenSource();
        harness.EndCallRequested = endCall.Token;
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        // Act
        var run = harness.RunAsync();
        harness.Conversation.Queue(new RealtimeConversationEvent
        {
            Type = RealtimeConversationEventType.AssistantAudioDelta,
            Audio = new byte[320],
        });
        harness.Conversation.Queue(new RealtimeConversationEvent
        {
            Type = RealtimeConversationEventType.AssistantTranscriptDone,
            Text = "Thanks, take care.",
        });

        // Long enough after the goodbye that the caller's moment to answer it has already passed.
        await Task.Delay(TimeSpan.FromSeconds(4.5), TestContext.Current.CancellationToken);
        var endedAt = DateTime.UtcNow;
        await endCall.CancelAsync();

        // Assert
        var completed = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

        Assert.Same(run, completed);
        await run;
        Assert.True(DateTime.UtcNow - endedAt < TimeSpan.FromSeconds(2), "The call was held open waiting for a goodbye that had already been said.");
    }

    [Fact]
    public async Task TheAssistantOpensTheCall_EvenOnASessionThatAnswersByItself()
    {
        // Arrange
        // We placed this call, so the silence after the customer picks up is ours to fill. It has to be the
        // unprompted request rather than the ordinary one: a session with no grounding answers by itself, and
        // the ordinary request is refused for those -- which is why every live call opened with the customer
        // saying "Hello?" into silence.
        var harness = new RealtimeHarness();
        harness.Conversation.RespondsAutomatically = true;

        // Act
        await harness.RunAsync();

        // Assert
        Assert.Single(harness.Conversation.Unprompted);
    }

    [Fact]
    public async Task WhenNobodyHasSpokenForTooLong_TheAssistantAsksWhetherTheCallerIsStillThere()
    {
        // Arrange
        // The live case this exists for: a customer answers "yes", the provider returns no transcript for it, and
        // both sides then wait -- the assistant for a turn it never saw, the customer for an answer to something
        // they believe they already gave. Somebody has to speak, and it is not going to be the customer.
        var harness = new RealtimeHarness();
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var run = harness.RunAsync();

        // Act
        // Nothing is said by anybody, for longer than a pause for thought.
        await Task.Delay(TimeSpan.FromSeconds(14), TestContext.Current.CancellationToken);

        // Assert
        // The opening line, and then the prompt that breaks the silence.
        Assert.Equal(2, harness.Conversation.Unprompted.Count);
        Assert.Contains("still there", harness.Conversation.Unprompted[^1], StringComparison.OrdinalIgnoreCase);

        harness.Conversation.KeepAlive = false;
        harness.Media.KeepAlive = false;

        await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheClosingLine_IsNotSaidTwice()
    {
        // Arrange
        // The model says goodbye, calls the end-call tool, reads the tool's reply, and — with the line still open
        // while the customer is given their moment — says the very same goodbye again. Heard live, twice.
        var harness = new RealtimeHarness();
        using var endCall = new CancellationTokenSource();
        harness.EndCallRequested = endCall.Token;
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var run = harness.RunAsync();

        // The order a live call actually produces: the model finishes saying the goodbye, and only then calls the
        // tool. Cancelling first was the easy ordering, and it hid the bug.
        harness.Conversation.Queue(
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.AssistantAudioDelta, Audio = new byte[320] },
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.AssistantTranscriptDone, Text = "Understood, you won't be contacted again. Take care." });

        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);

        await endCall.CancelAsync();

        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
        var spokenAfterGoodbye = harness.Media.WrittenAudio.Count;

        // Act
        // The model says it again.
        harness.Conversation.Queue(
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.AssistantAudioDelta, Audio = new byte[320] },
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.AssistantTranscriptDone, Text = "Understood, you won't be contacted again. Take care." });

        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        // Assert
        // The customer hears the goodbye once, and the record says it was said once.
        Assert.Equal(spokenAfterGoodbye, harness.Media.WrittenAudio.Count);
        Assert.Single(harness.StoredPrompts, prompt => prompt.Role == ChatRole.Assistant);

        harness.Conversation.KeepAlive = false;
        harness.Media.KeepAlive = false;

        await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ACustomerWhoStartsSpeakingAfterTheGoodbye_IsNotHungUpOn()
    {
        // Arrange
        // The one the other test could not catch. A transcript only exists once the customer has stopped talking
        // and the provider has transcribed them, which is seconds after they opened their mouth -- by which time
        // the line was already cut. Voice detection fires on the first syllable, and that is the moment the call
        // stops being over.
        var harness = new RealtimeHarness();
        using var endCall = new CancellationTokenSource();
        harness.EndCallRequested = endCall.Token;
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        // Act
        var run = harness.RunAsync();

        harness.Conversation.Queue(new RealtimeConversationEvent
        {
            Type = RealtimeConversationEventType.AssistantAudioDelta,
            Audio = new byte[320],
        });

        await endCall.CancelAsync();

        // They start talking, and say nothing the provider has finished transcribing yet.
        harness.Conversation.Queue(new RealtimeConversationEvent
        {
            Type = RealtimeConversationEventType.UserSpeechStarted,
        });

        // Well past the window in which the call would otherwise have been ended.
        await Task.Delay(TimeSpan.FromSeconds(6), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(run.IsCompleted);

        // Let the call go, so the session does not outlive the test.
        harness.Conversation.KeepAlive = false;
        harness.Media.KeepAlive = false;

        await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ACustomerWhoSpeaksAfterTheGoodbye_IsNotHungUpOn()
    {
        // Arrange
        // "Actually, one more thing" — said in the breath after goodbye, which is when people say it. The model
        // already decided the call was over; the customer decides otherwise, and the customer is right.
        var harness = new RealtimeHarness();
        using var endCall = new CancellationTokenSource();
        harness.EndCallRequested = endCall.Token;
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        // Act
        var run = harness.RunAsync();

        harness.Conversation.Queue(new RealtimeConversationEvent
        {
            Type = RealtimeConversationEventType.AssistantAudioDelta,
            Audio = new byte[320],
        });

        await endCall.CancelAsync();

        harness.Conversation.Queue(new RealtimeConversationEvent
        {
            Type = RealtimeConversationEventType.UserTranscript,
            Text = "Actually, one more thing.",
        });

        // Well past the window in which the call would otherwise have been ended.
        await Task.Delay(TimeSpan.FromSeconds(4), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(run.IsCompleted);

        // Let the call go, so the session does not outlive the test.
        harness.Conversation.KeepAlive = false;
        harness.Media.KeepAlive = false;

        await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ACustomerWhoAnsweredTheGoodbye_IsStillHungUpOn_OnceTheModelEndsTheCallAgain()
    {
        // Arrange
        // Live: the model said goodbye and ended the call, the customer said "bye", the model ended it again and
        // said "bye, take care" -- and the line stayed open until the customer hung up, because the watch had
        // stopped for good when the customer spoke and the end-call signal only ever fires once.
        var turn = new VoiceCallEndTurn();
        var harness = new RealtimeHarness
        {
            EndCallRequested = turn.EndCallRequestedToken,
            EndCallRequests = () => turn.RequestCount,
        };
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var run = harness.RunAsync();

        harness.Conversation.Queue(
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.AssistantAudioDelta, Audio = new byte[320] },
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.AssistantTranscriptDone, Text = "I'll send those options over. Talk soon." });

        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        turn.RequestEndCall("customer has what they needed");

        // The customer answers the goodbye.
        harness.Conversation.Queue(
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserSpeechStarted },
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserTranscript, Text = "Bye." });

        await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        var writtenBeforeTheLastGoodbye = harness.Media.WrittenAudio.Count;

        // Act
        // The model ends the call first and says its last line after, as it did live.
        turn.RequestEndCall("customer said goodbye");
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);

        harness.Conversation.Queue(
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.AssistantAudioDelta, Audio = new byte[320] },
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.AssistantTranscriptDone, Text = "Bye, take care." });

        var ended = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken)) == run;

        // Assert
        // The last goodbye is heard rather than muted as a repeat, and then the call is ended for the customer.
        Assert.True(ended);
        Assert.True(harness.Media.WrittenAudio.Count > writtenBeforeTheLastGoodbye);
        Assert.Contains(harness.StoredPrompts, prompt => prompt.Role == ChatRole.Assistant && prompt.Content == "Bye, take care.");

        harness.Conversation.KeepAlive = false;
        harness.Media.KeepAlive = false;
    }

    [Fact]
    public async Task AGoodbyeSaidAfterTheModelEndsTheCall_IsHeard_WhenItHasNotAnsweredTheCustomerYet()
    {
        // Arrange
        // The model does not always speak before it calls the tool. With nothing in flight, the line it says next
        // was taken for a repeat of a goodbye it had never said, and the customer's "bye" was met with silence.
        var turn = new VoiceCallEndTurn();
        var harness = new RealtimeHarness
        {
            EndCallRequested = turn.EndCallRequestedToken,
            EndCallRequests = () => turn.RequestCount,
        };
        harness.Conversation.KeepAlive = true;
        harness.Media.KeepAlive = true;

        var run = harness.RunAsync();

        harness.Conversation.Queue(
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserSpeechStarted },
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.UserTranscript, Text = "That's all, bye." });

        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        var writtenBefore = harness.Media.WrittenAudio.Count;

        // Act
        turn.RequestEndCall("customer said goodbye");

        harness.Conversation.Queue(
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.AssistantAudioDelta, Audio = new byte[320] },
            new RealtimeConversationEvent { Type = RealtimeConversationEventType.AssistantTranscriptDone, Text = "Bye, take care." });

        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(harness.Media.WrittenAudio.Count > writtenBefore);
        Assert.Contains(harness.StoredPrompts, prompt => prompt.Role == ChatRole.Assistant && prompt.Content == "Bye, take care.");

        harness.Conversation.KeepAlive = false;
        harness.Media.KeepAlive = false;

        await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheSessionIsGivenTheToolsACallNeeds()
    {
        // Arrange
        // A realtime session is configured once, from the profile, and never sees the per-turn tool wiring the
        // turn-based path does. Before this, the model on a live call had no way to end the call and no way to
        // escalate: the loop read a handoff flag that nothing on a realtime call could ever set.
        var harness = new RealtimeHarness();
        harness.HandoffInstructions = "Hand off when the customer asks for a human.";

        // Act
        await harness.RunAsync();

        // Assert
        var orchestration = harness.Orchestrator.Contexts.Single();

        Assert.Contains(EndCallTool.ToolName, orchestration.MustIncludeTools);
        Assert.Contains(OmnichannelHandoffHelper.TransferToAgentToolName, orchestration.MustIncludeTools);
        Assert.Contains("Hand off when the customer asks for a human.", orchestration.SystemMessageBuilder.ToString());
    }

    [Fact]
    public async Task TheSessionIsToldWhoItIsCalling()
    {
        // Arrange
        // A live session is configured from the profile and never sees the greeting template that has the contact
        // in scope. Told no name, a model does not open a sales call without one — it invents a plausible one.
        // Observed live: "is this Marcus?" to a contact named Amani, who asked who it was looking for, which the
        // assistant read as a request for a human and transferred the call. One invented word cost the call.
        var harness = new RealtimeHarness();
        harness.ContactName = "Amani";

        // Act
        await harness.RunAsync();

        // Assert
        var systemMessage = harness.Orchestrator.Contexts.Single().SystemMessageBuilder.ToString();

        Assert.Contains("Amani", systemMessage);
        Assert.Contains("never guess or invent one", systemMessage);
    }

    [Fact]
    public async Task TheSessionIsToldThatAnOptOutEndsTheCall_RatherThanTransferringThem()
    {
        // Arrange
        // On a live call the customer asked not to be called. Speech recognition delivered "call me", the model
        // read it as a request to be connected, and transferred them — the one response an opt-out must never
        // get. The session is told how this arrives and which way to resolve the ambiguity.
        var harness = new RealtimeHarness();

        // Act
        await harness.RunAsync();

        // Assert
        var systemMessage = harness.Orchestrator.Contexts.Single().SystemMessageBuilder.ToString();

        Assert.Contains("that is an opt-out and it ends the call", systemMessage);
        Assert.Contains("Never transfer somebody who is trying to end contact", systemMessage);
    }

    [Fact]
    public async Task ACallToSomebodyWithNoName_IsToldToUseNoneRatherThanInventOne()
    {
        // Arrange
        // The dangerous half: with no name available the model must be told to use none. Silence on the subject is
        // what produced the invented name in the first place.
        var harness = new RealtimeHarness();
        harness.ContactName = null;

        // Act
        await harness.RunAsync();

        // Assert
        var systemMessage = harness.Orchestrator.Contexts.Single().SystemMessageBuilder.ToString();

        Assert.Contains("Do not use a name", systemMessage);
        Assert.Contains("never guess or invent one", systemMessage);
    }

    [Fact]
    public async Task ACallWithNowhereToEscalate_IsNotOfferedTheTransferTool()
    {
        // Arrange
        // Telling a model it may transfer, on a call where nothing can receive the caller, has it promise a
        // person who is never coming. Ending the call is always available; escalating is not.
        var harness = new RealtimeHarness();
        harness.HandoffInstructions = null;

        // Act
        await harness.RunAsync();

        // Assert
        var orchestration = harness.Orchestrator.Contexts.Single();

        Assert.Contains(EndCallTool.ToolName, orchestration.MustIncludeTools);
        Assert.DoesNotContain(OmnichannelHandoffHelper.TransferToAgentToolName, orchestration.MustIncludeTools);
    }

    [Fact]
    public async Task WithNoTransferRequested_TheSessionRunsToItsNaturalEnd()
    {
        // Arrange
        // A context that never signals a handoff must behave exactly as before: the default token cannot be
        // cancelled, and registering on it must not end the call early.
        var harness = new RealtimeHarness();

        // Act
        var ran = await harness.RunAsync();

        // Assert
        Assert.True(ran);
        Assert.True(harness.Media.Stopped);
    }

    [Fact]
    public async Task TheSessionIsAskedFor_WithTheProfilesRealtimeDeploymentAndTheActivitysVoice()
    {
        // Arrange
        var harness = new RealtimeHarness();
        harness.Activity.TextToSpeechVoiceId = "chosen-voice";

        // Act
        await harness.RunAsync();

        // Assert
        var request = harness.Orchestrator.Requests.Single();
        Assert.Equal("realtime-deployment", request.RealtimeDeploymentName);
        Assert.Equal("chosen-voice", request.Voice);
        Assert.Same(harness.Session, request.ChatSession);
    }

    [Fact]
    public async Task WhenTheBatchChoseNoVoice_TheProfilesVoiceIsUsed()
    {
        // Arrange
        // A batch only carries a voice when whoever loaded the inventory picked one, which is the exception. The
        // runner read only the activity, so the voice an operator selected on the profile was thrown away and
        // every call used the model's default — with the profile still showing the voice they had chosen.
        var harness = new RealtimeHarness();
        harness.Activity.TextToSpeechVoiceId = null;
        harness.Profile.AlterSettings<ChatModeProfileSettings>(s => s.VoiceName = "coral");

        // Act
        await harness.RunAsync();

        // Assert
        Assert.Equal("coral", harness.Orchestrator.Requests.Single().Voice);
    }

    [Fact]
    public async Task TheBatchesVoice_StillWinsOverTheProfiles()
    {
        // Arrange
        // A campaign that deliberately picked a different voice must keep it.
        var harness = new RealtimeHarness();
        harness.Activity.TextToSpeechVoiceId = "shimmer";
        harness.Profile.AlterSettings<ChatModeProfileSettings>(s => s.VoiceName = "coral");

        // Act
        await harness.RunAsync();

        // Assert
        Assert.Equal("shimmer", harness.Orchestrator.Requests.Single().Voice);
    }

    [Fact]
    public async Task WithNoVoiceChosenAnywhere_TheDeploymentDecides()
    {
        // Arrange
        var harness = new RealtimeHarness();
        harness.Activity.TextToSpeechVoiceId = null;

        // Act
        await harness.RunAsync();

        // Assert
        Assert.Null(harness.Orchestrator.Requests.Single().Voice);
    }

    [Fact]
    public async Task ACallerWhoTalksOverTheAssistant_IsHeard()
    {
        // Arrange
        // Being talked through is the most common complaint about automated calls, and the only thing that makes
        // one feel like a recording rather than a conversation.
        var harness = new RealtimeHarness();

        // Act
        await harness.RunAsync();

        // Assert
        Assert.True(harness.Orchestrator.Requests.Single().AllowInterruption);
    }

    [Fact]
    public async Task TheAssistantsOwnVoice_ComingBackUpTheLine_IsNotHeardAsTheCaller()
    {
        // Arrange
        // Heard live: the greeting leaked back up the caller's line, faint and garbled, and the model transcribed it
        // as the caller saying "Bye-bye." — then answered it, and did the same with "you" after its next line. To
        // the person holding the phone, the assistant was talking to itself.
        var harness = new RealtimeHarness();
        harness.Conversation.KeepAlive = true;
        harness.Media.CallerAudioWaitsForTheAssistant = true;
        harness.Conversation.Queue(new RealtimeConversationEvent
        {
            Type = RealtimeConversationEventType.AssistantAudioDelta,

            // Five seconds of speech, so the whole of the caller's audio below arrives while it is still playing.
            Audio = new byte[RealtimeAudioConverter.RealtimeSampleRate * 2 * 5],
        });

        for (var i = 0; i < 50; i++)
        {
            harness.Media.QueueCallerAudio(LineTone(-52d, i));
        }

        // Act
        await harness.RunAsync();

        // Assert
        var sent = harness.Conversation.SentAudio.SelectMany(chunk => chunk).ToArray();

        Assert.NotEmpty(sent);
        Assert.All(sent, value => Assert.Equal(0, value));
    }

    [Fact]
    public async Task ACallerWhoTalksOverTheAssistant_ReachesTheModel_AsTheySaidIt()
    {
        // Arrange
        // The other half of the echo guard, and the one that must never regress: a person talking over the
        // assistant is heard, in full, while it is still speaking.
        var harness = new RealtimeHarness();
        harness.Conversation.KeepAlive = true;
        harness.Media.CallerAudioWaitsForTheAssistant = true;
        harness.Conversation.Queue(new RealtimeConversationEvent
        {
            Type = RealtimeConversationEventType.AssistantAudioDelta,
            Audio = new byte[RealtimeAudioConverter.RealtimeSampleRate * 2 * 5],
        });

        var frames = Enumerable.Range(0, 25).Select(i => LineTone(-20d, i)).ToArray();

        foreach (var frame in frames)
        {
            harness.Media.QueueCallerAudio(frame);
        }

        // Act
        await harness.RunAsync();

        // Assert
        var expected = frames
            .SelectMany(frame => RealtimeAudioConverter.ToRealtime(frame, harness.Media.IncomingFormat).ToArray())
            .ToArray();

        Assert.Equal(expected, harness.Conversation.SentAudio.SelectMany(chunk => chunk).ToArray());
    }

    [Fact]
    public async Task ASoftHello_BeforeTheAssistantHasSaidAnything_ReachesTheModelAsSaid()
    {
        // Arrange
        // The echo guard must only ever act on audio that could be the assistant coming back. At the start of the
        // call it has said nothing, so a quiet "hello?" from somebody picking up is theirs and goes through
        // untouched — even though the silence clocks are stamped the moment the session opens.
        var harness = new RealtimeHarness();
        harness.Conversation.KeepAlive = true;

        var frames = Enumerable.Range(0, 15).Select(i => LineTone(-44d, i)).ToArray();

        foreach (var frame in frames)
        {
            harness.Media.QueueCallerAudio(frame);
        }

        // Act
        await harness.RunAsync();

        // Assert
        var expected = frames
            .SelectMany(frame => RealtimeAudioConverter.ToRealtime(frame, harness.Media.IncomingFormat).ToArray())
            .ToArray();

        Assert.Equal(expected, harness.Conversation.SentAudio.SelectMany(chunk => chunk).ToArray());
    }

    // One 20 ms frame of a 300 Hz tone at the given RMS level, as the line carries it: 8 kHz μ-law.
    private static byte[] LineTone(double dbfs, int index)
    {
        const int sampleRate = 8_000;
        const int count = 160;

        var amplitude = 32767d * Math.Pow(10, dbfs / 20d) * Math.Sqrt(2);
        var frame = new byte[count];

        for (var n = 0; n < count; n++)
        {
            var t = (index * count + n) / (double)sampleRate;
            var sample = (short)Math.Round(amplitude * Math.Sin(2 * Math.PI * 300 * t));
            frame[n] = RealtimeAudioConverter.EncodeMuLawSample(sample);
        }

        return frame;
    }

    [Fact]
    public async Task WithNoProviderThatCanCarryLiveAudio_TheCallIsLeftToTheTurnBasedLoop()
    {
        // Arrange
        // Reporting this rather than failing is what lets the caller hear the older, slower conversation instead
        // of silence.
        var harness = new RealtimeHarness(hasMediaProvider: false);

        // Act
        var ran = await harness.RunAsync();

        // Assert
        Assert.False(ran);
        Assert.Empty(harness.Orchestrator.Requests);
    }

    [Fact]
    public async Task WhenTheSessionCannotStart_TheCallIsLeftToTheTurnBasedLoop()
    {
        // Arrange
        var harness = new RealtimeHarness();
        harness.Orchestrator.Fail = true;

        // Act
        var ran = await harness.RunAsync();

        // Assert
        Assert.False(ran);
    }

    [Fact]
    public async Task WhenTheCallEnds_TheMediaSessionIsStopped()
    {
        // Arrange
        // A media session left open holds a provider socket and, on some providers, keeps billing.
        var harness = new RealtimeHarness();

        // Act
        await harness.RunAsync();

        // Assert
        Assert.True(harness.Media.Stopped);
    }

    [Fact]
    public async Task AnErrorFromTheModel_EndsTheSessionRatherThanHangingTheCall()
    {
        // Arrange
        var harness = new RealtimeHarness();
        harness.Conversation.Queue(new RealtimeConversationEvent
        {
            Type = RealtimeConversationEventType.Error,
            ErrorMessage = "The session was closed by the provider.",
        });

        // Act
        var ran = await harness.RunAsync();

        // Assert
        Assert.True(ran);
        Assert.True(harness.Media.Stopped);
    }

    private sealed class RealtimeHarness
    {
        /// <summary>
        /// The instant the harness clock reports, so a test can assert a turn was stamped with it.
        /// </summary>
        public static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly List<AIChatSessionPrompt> _prompts = [];
        private readonly List<int> _flushedPromptCounts = [];

        public RealtimeHarness(bool hasMediaProvider = true)
        {
            Activity = new OmnichannelActivity { ItemId = "activity-1" };

            Session = new AIChatSession { SessionId = "session-1", ProfileId = "profile-1" };

            Profile = new AIProfile
            {
                ItemId = "profile-1",
                ChatDeploymentName = "realtime-deployment",
            };

            var mediaProvider = new Mock<IContactCenterVoiceMediaProvider>();
            mediaProvider.SetupGet(x => x.TechnicalName).Returns("Fake");
            mediaProvider.Setup(x => x.OpenSessionAsync(It.IsAny<ContactCenterVoiceMediaSessionRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Media);

            var mediaResolver = new Mock<IContactCenterVoiceMediaProviderResolver>();
            mediaResolver.Setup(x => x.Get(It.IsAny<string>()))
                .Returns(hasMediaProvider ? mediaProvider.Object : null);

            var promptStore = new Mock<IAIChatSessionPromptStore>();
            promptStore.Setup(x => x.CreateAsync(It.IsAny<AIChatSessionPrompt>(), It.IsAny<CancellationToken>()))
                .Callback<AIChatSessionPrompt, CancellationToken>((prompt, _) => _prompts.Add(prompt));

            var sessionManager = new Mock<IAIChatSessionManager>();

            // Count the flushes and snapshot how many turns had been stored at each one, so a test can prove the
            // transcript is committed turn by turn rather than accumulating in one long-lived write transaction.
            DocumentSession = new Mock<ISession>();
            DocumentSession.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Callback<CancellationToken>(_ => _flushedPromptCounts.Add(_prompts.Count))
                .Returns(Task.CompletedTask);

            var clock = new Mock<IClock>();
            clock.SetupGet(x => x.UtcNow).Returns(Now);

            Runner = new RealtimeVoiceConversationRunner(
                Orchestrator,
                mediaResolver.Object,
                promptStore.Object,
                sessionManager.Object,
                DocumentSession.Object,
                clock.Object,
                NullLogger<RealtimeVoiceConversationRunner>.Instance);
        }

        public Mock<ISession> DocumentSession { get; }

        /// <summary>
        /// The number of stored prompts observed at each flush, in flush order.
        /// </summary>
        public IReadOnlyList<int> FlushedPromptCounts => _flushedPromptCounts;

        public OmnichannelActivity Activity { get; }

        public AIProfile Profile { get; }

        public AIChatSession Session { get; }

        public FakeMediaSession Media { get; } = new();

        public FakeRealtimeConversation Conversation { get; } = new();

        public RecordingRealtimeOrchestrator Orchestrator => field ??= new RecordingRealtimeOrchestrator(Conversation);

        public RealtimeVoiceConversationRunner Runner { get; }

        /// <summary>
        /// The token the loop passes when the transfer tool fires.
        /// </summary>
        public CancellationToken HandoffRequested { get; set; }

        /// <summary>
        /// The token the loop passes when the end-call tool fires.
        /// </summary>
        public CancellationToken EndCallRequested { get; set; }

        /// <summary>
        /// Whether the model said, as it ended the call, that it had reached voicemail.
        /// </summary>
        public bool ReachedVoicemail { get; set; }

        /// <summary>
        /// How many times the model has asked to end the call, as the loop reports it from the end-call turn.
        /// </summary>
        public Func<int> EndCallRequests { get; set; }

        /// <summary>
        /// The escalation guidance the loop passes when this call has an agent queue behind it.
        /// </summary>
        public string HandoffInstructions { get; set; }

        /// <summary>
        /// Who the call is to, as the loop resolves it from the contact.
        /// </summary>
        public string ContactName { get; set; }

        public List<AIChatSessionPrompt> StoredPrompts => _prompts;

        public Task<bool> RunAsync()
            => Runner.RunAsync(new RealtimeVoiceConversationContext
            {
                Activity = Activity,
                Profile = Profile,
                Session = Session,
                ProviderName = "Fake",
                ProviderCallId = "call-1",
                HandoffRequested = HandoffRequested,
                EndCallRequested = EndCallRequested,
                ReachedVoicemail = () => ReachedVoicemail,
                EndCallRequests = EndCallRequests,
                HandoffInstructions = HandoffInstructions,
                ContactName = ContactName,

                // The loop decides this now, by asking whether the profile's chat deployment can hold a live call.
                RealtimeDeploymentName = "realtime-deployment",
            }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A realtime orchestrator that records what it was asked for and hands back a scripted conversation.
    /// </summary>
    [Fact]
    public async Task TheSession_IsStartedInsideAnActiveInvocationScope()
    {
        // Arrange
        // The orchestrator refuses to start a session that carries tools without one, because tools read their
        // context from that scope. Live, that refusal ended the call before the assistant said a word.
        var harness = new RealtimeHarness();

        // Act
        await harness.RunAsync();

        // Assert
        Assert.NotNull(harness.Orchestrator.InvocationScopeAtStart);
    }

    private sealed class RecordingRealtimeOrchestrator : IRealtimeOrchestrator
    {
        private readonly IRealtimeConversation _conversation;

        public RecordingRealtimeOrchestrator(IRealtimeConversation conversation)
        {
            _conversation = conversation;
        }

        public List<RealtimeOrchestrationRequest> Requests { get; } = [];

        /// <summary>
        /// The contexts the caller configured, so a test can assert what the session was actually built with.
        /// </summary>
        public List<OrchestrationContext> Contexts { get; } = [];

        public bool Fail { get; set; }

        /// <summary>
        /// The invocation context that was current when the session was started, which the real orchestrator
        /// refuses to start a tool-carrying session without.
        /// </summary>
        public AIInvocationContext InvocationScopeAtStart { get; private set; }

        public Task<IRealtimeConversation> StartAsync(RealtimeOrchestrationRequest request, CancellationToken cancellationToken = default)
        {
            InvocationScopeAtStart = AIInvocationScope.Current;

            if (Fail)
            {
                throw new InvalidOperationException("The realtime session could not be opened.");
            }

            Requests.Add(request);

            // The real orchestrator builds a context and hands it to the caller to configure. Doing the same here
            // is what lets a test see the tools and guidance a live session would have been given.
            var context = new OrchestrationContext
            {
                CompletionContext = new AICompletionContext(),
            };

            request.ConfigureContext?.Invoke(context);

            Contexts.Add(context);

            return Task.FromResult(_conversation);
        }
    }

    /// <summary>
    /// A realtime conversation that replays scripted events and records the audio sent to it.
    /// </summary>
    /// <summary>
    /// One call to <c>UpdateTurnDetectionAsync</c>, so a test can assert how the session was tuned.
    /// </summary>
    private sealed record TurnDetectionUpdate(
        bool AllowInterruption,
        int? SilenceDurationMs,
        float? VadThreshold,
        string TurnDetectionType);

    private sealed class FakeRealtimeConversation : IRealtimeConversation
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<RealtimeConversationEvent> _events = new();

        public List<byte[]> SentAudio { get; } = [];

        /// <summary>
        /// When set, the stream stays open after the queued events run out, the way a real session does — it ends
        /// only when the call is cancelled. Needed to prove something else closed the session.
        /// </summary>
        public bool KeepAlive { get; set; }

        public void Queue(params RealtimeConversationEvent[] events)
        {
            foreach (var conversationEvent in events)
            {
                _events.Enqueue(conversationEvent);
            }
        }

        /// <summary>
        /// The session drives its own turns unless a test says otherwise, which is how the provider behaves.
        /// </summary>
        public bool RespondsAutomatically { get; set; } = true;

        public List<string> Grounded { get; } = [];

        public Task<bool> GroundTurnAsync(string utterance, CancellationToken cancellationToken = default)
        {
            Grounded.Add(utterance);

            return Task.FromResult(true);
        }

        /// <summary>
        /// How many times the host asked the model to speak without being spoken to first.
        /// </summary>
        public int ResponsesRequested { get; private set; }

        public Task RequestResponseAsync(CancellationToken cancellationToken = default)
        {
            // Exactly what the real conversation does. A session the provider already answers for refuses this,
            // because on a turn it has answered itself the extra response would be a duplicate. Counting the call
            // regardless is what let a fix that cannot work on such a session pass here.
            if (RespondsAutomatically)
            {
                return Task.CompletedTask;
            }

            ResponsesRequested++;

            return Task.CompletedTask;
        }

        /// <summary>
        /// What the session asked the model to say without being prompted by the caller, in order.
        /// </summary>
        public List<string> Unprompted { get; } = [];

        public Task RequestUnpromptedResponseAsync(string instructions = null, CancellationToken cancellationToken = default)
        {
            Unprompted.Add(instructions ?? string.Empty);

            return Task.CompletedTask;
        }

        public Task RequestAcknowledgementAsync(string instructions, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendAudioAsync(ReadOnlyMemory<byte> audio, CancellationToken cancellationToken = default)
        {
            SentAudio.Add(audio.ToArray());

            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<RealtimeConversationEvent> GetEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_events.TryDequeue(out var conversationEvent))
                {
                    yield return conversationEvent;

                    continue;
                }

                if (!KeepAlive)
                {
                    yield break;
                }

                // Idle the way a live session does between turns, so a test can observe what closes it.
                await Task.Delay(10, cancellationToken);
            }
        }

        public Task TruncateAssistantAudioAsync(string itemId, int audioEndMs, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        /// <summary>
        /// What the session was told about how to decide whose turn it is.
        /// </summary>
        public List<TurnDetectionUpdate> TurnDetectionUpdates { get; } = [];

        public Task UpdateTurnDetectionAsync(bool allowInterruption, int? silenceDurationMs, float? vadThreshold, string turnDetectionType, CancellationToken cancellationToken = default)
        {
            TurnDetectionUpdates.Add(new TurnDetectionUpdate(allowInterruption, silenceDurationMs, vadThreshold, turnDetectionType));

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }

    /// <summary>
    /// A media session carrying 8 kHz μ-law, which is what a telephone call actually carries.
    /// </summary>
    private sealed class FakeMediaSession : IContactCenterVoiceMediaSession
    {
        private readonly List<byte[]> _incoming = [];

        public string SessionId => "media-1";

        public string ProviderCallId => "call-1";

        public ContactCenterVoiceMediaFormat IncomingFormat { get; } = new()
        {
            Encoding = ContactCenterVoiceMediaEncoding.MuLaw,
            SampleRate = 8_000,
        };

        public ContactCenterVoiceMediaFormat OutgoingFormat { get; } = new()
        {
            Encoding = ContactCenterVoiceMediaEncoding.MuLaw,
            SampleRate = 8_000,
        };

        public List<byte[]> WrittenAudio { get; } = [];

        public bool Stopped { get; private set; }

        /// <summary>
        /// When set, the caller's stream stays open after the queued frames run out, the way a real line does
        /// until somebody hangs up. Without it the call tears down the instant the fake runs dry, which makes any
        /// test about what ends a call pass for the wrong reason.
        /// </summary>
        public bool KeepAlive { get; set; }

        public void QueueCallerAudio(byte[] frame)
            => _incoming.Add(frame);

        /// <summary>
        /// When set, the caller's audio is held until the assistant's voice has been written to the line, so a test
        /// can put the caller's audio in the window where it plays rather than racing it.
        /// </summary>
        public bool CallerAudioWaitsForTheAssistant { get; set; }

        private readonly TaskCompletionSource _assistantSpoke = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async IAsyncEnumerable<ContactCenterVoiceMediaFrame> ReadIncomingAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (CallerAudioWaitsForTheAssistant)
            {
                await _assistantSpoke.Task.WaitAsync(cancellationToken);
            }

            foreach (var frame in _incoming)
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return new ContactCenterVoiceMediaFrame { Data = frame };
            }

            while (KeepAlive)
            {
                await Task.Delay(10, cancellationToken);
            }

            await Task.CompletedTask;
        }

        public ValueTask WriteOutgoingAsync(ContactCenterVoiceMediaFrame frame, CancellationToken cancellationToken = default)
        {
            WrittenAudio.Add(frame.Data.ToArray());
            _assistantSpoke.TrySetResult();

            return ValueTask.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            Stopped = true;

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }
}
