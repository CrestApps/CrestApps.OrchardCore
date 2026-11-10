using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Telemetry;

/// <summary>
/// The OpenTelemetry spans and metrics of automated AI voice calls: the live (realtime) session and the turn-based
/// loop, what the AI usage report measures on each call, and the call's media stream. The meter and activity source
/// are both named <see cref="SourceName"/>. Nothing is recorded, and next to nothing is spent, while no listener is
/// attached.
/// </summary>
/// <remarks>
/// Metrics carry the engine, the deployment (a configured name), the provider and bounded outcomes only. The activity,
/// session and call ids go on spans and logs; phone numbers and transcripts go nowhere.
/// </remarks>
public static class VoiceTelemetry
{
    /// <summary>
    /// The name of the meter and of the activity source.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".Voice";

    /// <summary>
    /// The tag that records the engine that held the call: <see cref="EngineRealtime"/> or <see cref="EngineTurnBased"/>.
    /// </summary>
    public const string EngineTag = "engine";

    /// <summary>
    /// The tag that records the AI deployment a call was held on, a configured name, or <c>unknown</c>.
    /// </summary>
    public const string DeploymentTag = "deployment";

    /// <summary>
    /// The tag that records what an idle prompt did: <see cref="IdleActionAsk"/> or <see cref="IdleActionGoodbye"/>.
    /// </summary>
    public const string ActionTag = "action";

    /// <summary>
    /// The tag that records the kind of a realtime session error.
    /// </summary>
    public const string KindTag = "kind";

    /// <summary>
    /// The tag that records which side of the conversation a guardrail checked: <c>customer</c> or <c>reply</c>.
    /// </summary>
    public const string DirectionTag = CrestAppsOrchardCoreTelemetry.DirectionTag;

    /// <summary>
    /// The span tag that holds the activity the call belongs to. Never a metric tag.
    /// </summary>
    public const string ActivityIdSpanTag = "crestapps.omnichannel.activity.id";

    /// <summary>
    /// The live speech-to-speech session.
    /// </summary>
    public const string EngineRealtime = "realtime";

    /// <summary>
    /// The turn-based loop: speech to text, a completion, text to speech.
    /// </summary>
    public const string EngineTurnBased = "turn_based";

    /// <summary>
    /// The assistant asked whether the caller was still there.
    /// </summary>
    public const string IdleActionAsk = "ask";

    /// <summary>
    /// The assistant said goodbye to a silent line and ended the call.
    /// </summary>
    public const string IdleActionGoodbye = "goodbye";

    /// <summary>
    /// The session or the call ended because its audio was lost, with the caller still on the line.
    /// </summary>
    public const string OutcomeLost = "lost";

    /// <summary>
    /// The live session never held the call, and the turn-based loop took it.
    /// </summary>
    public const string OutcomeNotHeld = "not_held";

    /// <summary>
    /// Gets the meter of AI voice.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("Voice");

    /// <summary>
    /// Gets the activity source of AI voice.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource("Voice");

    /// <summary>
    /// The number of automated calls summarized, once per call, tagged with <see cref="EngineTag"/>, the
    /// <c>outcome</c> (see <see cref="GetOutcome(AIVoiceSessionOutcome)"/>), <see cref="DeploymentTag"/> and the <c>provider</c>.
    /// </summary>
    public static Counter<long> Sessions { get; } = Meter.CreateCounter<long>(
        "crestapps.voice.ai.sessions",
        "{session}",
        "The number of automated AI voice calls, by engine, outcome and deployment.");

    /// <summary>
    /// The length of the assistant's part of a measured call, tagged with <see cref="EngineTag"/> and <see cref="DeploymentTag"/>.
    /// </summary>
    public static Histogram<double> SessionDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.voice.ai.session.duration",
        "The length of the assistant's part of an automated AI voice call.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// Each answer's time from the caller's turn to the assistant's first audio, tagged as <see cref="SessionDuration"/> is.
    /// </summary>
    public static Histogram<double> ResponseLatency { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.voice.ai.response.latency",
        "The time from the caller finishing a turn to the assistant's first audio of the answer.");

