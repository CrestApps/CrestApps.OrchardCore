using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Telemetry;

/// <summary>
/// The call funnel: every provider voice event the call stream ingests, from Telnyx, Asterisk or any other provider,
/// and what a call came to when it ended.
/// </summary>
public static partial class ContactCenterDiagnostics
{
    /// <summary>
    /// The name of the span of one provider voice event ingested into the call stream.
    /// </summary>
    internal const string CallIngestSpanName = "crestapps.contactcenter.call.ingest";

    /// <summary>
    /// The event changed the call.
    /// </summary>
    internal const string ProviderEventApplied = "applied";

    /// <summary>
    /// The event was a provider redelivery of one already ingested.
    /// </summary>
    internal const string ProviderEventDuplicate = "duplicate";

    /// <summary>
    /// The event was older than what the call already knew, so ordering discarded it. It is set on the span only; the
    /// discard is counted on <c>crestapps.telephony.voice_event.discards</c>.
    /// </summary>
    internal const string ProviderEventStale = "stale";

    /// <summary>
    /// The event came from a provider that does not own the call it named.
    /// </summary>
    internal const string ProviderEventForeign = "foreign";

    /// <summary>
    /// The event named a call no interaction is tracking.
    /// </summary>
    internal const string ProviderEventUnmatched = "unmatched";

    /// <summary>
    /// The event named a call other than the one its interaction's session is bound to, and was refused.
    /// </summary>
    internal const string ProviderEventConflict = "conflict";

    /// <summary>
    /// Applying the event lost a concurrency race and it was retried in a fresh scope.
    /// </summary>
    internal const string ProviderEventRetried = "retried";

    private static readonly Counter<long> _providerEvents = Meter.CreateCounter<long>(
        "crestapps.contactcenter.provider_event",
        "{event}",
        "The number of provider voice events the call stream ingested, by provider, reported state and what became of each.");

