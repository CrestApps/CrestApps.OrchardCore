using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Telephony.Telemetry;

/// <summary>
/// The OpenTelemetry spans and metrics every telephony provider shares: provider commands, dial screening, browser
/// credentials, call reconciliation, recording ingest and the ordering of provider voice events. The meter and the
/// activity source are both named <see cref="SourceName"/>; a provider's own transport (its REST API, its webhooks)
/// is measured by the provider's own meter.
/// </summary>
/// <remarks>
/// Metrics carry the provider's technical name and bounded values only. Call ids, phone numbers and SIP URIs never go
/// on a metric.
/// </remarks>
public static class TelephonyTelemetry
{
    /// <summary>
    /// The name of the meter and of the activity source.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".Telephony";

    /// <summary>
    /// The tag that records the provider command, such as <c>Dial</c> or <c>Hold</c>.
    /// </summary>
    public const string CommandTag = "command";

    /// <summary>
    /// The tag that records the stage of dial screening that decided: <see cref="ScreeningStageDestinationPolicy"/>
    /// or <see cref="ScreeningStageCompliance"/>.
    /// </summary>
    public const string ScreeningStageTag = "stage";

    /// <summary>
    /// The tag that records what a reconciler corrected: <see cref="ReconcileOrphanEnded"/>.
    /// </summary>
    public const string ReconcileActionTag = "action";

    /// <summary>
    /// The command was refused before it reached the provider, because the host is shutting down.
    /// </summary>
    public const string ResultNotAdmitted = "not_admitted";

    /// <summary>
    /// The destination safety policy (emergency and premium numbers).
    /// </summary>
    public const string ScreeningStageDestinationPolicy = "destination_policy";

    /// <summary>
    /// The outbound compliance screeners (do-not-call lists, calling windows).
    /// </summary>
    public const string ScreeningStageCompliance = "compliance";

    /// <summary>
    /// A call the provider no longer has was ended on the platform.
    /// </summary>
    public const string ReconcileOrphanEnded = "orphan_ended";

    /// <summary>
    /// The browser credential operation that issues a credential.
    /// </summary>
    public const string CredentialIssue = "issue";

    /// <summary>
    /// The browser credential operation that revokes credentials.
    /// </summary>
    public const string CredentialRevoke = "revoke";

    /// <summary>
    /// The recording was stored in the media store.
    /// </summary>
    public const string IngestStored = "stored";

    /// <summary>
    /// The recording was not ready, or a step failed, and the job is tried again later.
    /// </summary>
    public const string IngestRetry = "retry";

    /// <summary>
    /// The recording's interaction was erased, so the job was cancelled.
    /// </summary>
    public const string IngestCancelled = "cancelled";

    /// <summary>
    /// The job failed too many times and was dead-lettered.
    /// </summary>
    public const string IngestDeadLettered = "dead_lettered";

    /// <summary>
    /// Gets the meter shared by the telephony providers.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("Telephony");

    /// <summary>
    /// Gets the activity source shared by the telephony providers.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource("Telephony");