    /// <summary>
    /// Each answer's model time, tagged as <see cref="SessionDuration"/> is.
    /// </summary>
    public static Histogram<double> ModelLatency { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.voice.ai.model.latency",
        "The time the model took to answer a caller's turn.");

    /// <summary>
    /// How long the turn detector waited after the caller stopped, per answer where it was seen.
    /// </summary>
    public static Histogram<double> DetectorWait { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.voice.ai.detector.wait",
        "How long the turn detector waited after the caller stopped speaking before the turn was taken.");

    /// <summary>
    /// How long after the answer the caller first heard the assistant, tagged as <see cref="SessionDuration"/> is.
    /// </summary>
    public static Histogram<double> FirstAudioLatency { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.voice.ai.first_audio.latency",
        "The time from a call being answered to the caller first hearing the assistant.");

    /// <summary>
    /// The number of times callers talked over the assistant and it stopped, on live sessions.
    /// </summary>
    public static Counter<long> BargeIns { get; } = Meter.CreateCounter<long>(
        "crestapps.voice.ai.barge_ins",
        "{barge_in}",
        "The number of times a caller talked over the assistant and it stopped.");

    /// <summary>
    /// The number of call summaries for the AI usage report that could not be written, tagged with the <c>reason</c>:
    /// <c>scope</c> or <c>write</c>.
    /// </summary>
    public static Counter<long> SummariesLost { get; } = Meter.CreateCounter<long>(
        "crestapps.voice.ai.session.summary.lost",
        "{summary}",
        "The number of AI voice call summaries that could not be written.");

    /// <summary>
    /// The number of attempts to replace a live session lost with the caller still on the line, tagged with the <c>outcome</c>.
    /// </summary>
    public static Counter<long> SessionReconnects { get; } = Meter.CreateCounter<long>(
        "crestapps.voice.ai.session.reconnects",
        "{attempt}",
        "The number of attempts to replace a live AI voice session lost with the caller still on the line.");

    /// <summary>
    /// The number of errors live sessions reported, tagged with <see cref="KindTag"/> and the <c>result</c>:
    /// <c>continued</c> or <c>ended</c>.
    /// </summary>
    public static Counter<long> SessionErrors { get; } = Meter.CreateCounter<long>(
        "crestapps.voice.ai.session.errors",
        "{error}",
        "The number of errors live AI voice sessions reported, by kind and whether the session carried on.");

    /// <summary>
    /// The number of automated calls a machine answered, tagged with the <c>source</c> (<c>provider</c> when the
    /// provider's answering machine detection said so, <c>transcript</c> when the turn-based loop recognised a
    /// recorded greeting) and the <c>result</c>: <c>voicemail</c> or <c>phone_menu</c>.
    /// </summary>
    public static Counter<long> MachineDetections { get; } = Meter.CreateCounter<long>(
        "crestapps.voice.ai.voicemail.machine_detected",
        "{call}",
        "The number of automated calls a machine answered, by whether it was a voicemail or a phone menu.");

    /// <summary>
    /// The number of times the assistant spoke up on a line quiet on both sides, tagged with <see cref="EngineTag"/>
    /// and <see cref="ActionTag"/>.
    /// </summary>
    public static Counter<long> IdlePrompts { get; } = Meter.CreateCounter<long>(
        "crestapps.voice.ai.idle_prompts",
        "{prompt}",
        "The number of times the assistant spoke up because nobody had spoken for a while.");

    /// <summary>
    /// The number of times the assistant asked the caller to repeat an answer the live session never reported hearing.
    /// </summary>
    public static Counter<long> UnheardPrompts { get; } = Meter.CreateCounter<long>(
        "crestapps.voice.ai.unheard_prompts",
        "{prompt}",
        "The number of times the assistant asked a caller to repeat an answer the live session never heard.");

    /// <summary>
    /// The number of lines a profile's guardrails refused on calls, tagged with <see cref="EngineTag"/> and
    /// <see cref="DirectionTag"/>.
    /// </summary>
    public static Counter<long> GuardrailRefusals { get; } = Meter.CreateCounter<long>(
        "crestapps.voice.ai.guardrail.refusals",
        "{refusal}",
        "The number of caller lines or assistant replies a profile's guardrails refused on calls.");

