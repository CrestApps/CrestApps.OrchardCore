using System.Runtime.CompilerServices;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Realtime;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
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

        // Not raised above the default. A higher threshold suppressed phantom turns but clipped the onset of
        // short quiet answers — a live "yeah" reached the model as a fragment and came back transcribed as an
        // unrelated sentence, which the assistant read as a brush-off and ended the call on. Mis-hearing the most
        // common thing a caller says costs more than the phantom turns it bought.
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
                RealtimeDeploymentName = "realtime-deployment",
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
            }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A realtime orchestrator that records what it was asked for and hands back a scripted conversation.
    /// </summary>
    private sealed class RecordingRealtimeOrchestrator : IRealtimeOrchestrator
    {
        private readonly IRealtimeConversation _conversation;

        public RecordingRealtimeOrchestrator(IRealtimeConversation conversation)
        {
            _conversation = conversation;
        }

        public List<RealtimeOrchestrationRequest> Requests { get; } = [];

        public bool Fail { get; set; }

        public Task<IRealtimeConversation> StartAsync(RealtimeOrchestrationRequest request, CancellationToken cancellationToken = default)
        {
            if (Fail)
            {
                throw new InvalidOperationException("The realtime session could not be opened.");
            }

            Requests.Add(request);

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

        public async IAsyncEnumerable<ContactCenterVoiceMediaFrame> ReadIncomingAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
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