    private static readonly Histogram<double> _providerEventLag = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contactcenter.provider_event.lag",
        "The time from when the provider saw a call change to when the call stream ingested it: the webhook and queueing lag.");

    private static readonly Histogram<double> _callSetupDuration = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contactcenter.call.setup.duration",
        "The time from when a call started dialing or ringing to when it was answered.");

    private static readonly Histogram<double> _callDuration = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contactcenter.call.duration",
        "The time from when a call started to when it ended.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    private static readonly Counter<long> _callsCompleted = Meter.CreateCounter<long>(
        "crestapps.contactcenter.call.completed",
        "{call}",
        "The number of calls that ended, by direction, hangup cause and who hung up.");

    private static readonly Counter<long> _callsAbandoned = Meter.CreateCounter<long>(
        "crestapps.contactcenter.call.abandoned",
        "{call}",
        "The number of inbound calls whose caller hung up before an agent answered, by the stage they hung up in.");

    private static readonly Counter<long> _amdResults = Meter.CreateCounter<long>(
        "crestapps.contactcenter.amd.result",
        "{call}",
        "The number of answered outbound calls classified by answering machine detection, by classification.");

    /// <summary>
    /// Starts the span of one provider voice event. The provider and the reported state are bounded and also set on
    /// the span; the call is named on the span only by its interaction once it is matched.
    /// </summary>
    /// <param name="providerName">The canonical provider name.</param>
    /// <param name="state">The state the provider reported.</param>
    /// <returns>The span, or <see langword="null"/> when nothing listens.</returns>
    internal static Activity StartCallIngest(string providerName, VoiceCallState state)
    {
        var activity = ActivitySource.StartActivity(CallIngestSpanName, ActivityKind.Internal);

        if (activity is not null)
        {
            activity.SetTag(CrestAppsOrchardCoreTelemetry.ProviderTag, GetProvider(providerName));
            activity.SetTag(StateTag, state.ToString());
        }

        return activity;
    }

    /// <summary>
    /// Records what became of one provider voice event, and how late it arrived.
    /// </summary>
    /// <param name="activity">The span of the event, or <see langword="null"/>.</param>
    /// <param name="providerName">The canonical provider name.</param>
    /// <param name="state">The state the provider reported.</param>
    /// <param name="result">What became of it: one of the <c>ProviderEvent*</c> values.</param>
    /// <param name="lag">The time from the provider's own timestamp to now, when the provider sent one.</param>
    /// <param name="count">Whether to count the event on <c>crestapps.contactcenter.provider_event</c>. A discard is
    /// counted on the shared telephony discard instrument instead, so it is never counted twice.</param>
    internal static void RecordProviderEvent(
        Activity activity,
        string providerName,
        VoiceCallState state,
        string result,
        TimeSpan? lag,
        bool count = true)
    {
        activity?.SetTag(CrestAppsOrchardCoreTelemetry.ResultTag, result);

        if (count && _providerEvents.Enabled)
        {
            Add(_providerEvents, new TagList
            {
                { CrestAppsOrchardCoreTelemetry.ProviderTag, GetProvider(providerName) },
                { StateTag, state.ToString() },
                { CrestAppsOrchardCoreTelemetry.ResultTag, result },
            });
        }

        if (lag is { } elapsed && _providerEventLag.Enabled)
        {
            Record(_providerEventLag, Math.Max(0, elapsed.TotalSeconds), new TagList
            {
                { CrestAppsOrchardCoreTelemetry.ProviderTag, GetProvider(providerName) },
            });
        }
    }

    /// <summary>
    /// Records what a call came to on the event that ended it: its setup time, its duration, how it ended and, for an
    /// inbound call nobody answered, the stage the caller gave up in.
    /// </summary>
    /// <param name="session">The call session, already moved to its terminal state.</param>
    /// <param name="interaction">The interaction the call belongs to.</param>
    /// <param name="hangupSource">Who hung up, as the provider reported it, or <see langword="null"/>.</param>
    internal static void RecordCallEnded(CallSession session, Interaction interaction, string hangupSource)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(interaction);

        var direction = GetDirection(session.Direction);
        var provider = GetProvider(session.ProviderName);
        var answeredUtc = session.AnsweredUtc ?? interaction.AnsweredUtc;
        var startedUtc = session.StartedUtc ?? session.CreatedUtc;
        var endedUtc = session.EndedUtc ?? interaction.EndedUtc;

        if (_callsCompleted.Enabled)
        {
            Add(_callsCompleted, new TagList
            {
                { CrestAppsOrchardCoreTelemetry.DirectionTag, direction },
                { CrestAppsOrchardCoreTelemetry.ProviderTag, provider },
                { HangupCauseTag, (session.HangupCause ?? HangupCause.Unknown).ToString() },
                { HangupSourceTag, GetHangupSource(hangupSource) },
            });
        }

        if (answeredUtc is { } answered && startedUtc != default && _callSetupDuration.Enabled)
        {
            Record(_callSetupDuration, Math.Max(0, (answered - startedUtc).TotalSeconds), new TagList
            {
                { CrestAppsOrchardCoreTelemetry.DirectionTag, direction },
                { CrestAppsOrchardCoreTelemetry.ProviderTag, provider },
            });
        }

        if (endedUtc is { } ended && startedUtc != default && _callDuration.Enabled)
        {
            Record(_callDuration, Math.Max(0, (ended - startedUtc).TotalSeconds), new TagList
            {
                { CrestAppsOrchardCoreTelemetry.DirectionTag, direction },
                { CrestAppsOrchardCoreTelemetry.ProviderTag, provider },
                { "answered", answeredUtc.HasValue },
            });
        }

        // An inbound caller nobody answered gave up somewhere: ringing an agent who had been offered the call, waiting
        // in a queue, or before routing put them anywhere (an announcement, a menu).
        if (session.Direction == InteractionDirection.Inbound &&
            !answeredUtc.HasValue &&
            session.HangupCause is null or HangupCause.NormalClearing or HangupCause.Canceled &&
            _callsAbandoned.Enabled)
        {
            var stage = !string.IsNullOrEmpty(interaction.AgentId)
                ? "offer"
                : !string.IsNullOrEmpty(interaction.QueueId)
                    ? "queue"
                    : "pre_routing";

            Add(_callsAbandoned, new TagList
            {
                { StageTag, stage },
                { KindTag, GetRoutingKind(interaction.QueueId) },
            });
        }
    }

    /// <summary>
    /// Records the answering machine detection result of an outbound call, once, when it is first classified.
    /// </summary>
    /// <param name="classification">The classification the provider reported.</param>
    internal static void RecordAnswerClassification(AnswerClassification classification)
    {
        if (!_amdResults.Enabled)
        {
            return;
        }

        var result = classification switch
        {
            AnswerClassification.Human => "human",
            AnswerClassification.Machine => "machine",
            AnswerClassification.Fax => "fax",
            _ => CrestAppsOrchardCoreTelemetry.Unknown,
        };

        Add(_amdResults, new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
        });
    }

    /// <summary>
    /// Maps the party a provider says hung up to a bounded value.
    /// </summary>
    /// <param name="hangupSource">The provider's value, such as Telnyx's <c>caller</c> or <c>callee</c>.</param>
    /// <returns><c>caller</c>, <c>callee</c>, <c>agent</c>, <c>supervisor</c>, <c>system</c>, <c>unknown</c> or <c>other</c>.</returns>
    internal static string GetHangupSource(string hangupSource)
    {
        if (string.IsNullOrWhiteSpace(hangupSource))
        {
            return CrestAppsOrchardCoreTelemetry.Unknown;
        }

        return hangupSource.Trim().ToLowerInvariant() switch
        {
            "caller" => "caller",
            "callee" => "callee",
            "agent" => "agent",
            "supervisor" => "supervisor",
            "system" or "platform" => "system",
            "unknown" => CrestAppsOrchardCoreTelemetry.Unknown,
            _ => CrestAppsOrchardCoreTelemetry.Other,
        };
    }
}