    /// <summary>
    /// The duration of a provider command run under the server-owned deadline, tagged with the <c>provider</c>,
    /// <see cref="CommandTag"/> and the <c>result</c>: <c>ok</c>, <c>error</c>, <c>timeout</c> (the outcome is
    /// unknown), <c>cancelled</c> or <see cref="ResultNotAdmitted"/>.
    /// </summary>
    public static Histogram<double> CommandDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.telephony.command.duration",
        "The duration of telephony provider commands run under the server-owned deadline.");

    /// <summary>
    /// The number of dial requests screened, tagged with <see cref="ScreeningStageTag"/>, the <c>result</c>
    /// (<c>allowed</c> or <c>denied</c>) and, for a denial by the compliance screeners, the bounded <c>reason</c>.
    /// </summary>
    public static Counter<long> DialScreenings { get; } = Meter.CreateCounter<long>(
        "crestapps.telephony.dial.screenings",
        "{dial}",
        "The number of dial requests screened before they reached a provider, by result.");

    /// <summary>
    /// The number of browser credential operations, tagged with the <c>provider</c>, the <c>operation</c>
    /// (<see cref="CredentialIssue"/> or <see cref="CredentialRevoke"/>) and the <c>outcome</c>.
    /// </summary>
    public static Counter<long> SoftPhoneCredentials { get; } = Meter.CreateCounter<long>(
        "crestapps.telephony.softphone.credentials",
        "{credential}",
        "The number of browser (soft phone) credential operations, by provider, operation and outcome.");

    /// <summary>
    /// The number of corrections a provider reconciler made, tagged with the <c>provider</c> and
    /// <see cref="ReconcileActionTag"/>. Any steady rate means events are being lost between the provider and the platform.
    /// </summary>
    public static Counter<long> ReconcileCorrections { get; } = Meter.CreateCounter<long>(
        "crestapps.telephony.reconcile.corrections",
        "{call}",
        "The number of calls a provider reconciler ended or corrected because their state had drifted from the provider's.");

    /// <summary>
    /// The duration of one recording ingest job attempt, tagged with the <c>provider</c>, the <c>result</c>
    /// (<see cref="IngestStored"/>, <see cref="IngestRetry"/>, <see cref="IngestCancelled"/>,
    /// <see cref="IngestDeadLettered"/> or <c>error</c>) and the <c>outcome</c>.
    /// </summary>
    public static Histogram<double> RecordingIngestDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.telephony.recording.ingest.duration",
        "The duration of one recording ingest job attempt.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// The time from a recording ingest job being queued to the recording being stored, tagged with the <c>provider</c>.
    /// </summary>
    public static Histogram<double> RecordingIngestLag { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.telephony.recording.ingest.lag",
        "The time from a recording ingest job being queued to the recording being stored.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    // What each tenant's last ingest run found, keyed by provider and tenant so a healthy tenant never hides a stuck
    // one: the gauge reports the oldest per provider. The tenant is a key here, never a tag.
    private static readonly ConcurrentDictionary<(string Provider, string Tenant), IngestBacklog> _ingestBacklogs = new();

    // A tenant whose ingest has not run for this long has stopped running it (disabled, or the tenant was released).
    private static readonly TimeSpan _ingestBacklogStaleAfter = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The age, in seconds, of the oldest recording ingest job a provider's runs found due, the oldest across tenants,
    /// or zero when none was due, tagged with the <c>provider</c>. A value that keeps growing means recordings are not
    /// reaching the media store. Read from what the runs recorded: never a query in the callback.
    /// </summary>
    public static ObservableGauge<double> RecordingIngestBacklogAge { get; } = Meter.CreateObservableGauge(
        "crestapps.telephony.recording.ingest.backlog_age",
        ObserveIngestBacklog,
        "s",
        "The age of the oldest recording ingest job the runs found due.");

    /// <summary>
    /// The number of provider voice events discarded before they were applied, tagged with the <c>provider</c> and the
    /// bounded <c>reason</c>.
    /// </summary>
    public static Counter<long> VoiceEventDiscards { get; } = Meter.CreateCounter<long>(
        "crestapps.telephony.voice_event.discards",
        "{event}",
        "The number of provider voice events discarded before they were applied, by reason.");

    /// <summary>
    /// Records one dial screening decision.
    /// </summary>
    /// <param name="stage"><see cref="ScreeningStageDestinationPolicy"/> or <see cref="ScreeningStageCompliance"/>.</param>
    /// <param name="allowed">Whether the dial may go on.</param>
    /// <param name="reason">The bounded reason of a denial, or <see langword="null"/>.</param>
    public static void RecordDialScreening(string stage, bool allowed, string reason = null)
    {
        if (!DialScreenings.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { ScreeningStageTag, stage },
            { CrestAppsOrchardCoreTelemetry.ResultTag, allowed ? "allowed" : "denied" },
        };

        if (!allowed)
        {
            tags.Add(CrestAppsOrchardCoreTelemetry.ReasonTag, ToBoundedName(reason));
        }

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        DialScreenings.Add(1, tags);
    }

    /// <summary>
    /// Records one browser credential operation.
    /// </summary>
    /// <param name="provider">The provider's technical name.</param>
    /// <param name="operation"><see cref="CredentialIssue"/> or <see cref="CredentialRevoke"/>.</param>
    /// <param name="outcome">The outcome, such as <see cref="CrestAppsOrchardCoreTelemetry.OutcomeOk"/>.</param>
    public static void RecordSoftPhoneCredential(string provider, string operation, string outcome)
    {
        if (!SoftPhoneCredentials.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ProviderTag, provider ?? CrestAppsOrchardCoreTelemetry.Unknown },
            { CrestAppsOrchardCoreTelemetry.OperationTag, operation },
            { CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        SoftPhoneCredentials.Add(1, tags);
    }

    /// <summary>
    /// Runs a browser credential operation and records how it ended.
    /// </summary>
    /// <typeparam name="TResult">The result of the operation.</typeparam>
    /// <param name="provider">The provider's technical name.</param>
    /// <param name="operation"><see cref="CredentialIssue"/> or <see cref="CredentialRevoke"/>.</param>
    /// <param name="work">The operation.</param>
    /// <returns>The result of the operation. Exceptions are recorded and rethrown.</returns>
    public static async Task<TResult> MeasureSoftPhoneCredentialAsync<TResult>(string provider, string operation, Func<Task<TResult>> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        try
        {
            var result = await work();

            RecordSoftPhoneCredential(provider, operation, CrestAppsOrchardCoreTelemetry.OutcomeOk);

            return result;
        }
        catch (Exception ex)
        {
            RecordSoftPhoneCredential(provider, operation, CrestAppsOrchardCoreTelemetry.GetOutcome(ex));

            throw;
        }
    }

    /// <summary>
    /// Records calls a reconciler corrected.
    /// </summary>
    /// <param name="provider">The provider's technical name.</param>
    /// <param name="action"><see cref="ReconcileOrphanEnded"/>.</param>
    /// <param name="count">How many calls. Zero and negative counts are ignored.</param>
    public static void RecordReconcileCorrections(string provider, string action, long count = 1)
    {
        if (count <= 0 || !ReconcileCorrections.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ProviderTag, provider ?? CrestAppsOrchardCoreTelemetry.Unknown },
            { ReconcileActionTag, action },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        ReconcileCorrections.Add(count, tags);
    }

    /// <summary>
    /// Starts measuring one recording ingest job attempt. Set the <c>result</c> with
    /// <see cref="TelemetryScope.AddMetricTag(string, object)"/> before the scope is disposed.
    /// </summary>
    /// <param name="provider">The provider's technical name.</param>
    /// <returns>The scope of the attempt.</returns>
    public static TelemetryScope StartRecordingIngest(string provider)
    {
        return TelemetryScope.Start(
            ActivitySource,
            "crestapps.telephony.recording.ingest",
            RecordingIngestDuration,
            new TagList
            {
                { CrestAppsOrchardCoreTelemetry.ProviderTag, provider ?? CrestAppsOrchardCoreTelemetry.Unknown },
            });
    }

    /// <summary>
    /// Records how long after it was queued a recording reached the media store.
    /// </summary>
    /// <param name="provider">The provider's technical name.</param>
    /// <param name="queuedUtc">When the job was queued.</param>
    /// <param name="storedUtc">When the recording was stored.</param>
    public static void RecordRecordingIngestLag(string provider, DateTime queuedUtc, DateTime storedUtc)
    {
        if (!RecordingIngestLag.Enabled || queuedUtc == default)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ProviderTag, provider ?? CrestAppsOrchardCoreTelemetry.Unknown },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        RecordingIngestLag.Record(Math.Max(0, (storedUtc - queuedUtc).TotalSeconds), tags);
    }

    /// <summary>
    /// Records the age of the oldest recording ingest job a run found due.
    /// </summary>
    /// <param name="provider">The provider's technical name.</param>
    /// <param name="oldestQueuedUtc">When the oldest due job was queued, or <see langword="null"/> when none was due.</param>
    /// <param name="nowUtc">The current time.</param>
    public static void RecordRecordingIngestBacklog(string provider, DateTime? oldestQueuedUtc, DateTime nowUtc)
    {
        var key = (provider ?? CrestAppsOrchardCoreTelemetry.Unknown, ShellScope.Context?.Settings?.Name ?? string.Empty);
        var ageSeconds = oldestQueuedUtc is { } queued ? Math.Max(0, (nowUtc - queued).TotalSeconds) : 0;

        _ingestBacklogs[key] = new IngestBacklog(ageSeconds, Stopwatch.GetTimestamp());
    }

    private static IEnumerable<Measurement<double>> ObserveIngestBacklog()
    {
        foreach (var (key, backlog) in _ingestBacklogs)
        {
            if (backlog.Since >= _ingestBacklogStaleAfter)
            {
                _ingestBacklogs.TryRemove(key, out _);
            }
        }

        return _ingestBacklogs
            .GroupBy(entry => entry.Key.Provider, StringComparer.Ordinal)
            .Select(group => new Measurement<double>(
                group.Max(entry => entry.Value.CurrentAgeSeconds),
                new KeyValuePair<string, object>(CrestAppsOrchardCoreTelemetry.ProviderTag, group.Key)));
    }

    /// <summary>
    /// Records a provider voice event discarded before it was applied.
    /// </summary>
    /// <param name="provider">The provider's technical name.</param>
    /// <param name="reason">Why it was discarded, from a bounded set such as <c>stale</c> or <c>duplicate</c>.</param>
    public static void RecordVoiceEventDiscard(string provider, string reason)
    {
        if (!VoiceEventDiscards.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ProviderTag, provider ?? CrestAppsOrchardCoreTelemetry.Unknown },
            { CrestAppsOrchardCoreTelemetry.ReasonTag, reason },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        VoiceEventDiscards.Add(1, tags);
    }

    // What a tenant's last ingest run found: the age of its oldest due job then, and when that was.
    private readonly record struct IngestBacklog(double AgeSeconds, long RecordedTimestamp)
    {
        public TimeSpan Since => Stopwatch.GetElapsedTime(RecordedTimestamp);

        // A job still due has kept ageing since the run saw it; a run that found none stays at zero.
        public double CurrentAgeSeconds => AgeSeconds > 0 ? AgeSeconds + Since.TotalSeconds : 0;
    }

    /// <summary>
    /// Maps a code a screener chose, such as <c>DoNotCall</c>, onto a bounded tag value: lower-case letters, digits and
    /// underscores, at most 40 characters, otherwise <c>other</c>.
    /// </summary>
    /// <param name="value">The code.</param>
    /// <returns>The bounded value.</returns>
    public static string ToBoundedName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return CrestAppsOrchardCoreTelemetry.Unknown;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > 40)
        {
            return CrestAppsOrchardCoreTelemetry.Other;
        }

        var builder = new System.Text.StringBuilder(trimmed.Length + 4);

        for (var index = 0; index < trimmed.Length; index++)
        {
            var character = trimmed[index];

            if (char.IsAsciiLetterUpper(character))
            {
                if (index > 0 && builder.Length > 0 && builder[^1] != '_')
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(character));
            }
            else if (char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character))
            {
                builder.Append(character);
            }
            else if (character is '_' or '-' or ' ' or '.')
            {
                if (builder.Length > 0 && builder[^1] != '_')
                {
                    builder.Append('_');
                }
            }
            else
            {
                return CrestAppsOrchardCoreTelemetry.Other;
            }
        }

        return builder.Length == 0 ? CrestAppsOrchardCoreTelemetry.Other : builder.ToString();
    }
}