    /// <summary>
    /// The time from a final transcript of the caller to the reply starting to play, on the turn-based loop, tagged
    /// with the <c>outcome</c>: <c>ok</c> for a reply, otherwise why there was none.
    /// </summary>
    public static Histogram<double> TurnDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.voice.ai.turn.duration",
        "The time from the caller's final transcript to the turn-based reply starting to play.");

    /// <summary>
    /// The number of call media streams open for live sessions, tagged with the <c>provider</c>.
    /// </summary>
    public static UpDownCounter<long> MediaStreams { get; } = Meter.CreateUpDownCounter<long>(
        "crestapps.voice.media.streams",
        "{stream}",
        "The number of call media streams currently open for live AI voice sessions.");

    /// <summary>
    /// The number of call media streams lost with the caller still on the line, tagged with the <c>provider</c>.
    /// </summary>
    public static Counter<long> MediaStreamsLost { get; } = Meter.CreateCounter<long>(
        "crestapps.voice.media.stream.lost",
        "{stream}",
        "The number of call media streams lost with the caller still on the line.");

    /// <summary>
    /// The number of caller audio frames a live session dropped instead of sending to the model, tagged with the
    /// <c>reason</c>.
    /// </summary>
    public static Counter<long> FramesDropped { get; } = Meter.CreateCounter<long>(
        "crestapps.voice.audio.frames.dropped",
        "{frame}",
        "The number of caller audio frames a live AI voice session dropped instead of sending to the model.");

    /// <summary>
    /// The number of times sending a live call's audio held the call back, tagged with the <c>direction</c>:
    /// <c>to_model</c> when sending the caller's audio to the session took 250 ms or more.
    /// </summary>
    public static Counter<long> SendStalls { get; } = Meter.CreateCounter<long>(
        "crestapps.voice.audio.send.stalls",
        "{stall}",
        "The number of times sending a live AI voice call's audio took long enough to hold the call back.");

    /// <summary>
    /// Gets the <c>outcome</c> tag value of how a conversation ended.
    /// </summary>
    /// <param name="outcome">How the conversation ended.</param>
    /// <returns>The bounded value, such as <c>completed_by_ai</c> or <c>handed_to_agent</c>.</returns>
    public static string GetOutcome(AIVoiceSessionOutcome outcome)
    {
        return outcome switch
        {
            AIVoiceSessionOutcome.CompletedByAI => "completed_by_ai",
            AIVoiceSessionOutcome.HandedToAgent => "handed_to_agent",
            AIVoiceSessionOutcome.Voicemail => "voicemail",
            AIVoiceSessionOutcome.NoAnswer => "no_answer",
            AIVoiceSessionOutcome.CallerHungUp => "caller_hung_up",
            AIVoiceSessionOutcome.Failed => "failed",
            AIVoiceSessionOutcome.Transferred => "transferred",
            _ => CrestAppsOrchardCoreTelemetry.Unknown,
        };
    }

    /// <summary>
    /// Gets the <see cref="EngineTag"/> value of an engine.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <returns><see cref="EngineRealtime"/> or <see cref="EngineTurnBased"/>.</returns>
    public static string GetEngine(AIVoiceSessionEngine engine)
        => engine == AIVoiceSessionEngine.Realtime ? EngineRealtime : EngineTurnBased;

    /// <summary>
    /// Records what is known about one summarized call. Called where the call's one summary is written, so a call is
    /// counted once: the session counter only for the first summary of a call, and the measurements only for the
    /// summary that carries them, which is written at most once per call.
    /// </summary>
    /// <param name="engine">The engine that held the call.</param>
    /// <param name="outcome">How the conversation ended.</param>
    /// <param name="deploymentName">The deployment the call was held on, a configured name, or <see langword="null"/>.</param>
    /// <param name="providerName">The provider that carried the call.</param>
    /// <param name="measured">What was measured on the call's audio, or <see langword="null"/>.</param>
    /// <param name="countSession">Whether this is the first summary of the call, which counts it.</param>
    public static void RecordSession(
        AIVoiceSessionEngine engine,
        AIVoiceSessionOutcome outcome,
        string deploymentName,
        string providerName,
        AIVoiceSessionMeasurements measured,
        bool countSession)
    {
        var tags = new TagList
        {
            { EngineTag, GetEngine(engine) },
            { DeploymentTag, string.IsNullOrWhiteSpace(deploymentName) ? CrestAppsOrchardCoreTelemetry.Unknown : deploymentName },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);

        if (countSession && Sessions.Enabled)
        {
            var sessionTags = tags;
            sessionTags.Add(CrestAppsOrchardCoreTelemetry.OutcomeTag, GetOutcome(outcome));
            sessionTags.Add(CrestAppsOrchardCoreTelemetry.ProviderTag, string.IsNullOrWhiteSpace(providerName) ? CrestAppsOrchardCoreTelemetry.Unknown : providerName);
            Sessions.Add(1, sessionTags);
        }

        if (measured is null)
        {
            return;
        }

        if (SessionDuration.Enabled)
        {
            SessionDuration.Record(measured.SessionDurationMs / 1000d, tags);
        }

        if (measured.TimeToFirstAssistantAudioMs is { } firstAudio && FirstAudioLatency.Enabled)
        {
            FirstAudioLatency.Record(firstAudio / 1000d, tags);
        }

        RecordEach(ResponseLatency, measured.ResponseLatenciesMs, tags);
        RecordEach(ModelLatency, measured.ModelLatenciesMs, tags);
        RecordEach(DetectorWait, measured.DetectorWaitsMs, tags);

        // A turn-based call stops listening while it speaks, so nobody can talk over it.
        if (engine == AIVoiceSessionEngine.Realtime && measured.BargeIns > 0 && BargeIns.Enabled)
        {
            BargeIns.Add(measured.BargeIns, tags);
        }
    }

    /// <summary>
    /// Records that a call's summary could not be written.
    /// </summary>
    /// <param name="reason"><c>scope</c> when no scope could be opened to write it, <c>write</c> when writing failed.</param>
    public static void RecordSummaryLost(string reason)
    {
        Add(SummariesLost, CrestAppsOrchardCoreTelemetry.ReasonTag, reason);
    }

    /// <summary>
    /// Records one attempt to replace a lost live session.
    /// </summary>
    /// <param name="outcome">The outcome of the attempt.</param>
    public static void RecordSessionReconnect(string outcome)
    {
        Add(SessionReconnects, CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome);
    }

    /// <summary>
    /// Records one error a live session reported.
    /// </summary>
    /// <param name="kind">The kind of error, from a bounded set such as <c>refused</c>, <c>unexpected</c>,
    /// <c>fatal</c> or <c>setup_refused</c>.</param>
    /// <param name="continued">Whether the session carried on.</param>
    public static void RecordSessionError(string kind, bool continued)
    {
        if (!SessionErrors.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { KindTag, kind },
            { CrestAppsOrchardCoreTelemetry.ResultTag, continued ? "continued" : "ended" },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        SessionErrors.Add(1, tags);
    }

    /// <summary>
    /// Records that a machine answered a call. Recorded once per call, where the loop first learns of it, whichever
    /// engine holds the call.
    /// </summary>
    /// <param name="source"><c>provider</c> or <c>transcript</c>.</param>
    /// <param name="phoneMenu">Whether it is a phone menu the assistant is getting through rather than a voicemail.</param>
    public static void RecordMachineDetected(string source, bool phoneMenu)
    {
        Add(MachineDetections, "source", source, CrestAppsOrchardCoreTelemetry.ResultTag, phoneMenu ? "phone_menu" : "voicemail");
    }

    /// <summary>
    /// Records that the assistant spoke up on a quiet line.
    /// </summary>
    /// <param name="engine"><see cref="EngineRealtime"/> or <see cref="EngineTurnBased"/>.</param>
    /// <param name="action"><see cref="IdleActionAsk"/> or <see cref="IdleActionGoodbye"/>.</param>
    public static void RecordIdlePrompt(string engine, string action)
    {
        Add(IdlePrompts, EngineTag, engine, ActionTag, action);
    }

    /// <summary>
    /// Records that the assistant asked the caller to repeat an answer the live session never heard.
    /// </summary>
    public static void RecordUnheardPrompt()
    {
        Add(UnheardPrompts, EngineTag, EngineRealtime);
    }

    /// <summary>
    /// Records that a guardrail refused a line on a call.
    /// </summary>
    /// <param name="engine"><see cref="EngineRealtime"/> or <see cref="EngineTurnBased"/>.</param>
    /// <param name="direction"><c>customer</c> or <c>reply</c>.</param>
    public static void RecordGuardrailRefusal(string engine, string direction)
    {
        Add(GuardrailRefusals, EngineTag, engine, DirectionTag, direction);
    }

    /// <summary>
    /// Records that a call media stream opened, or closed.
    /// </summary>
    /// <param name="providerName">The provider that carries the stream.</param>
    /// <param name="delta">One when it opened; minus one when it closed.</param>
    public static void RecordMediaStream(string providerName, int delta)
    {
        if (!MediaStreams.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ProviderTag, providerName ?? CrestAppsOrchardCoreTelemetry.Unknown },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        MediaStreams.Add(delta, tags);
    }

    /// <summary>
    /// Counts a call media stream as open on <see cref="MediaStreams"/> until the returned lease is disposed.
    /// </summary>
    /// <param name="providerName">The provider that carries the stream.</param>
    /// <returns>The lease; dispose it when the stream closes.</returns>
    public static IDisposable TrackMediaStream(string providerName)
    {
        // Only a stream counted when it opened is taken off when it closes, so a listener that attaches mid-call
        // never sees the count go below zero.
        var counted = MediaStreams.Enabled;

        if (counted)
        {
            RecordMediaStream(providerName, 1);
        }

        return new MediaStreamLease(providerName, counted);
    }

    /// <summary>
    /// Records that a call media stream was lost with the caller still on the line.
    /// </summary>
    /// <param name="providerName">The provider that carried the stream.</param>
    public static void RecordMediaStreamLost(string providerName)
    {
        Add(MediaStreamsLost, CrestAppsOrchardCoreTelemetry.ProviderTag, providerName ?? CrestAppsOrchardCoreTelemetry.Unknown);
    }

    /// <summary>
    /// Records caller audio frames a live session dropped.
    /// </summary>
    /// <param name="reason">Why, from a bounded set such as <c>no_session</c>.</param>
    /// <param name="count">How many frames.</param>
    public static void RecordFramesDropped(string reason, long count = 1)
    {
        if (count <= 0 || !FramesDropped.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ReasonTag, reason },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        FramesDropped.Add(count, tags);
    }

    /// <summary>
    /// Records that sending a live call's audio held the call back.
    /// </summary>
    /// <param name="direction">Which audio: <c>to_model</c> for the caller's audio sent to the session.</param>
    public static void RecordSendStall(string direction)
    {
        Add(SendStalls, DirectionTag, direction);
    }

    /// <summary>
    /// Starts a span of AI voice with no metric of its own, such as a session's setup or a handover.
    /// </summary>
    /// <param name="spanName">The name of the span.</param>
    /// <param name="activityId">The activity the call belongs to, set on the span only.</param>
    /// <returns>The scope of the span.</returns>
    public static TelemetryScope StartSpan(string spanName, string activityId)
    {
        var scope = TelemetryScope.Start(ActivitySource, spanName, duration: null);

        scope.SetSpanTag(ActivityIdSpanTag, activityId);

        return scope;
    }

    private sealed class MediaStreamLease : IDisposable
    {
        private readonly string _providerName;
        private int _counted;

        public MediaStreamLease(string providerName, bool counted)
        {
            _providerName = providerName;
            _counted = counted ? 1 : 0;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _counted, 0) == 1)
            {
                RecordMediaStream(_providerName, -1);
            }
        }
    }

    private static void RecordEach(Histogram<double> histogram, List<int> valuesMs, TagList tags)
    {
        if (valuesMs is null || valuesMs.Count == 0 || !histogram.Enabled)
        {
            return;
        }

        foreach (var valueMs in valuesMs)
        {
            histogram.Record(valueMs / 1000d, tags);
        }
    }

    private static void Add(Counter<long> counter, string key, object value, string secondKey = null, object secondValue = null)
    {
        if (!counter.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { key, value },
        };

        if (secondKey is not null)
        {
            tags.Add(secondKey, secondValue);
        }

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        counter.Add(1, tags);
    }
}
