using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.ContactCenter.Core.Telemetry;

/// <summary>
/// The features around a call: queue treatments and hold music, the hand-off from an AI voice agent to a person,
/// callbacks, supervision, transfers, recording and call quality.
/// </summary>
public static partial class ContactCenterDiagnostics
{
    private static readonly Counter<long> _treatmentsPlayed = Meter.CreateCounter<long>(
        "crestapps.contactcenter.treatment.played",
        "{step}",
        "The number of queue treatment steps a waiting caller heard, by step kind.");

    private static readonly Counter<long> _holdMusicStopFailures = Meter.CreateCounter<long>(
        "crestapps.contactcenter.hold_music.stop_failed",
        "{call}",
        "The number of times stopping a caller's hold music failed; the failure is swallowed so the caller still moves on.");

    private static readonly Histogram<double> _handoffDuration = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contactcenter.handoff.duration",
        "The time it took to hand a caller from an AI voice agent to the queue or an agent, by result. Its count is the number of hand-offs.");

    private static readonly Counter<long> _callbacks = Meter.CreateCounter<long>(
        "crestapps.contactcenter.callback",
        "{callback}",
        "The number of callbacks scheduled and promoted into a queue, by operation.");

    private static readonly Counter<long> _supervision = Meter.CreateCounter<long>(
        "crestapps.contactcenter.supervision",
        "{action}",
        "The number of supervisor interventions on live calls, by action and result.");

    private static readonly Counter<long> _transfers = Meter.CreateCounter<long>(
        "crestapps.contactcenter.transfer",
        "{transfer}",
        "The number of call transfers, by kind and result.");

    private static readonly Counter<long> _recordings = Meter.CreateCounter<long>(
        "crestapps.contactcenter.recording",
        "{action}",
        "The number of call recording actions, by action and result.");

    private static readonly Histogram<double> _callQualityMos = Meter.CreateHistogram<double>(
        "crestapps.contactcenter.call.quality.mos",
        "1",
        "The mean opinion score of call quality reports, from 1 (bad) to 5 (excellent).",
        tags: null,
        advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [1, 2, 2.5, 3, 3.5, 4, 4.3, 5] });

    private static readonly Histogram<double> _callQualityJitter = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contactcenter.call.quality.jitter",
        "The jitter of call quality reports.",
        [0.005, 0.01, 0.02, 0.03, 0.05, 0.075, 0.1, 0.2, 0.5]);

    private static readonly Histogram<double> _callQualityLoss = Meter.CreateHistogram<double>(
        "crestapps.contactcenter.call.quality.loss",
        "1",
        "The packet loss ratio of call quality reports, from 0 to 1.",
        tags: null,
        advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [0.001, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2] });

    private static readonly Counter<long> _callQualityAlerts = Meter.CreateCounter<long>(
        "crestapps.contactcenter.call.quality.alerts",
        "{alert}",
        "The number of poor-quality calls that crossed an agent's alert threshold; the alert itself is published once per agent per window.");

    /// <summary>
    /// Records that a waiting caller heard a queue treatment step.
    /// </summary>
    /// <param name="kind">The step kind.</param>
    internal static void RecordTreatmentPlayed(string kind)
    {
        if (_treatmentsPlayed.Enabled)
        {
            Add(_treatmentsPlayed, new TagList { { KindTag, kind } });
        }
    }

    /// <summary>
    /// Records that stopping a caller's hold music failed and was swallowed.
    /// </summary>
    /// <param name="exception">The exception it failed with.</param>
    internal static void RecordHoldMusicStopFailed(Exception exception)
    {
        AddExceptionEvent(exception);

        if (_holdMusicStopFailures.Enabled)
        {
            Add(_holdMusicStopFailures, new TagList { { CrestAppsOrchardCoreTelemetry.ErrorTypeTag, exception.GetType().FullName } });
        }
    }

    /// <summary>
    /// Records a hand-off an AI voice agent asked for.
    /// </summary>
    /// <param name="result">What became of the caller: <c>queued</c>, <c>offered</c>, <c>no_agents</c>, <c>closed</c>,
    /// <c>callback</c>, <c>refused</c> or <c>error</c>.</param>
    /// <param name="elapsed">The time the hand-off took.</param>
    internal static void RecordHandoff(string result, TimeSpan elapsed)
    {
        if (_handoffDuration.Enabled)
        {
            Record(_handoffDuration, elapsed.TotalSeconds, new TagList { { CrestAppsOrchardCoreTelemetry.ResultTag, result } });
        }
    }

    /// <summary>
    /// Records a callback scheduled or promoted.
    /// </summary>
    /// <param name="operation"><c>scheduled</c> or <c>promoted</c>.</param>
    /// <param name="count">How many.</param>
    internal static void RecordCallback(string operation, long count = 1)
    {
        if (count > 0 && _callbacks.Enabled)
        {
            Add(_callbacks, new TagList { { CrestAppsOrchardCoreTelemetry.OperationTag, operation } }, count);
        }
    }

    /// <summary>
    /// Records a supervisor intervention.
    /// </summary>
    /// <param name="action">The intervention, such as <c>monitor</c>, <c>whisper</c>, <c>barge</c> or <c>hangup</c>.</param>
    /// <param name="result"><c>ok</c> or <c>error</c>.</param>
    internal static void RecordSupervision(string action, string result)
    {
        if (_supervision.Enabled)
        {
            Add(_supervision, new TagList
            {
                { ActionTag, action },
                { CrestAppsOrchardCoreTelemetry.ResultTag, result },
            });
        }
    }

    /// <summary>
    /// Records a call transfer.
    /// </summary>
    /// <param name="kind">The kind of transfer, such as <c>warm</c>, <c>blind</c> or <c>consult</c>.</param>
    /// <param name="result">How it ended, from a bounded set.</param>
    internal static void RecordTransfer(string kind, string result)
    {
        if (_transfers.Enabled)
        {
            Add(_transfers, new TagList
            {
                { KindTag, kind },
                { CrestAppsOrchardCoreTelemetry.ResultTag, result },
            });
        }
    }

    /// <summary>
    /// Records a call recording action.
    /// </summary>
    /// <param name="action">The action, such as <c>start</c>, <c>pause</c>, <c>resume</c> or <c>stop</c>.</param>
    /// <param name="result"><c>ok</c> or <c>error</c>.</param>
    internal static void RecordRecording(string action, string result)
    {
        if (_recordings.Enabled)
        {
            Add(_recordings, new TagList
            {
                { ActionTag, action },
                { CrestAppsOrchardCoreTelemetry.ResultTag, result },
            });
        }
    }

    /// <summary>
    /// Records a call quality report.
    /// </summary>
    /// <param name="mos">The mean opinion score, when reported.</param>
    /// <param name="jitterSeconds">The jitter in seconds, when reported.</param>
    /// <param name="lossRatio">The packet loss ratio, when reported.</param>
    internal static void RecordCallQuality(double? mos, double? jitterSeconds, double? lossRatio)
    {
        if (mos is { } score && _callQualityMos.Enabled)
        {
            Record(_callQualityMos, score, default);
        }

        if (jitterSeconds is { } jitter && _callQualityJitter.Enabled)
        {
            Record(_callQualityJitter, jitter, default);
        }

        if (lossRatio is { } loss && _callQualityLoss.Enabled)
        {
            Record(_callQualityLoss, loss, default);
        }
    }

    /// <summary>
    /// Records a call quality alert.
    /// </summary>
    internal static void RecordCallQualityAlert()
    {
        if (_callQualityAlerts.Enabled)
        {
            Add(_callQualityAlerts, default);
        }
    }
}
