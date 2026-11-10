using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.DataConnectors.Connectors.Http;
using CrestApps.OrchardCore.DataConnectors.Http;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.DataConnectors.Telemetry;

/// <summary>
/// The OpenTelemetry spans and metrics of data connectors: refreshes of imported data sets, live reads, the HTTP client
/// HTTP-based connectors share, and the row store. Nothing is recorded, and next to nothing is spent, while no listener
/// is attached to <see cref="SourceName"/>.
/// </summary>
/// <remarks>
/// A metric never carries a data set, connection or refresh identifier, a URL, a host or a data set name; those go on
/// spans and in logs only. The <see cref="ConnectorTag"/> holds the name of a registered connector, such as
/// <c>HttpApi</c> or <c>SqlServer</c>, which the code bounds.
/// </remarks>
public static class DataConnectorsTelemetry
{
    /// <summary>
    /// The area of the meter and the activity source.
    /// </summary>
    public const string Area = "DataConnectors";

    /// <summary>
    /// The name of the meter and of the activity source: <c>CrestApps.OrchardCore.DataConnectors</c>.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + "." + Area;

    /// <summary>
    /// The area the retries of the HTTP client are counted under on <see cref="RetryTelemetry.Attempts"/>.
    /// </summary>
    public const string RetryArea = "dataconnectors";

    /// <summary>
    /// The tag that holds the name of the registered connector, such as <c>HttpApi</c>, or <c>unknown</c> when the
    /// connection couldn't be resolved.
    /// </summary>
    public const string ConnectorTag = "connector";

    /// <summary>
    /// The tag that holds how a refresh read its source: <c>full</c> or <c>incremental</c>.
    /// </summary>
    public const string ModeTag = "mode";

    /// <summary>
    /// The tag that holds why rows are read from a source: <c>live</c>, <c>import</c> or <c>preview</c>.
    /// </summary>
    public const string PurposeTag = "purpose";

    /// <summary>
    /// The tag that holds where a database error happened: <c>connect</c>, <c>query</c> or <c>read</c>.
    /// </summary>
    public const string StageTag = "stage";

    /// <summary>
    /// The tag that tells whether a failure may succeed when tried again: <c>true</c> or <c>false</c>.
    /// </summary>
    public const string TransientTag = "transient";

    /// <summary>
    /// The tag that holds the kind of authentication that asked for a token: <c>oauth2</c> or <c>openid_connect</c>.
    /// </summary>
    public const string AuthTag = "auth";

    /// <summary>
    /// The tag that holds which limit a read hit, such as <c>max_pages</c>.
    /// </summary>
    public const string LimitTag = "limit";

    /// <summary>
    /// The tag that holds how an HTTP API data set follows its pages, such as <c>page_number</c> or <c>cursor</c>.
    /// </summary>
    public const string PaginationTag = "pagination";

    /// <summary>
    /// The span tag that holds the identifier of the data set. Never a metric tag.
    /// </summary>
    public const string DataSetIdSpanTag = "crestapps.dataconnector.data_set.id";

    /// <summary>
    /// The span tag that holds the identifier of the refresh. Never a metric tag.
    /// </summary>
    public const string RefreshIdSpanTag = "crestapps.dataconnector.refresh.id";

    /// <summary>
    /// The span tag that holds the identifier of the connection. Never a metric tag.
    /// </summary>
    public const string ConnectionIdSpanTag = "crestapps.dataconnector.connection.id";

    /// <summary>
    /// The span tag that holds the number of rows an operation read or wrote.
    /// </summary>
    public const string RowsSpanTag = "crestapps.dataconnector.rows";

    /// <summary>
    /// The <see cref="LimitTag"/> value of an HTTP API read that stopped at its page limit.
    /// </summary>
    public const string LimitMaxPages = "max_pages";

    /// <summary>
    /// The retry reason of a request that couldn't reach the server.
    /// </summary>
    public const string RetryNetwork = "network";

    /// <summary>
    /// The retry reason of a request the server didn't answer in time, or answered <c>408</c>.
    /// </summary>
    public const string RetryTimeout = "timeout";

    /// <summary>
    /// The retry reason of a request the server throttled with <c>429</c>.
    /// </summary>
    public const string RetryStatus429 = "status_429";

    /// <summary>
    /// The retry reason of a request the server answered with a <c>5xx</c> status that may pass.
    /// </summary>
    public const string RetryStatus5xx = "status_5xx";

    /// <summary>
    /// The retry reason of a request redirected to another address on the same server.
    /// </summary>
    public const string RetryRedirect = "redirect";

    /// <summary>
    /// The retry reason of a request answered <c>401</c>, sent again with a new token.
    /// </summary>
    public const string RetryTokenRefresh = "token_refresh";

    private static readonly ConcurrentDictionary<string, LastSuccessSnapshot> _lastSuccess = new(StringComparer.Ordinal);
    private static readonly TimeSpan _lastSuccessSnapshotLifetime = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets the meter of data connectors.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter(Area);

    /// <summary>
    /// Gets the activity source of data connectors.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource(Area);

    /// <summary>
    /// Gets the histogram of the duration of refreshes, tagged with the <see cref="CrestAppsOrchardCoreTelemetry.TriggerTag"/>,
    /// <see cref="ModeTag"/>, <see cref="ConnectorTag"/> and <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/>
    /// (<c>ok</c>, <c>error</c>, <c>cancelled</c>, <c>timeout</c> or <c>skipped</c>).
    /// </summary>
    public static Histogram<double> RefreshDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.dataconnector.refresh.duration",
        "The duration of refreshes of imported data sets.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// Gets the counter of refreshes, tagged as <see cref="RefreshDuration"/> is.
    /// </summary>
    public static Counter<long> Refreshes { get; } = Meter.CreateCounter<long>(
        "crestapps.dataconnector.refreshes",
        "{refresh}",
        "The number of refreshes of imported data sets, by how they ended.");

    /// <summary>
    /// Gets the histogram of the rows a successful refresh read from its source, tagged with the
    /// <see cref="CrestAppsOrchardCoreTelemetry.TriggerTag"/>, <see cref="ModeTag"/> and <see cref="ConnectorTag"/>.
    /// </summary>
    public static Histogram<long> RefreshRows { get; } = CrestAppsOrchardCoreTelemetry.CreateCountHistogram(
        Meter,
        "crestapps.dataconnector.refresh.rows",
        "{row}",
        "The rows a successful refresh of an imported data set read from its source.");

    /// <summary>
    /// Gets the up-down counter of the refreshes running now, tagged with the
    /// <see cref="CrestAppsOrchardCoreTelemetry.TriggerTag"/>.
    /// </summary>
    public static UpDownCounter<long> ActiveRefreshes { get; } = Meter.CreateUpDownCounter<long>(
        "crestapps.dataconnector.refresh.active",
        "{refresh}",
        "The refreshes of imported data sets running now.");

    /// <summary>
    /// Gets the gauge of the seconds since the oldest last successful refresh among the enabled imported data sets that
    /// refresh on a schedule. Each run of the refresh task takes the snapshot it reports, so the value stops being
    /// reported a few minutes after the task stops running; <c>crestapps.background_task.last_success</c> covers that.
    /// </summary>
    public static ObservableGauge<double> LastSuccessAge { get; } = Meter.CreateObservableGauge(
        "crestapps.dataconnector.refresh.last_success.age",
        ObserveLastSuccessAge,
        "s",
        "The seconds since the oldest last successful refresh among the scheduled imported data sets.");

    /// <summary>
    /// Gets the histogram of the duration of reads of a data set from its source, tagged with the
    /// <see cref="ConnectorTag"/>, <see cref="PurposeTag"/> and <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/>.
    /// It runs from the first batch asked for to the last one, so it includes the time the consumer spends between
    /// batches.
    /// </summary>
    public static Histogram<double> ReadDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.dataconnector.read.duration",
        "The duration of reads of connected data sets from their sources.");

    /// <summary>
    /// Gets the histogram of the rows a read of a data set returned, tagged with the <see cref="ConnectorTag"/> and
    /// <see cref="PurposeTag"/>.
    /// </summary>
    public static Histogram<long> ReadRows { get; } = CrestAppsOrchardCoreTelemetry.CreateCountHistogram(
        Meter,
        "crestapps.dataconnector.read.rows",
        "{row}",
        "The rows a read of a connected data set returned.");

    /// <summary>
    /// Gets the counter of reads of a data set that failed, tagged with the <see cref="ConnectorTag"/>,
    /// <see cref="PurposeTag"/> and <see cref="TransientTag"/>.
    /// </summary>
    public static Counter<long> ReadFailures { get; } = Meter.CreateCounter<long>(
        "crestapps.dataconnector.read.failures",
        "{read}",
        "The number of reads of connected data sets that failed.");

    /// <summary>
    /// Gets the counter of reads of a data set stopped by the live read timeout, tagged with the
    /// <see cref="ConnectorTag"/> and <see cref="PurposeTag"/>.
    /// </summary>
    public static Counter<long> ReadTimeouts { get; } = Meter.CreateCounter<long>(
        "crestapps.dataconnector.read.timeouts",
        "{read}",
        "The number of reads of connected data sets stopped because they ran longer than the live read timeout.");

    /// <summary>
    /// Gets the counter of connection checks, tagged with the <see cref="ConnectorTag"/> and
    /// <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/> (<c>ok</c> or <c>error</c>).
    /// </summary>
    public static Counter<long> ConnectionTests { get; } = Meter.CreateCounter<long>(
        "crestapps.dataconnector.connection.tests",
        "{test}",
        "The number of times a data connection was checked, by whether it worked.");

    /// <summary>
    /// Gets the counter of reads of imported data sets served before their first refresh succeeded, which return no
    /// rows.
    /// </summary>
    public static Counter<long> EmptyReads { get; } = Meter.CreateCounter<long>(
        "crestapps.dataconnector.empty_reads",
        "{read}",
        "The number of reads of imported data sets that returned no rows because the data set was never imported.");

    /// <summary>
    /// Gets the histogram of the duration of reads of one page of rows from the row store, tagged with the
    /// <see cref="CrestAppsOrchardCoreTelemetry.OperationTag"/> (<c>scan</c> or <c>keys</c>) and
    /// <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/>.
    /// </summary>
    public static Histogram<double> RowStoreReadDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.dataconnector.rowstore.read.duration",
        "The duration of reads of one page of rows of an imported data set from the row store.");

    /// <summary>
    /// Gets the histogram of the duration of writes of one batch of rows to the row store, tagged with the
    /// <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/>.
    /// </summary>
    public static Histogram<double> RowStoreWriteDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.dataconnector.rowstore.write.duration",
        "The duration of writes of one batch of rows of an imported data set to the row store.");

    /// <summary>
    /// Gets the counter of requests the HTTP client sent again, tagged with the
    /// <see cref="CrestAppsOrchardCoreTelemetry.ReasonTag"/>: <see cref="RetryNetwork"/>, <see cref="RetryTimeout"/>,
    /// <see cref="RetryStatus429"/>, <see cref="RetryStatus5xx"/>, <see cref="RetryRedirect"/> or
    /// <see cref="RetryTokenRefresh"/>.
    /// </summary>
    public static Counter<long> HttpRetries { get; } = Meter.CreateCounter<long>(
        "crestapps.dataconnector.http.retries",
        "{retry}",
        "The number of requests of HTTP-based connectors sent again, by why.");

    /// <summary>
    /// Gets the histogram of the requests one call of the HTTP client sent, retries and redirects included, tagged
    /// with the <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/> and
    /// <see cref="CrestAppsOrchardCoreTelemetry.StatusClassTag"/> of the last response.
    /// </summary>
    public static Histogram<long> HttpAttempts { get; } = CrestAppsOrchardCoreTelemetry.CreateCountHistogram(
        Meter,
        "crestapps.dataconnector.http.attempts",
        "{attempt}",
        "The requests one call of an HTTP-based connector sent, retries and redirects included.",
        [1, 2, 3, 4, 5, 8]);

    /// <summary>
    /// Gets the counter of tokens HTTP-based connectors needed, tagged with the <see cref="AuthTag"/> and the
    /// <see cref="CrestAppsOrchardCoreTelemetry.ResultTag"/>: <c>cache_hit</c>, <c>acquired</c> or <c>failed</c>.
    /// </summary>
    public static Counter<long> TokenRequests { get; } = Meter.CreateCounter<long>(
        "crestapps.dataconnector.token.requests",
        "{request}",
        "The number of tokens HTTP-based connectors needed, by whether the cache served them.");

    /// <summary>
    /// Gets the histogram of the duration of token requests to a token endpoint, discovery included, tagged with the
    /// <see cref="AuthTag"/> and <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/>.
    /// </summary>
    public static Histogram<double> TokenDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.dataconnector.token.duration",
        "The duration of requests HTTP-based connectors sent to a token endpoint.");

    /// <summary>
    /// Gets the counter of connections refused because every address of the host is on the site's own network.
    /// </summary>
    public static Counter<long> NetworkBlocked { get; } = Meter.CreateCounter<long>(
        "crestapps.dataconnector.network.blocked",
        "{connection}",
        "The number of connections of data connectors refused because the host is on the site's own network.");

    /// <summary>
    /// Gets the counter of pages HTTP API data sets read, tagged with the <see cref="PaginationTag"/>.
    /// </summary>
    public static Counter<long> HttpPages { get; } = Meter.CreateCounter<long>(
        "crestapps.dataconnector.http.pages",
        "{page}",
        "The number of pages HTTP API data sets read.");

    /// <summary>
    /// Gets the counter of reads that stopped at a limit before the source had no more rows, tagged with the
    /// <see cref="LimitTag"/>.
    /// </summary>
    public static Counter<long> LimitHits { get; } = Meter.CreateCounter<long>(
        "crestapps.dataconnector.limit.hits",
        "{hit}",
        "The number of reads of data sets cut short by a limit.");

    /// <summary>
    /// Gets the counter of pages of documents Elasticsearch data sets scrolled through.
    /// </summary>
    public static Counter<long> ElasticsearchScrollPages { get; } = Meter.CreateCounter<long>(
        "crestapps.dataconnector.elasticsearch.scroll.pages",
        "{page}",
        "The number of pages of documents Elasticsearch data sets scrolled through.");

    /// <summary>
    /// Gets the counter of Elasticsearch scrolls that couldn't be cleared after a read, and stay open until their
    /// keep-alive passes.
    /// </summary>
    public static Counter<long> ElasticsearchScrollClearFailures { get; } = Meter.CreateCounter<long>(
        "crestapps.dataconnector.elasticsearch.scroll.clear_failures",
        "{scroll}",
        "The number of Elasticsearch scrolls that couldn't be cleared after a read.");

    /// <summary>
    /// Gets the counter of errors relational databases answered, tagged with the <see cref="ConnectorTag"/>,
    /// <see cref="StageTag"/> and <see cref="TransientTag"/>.
    /// </summary>
    public static Counter<long> DbErrors { get; } = Meter.CreateCounter<long>(
        "crestapps.dataconnector.db.errors",
        "{error}",
        "The number of errors relational data connectors got from their databases.");

    /// <summary>
    /// Gets the histogram of the time spent parsing a media library file, tagged with the
    /// <see cref="CrestAppsOrchardCoreTelemetry.FormatTag"/> and <see cref="CrestAppsOrchardCoreTelemetry.OutcomeTag"/>.
    /// It counts the time the file format spends producing rows, not the time the consumer spends between batches.
    /// </summary>
    public static Histogram<double> FileParseDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.dataconnector.file.parse.duration",
        "The time spent parsing media library files read by data sets.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// Returns the tag value of what started a refresh.
    /// </summary>
    /// <param name="trigger">What started the refresh.</param>
    /// <returns><c>manual</c> or <c>scheduled</c>.</returns>
    public static string ToTag(DataSetRefreshTrigger trigger)
    {
        return trigger switch
        {
            DataSetRefreshTrigger.Manual => "manual",
            DataSetRefreshTrigger.Scheduled => "scheduled",
            _ => CrestAppsOrchardCoreTelemetry.Other,
        };
    }

    /// <summary>
    /// Returns the tag value of how a refresh read its source.
    /// </summary>
    /// <param name="mode">How the refresh read its source.</param>
    /// <returns><c>full</c> or <c>incremental</c>.</returns>
    public static string ToTag(DataSetRefreshMode mode)
    {
        return mode switch
        {
            DataSetRefreshMode.Full => "full",
            DataSetRefreshMode.Incremental => "incremental",
            _ => CrestAppsOrchardCoreTelemetry.Other,
        };
    }

    /// <summary>
    /// Returns the tag value of why rows are read from a source.
    /// </summary>
    /// <param name="purpose">Why the rows are read.</param>
    /// <returns><c>live</c>, <c>import</c> or <c>preview</c>.</returns>
    public static string ToTag(DataConnectorReadPurpose purpose)
    {
        return purpose switch
        {
            DataConnectorReadPurpose.Live => "live",
            DataConnectorReadPurpose.Import => "import",
            DataConnectorReadPurpose.Preview => "preview",
            _ => CrestAppsOrchardCoreTelemetry.Other,
        };
    }

    /// <summary>
    /// Returns the tag value of how an HTTP API data set follows its pages.
    /// </summary>
    /// <param name="pagination">The paging strategy.</param>
    /// <returns>The strategy in snake case, such as <c>page_number</c>.</returns>
    public static string ToTag(HttpPaginationType pagination)
    {
        return pagination switch
        {
            HttpPaginationType.None => "none",
            HttpPaginationType.PageNumber => "page_number",
            HttpPaginationType.Offset => "offset",
            HttpPaginationType.Cursor => "cursor",
            HttpPaginationType.NextLink => "next_link",
            HttpPaginationType.LinkHeader => "link_header",
            _ => CrestAppsOrchardCoreTelemetry.Other,
        };
    }

    /// <summary>
    /// Returns the tag value of the kind of authentication that asks for tokens.
    /// </summary>
    /// <param name="authentication">The kind of authentication.</param>
    /// <returns><c>oauth2</c> or <c>openid_connect</c>.</returns>
    public static string ToTag(HttpAuthenticationType authentication)
    {
        return authentication switch
        {
            HttpAuthenticationType.OAuth2 => "oauth2",
            HttpAuthenticationType.OpenIdConnect => "openid_connect",
            _ => CrestAppsOrchardCoreTelemetry.Other,
        };
    }

    /// <summary>
    /// Marks an operation failed by an exception, with the exception type but not its message: a data connector's
    /// messages can name the server and the path it called, which never go on a span or a metric.
    /// </summary>
    /// <param name="scope">The scope of the operation.</param>
    /// <param name="exception">The exception the operation failed with.</param>
    internal static void Fail(TelemetryScope scope, Exception exception)
    {
        var outcome = CrestAppsOrchardCoreTelemetry.GetOutcome(exception);

        scope.SetOutcome(outcome);

        if (outcome == CrestAppsOrchardCoreTelemetry.OutcomeCancelled)
        {
            return;
        }

        scope.AddMetricTag(CrestAppsOrchardCoreTelemetry.ErrorTypeTag, exception.GetType().FullName);
        scope.Activity?.SetStatus(ActivityStatusCode.Error);
    }

    /// <summary>
    /// Adds to a counter, with the tenant tag when it is enabled, unless nothing listens to it.
    /// </summary>
    /// <param name="counter">The counter.</param>
    /// <param name="tags">The bounded tags.</param>
    /// <param name="value">The value to add.</param>
    internal static void Add(Counter<long> counter, TagList tags = default, long value = 1)
    {
        if (!counter.Enabled)
        {
            return;
        }

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        counter.Add(value, tags);
    }

    /// <summary>
    /// Records a value on a histogram, with the tenant tag when it is enabled, unless nothing listens to it.
    /// </summary>
    /// <param name="histogram">The histogram.</param>
    /// <param name="value">The value.</param>
    /// <param name="tags">The bounded tags.</param>
    internal static void Record(Histogram<long> histogram, long value, TagList tags = default)
    {
        if (!histogram.Enabled)
        {
            return;
        }

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        histogram.Record(value, tags);
    }

    /// <summary>
    /// Records a duration on a duration histogram, with the outcome and the tenant tag when it is enabled, unless
    /// nothing listens to it.
    /// </summary>
    /// <param name="histogram">The duration histogram.</param>
    /// <param name="elapsed">The duration.</param>
    /// <param name="outcome">How the operation ended.</param>
    /// <param name="tags">The other bounded tags.</param>
    internal static void RecordDuration(Histogram<double> histogram, TimeSpan elapsed, string outcome, TagList tags = default)
    {
        if (!histogram.Enabled)
        {
            return;
        }

        tags.Add(CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome);
        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        histogram.Record(elapsed.TotalSeconds, tags);
    }

    /// <summary>
    /// Counts a request the HTTP client sends again.
    /// </summary>
    /// <param name="reason">Why it is sent again.</param>
    internal static void RecordHttpRetry(string reason)
    {
        Add(HttpRetries, new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ReasonTag, reason },
        });
    }

    /// <summary>
    /// Records the requests one call of the HTTP client sent.
    /// </summary>
    /// <param name="attempts">The number of requests sent.</param>
    /// <param name="outcome">How the call ended.</param>
    /// <param name="statusCode">The status code of the last response, or <see langword="null"/> when none was received.</param>
    internal static void RecordHttpAttempts(int attempts, string outcome, int? statusCode)
    {
        Record(HttpAttempts, attempts, new TagList
        {
            { CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome },
            { CrestAppsOrchardCoreTelemetry.StatusClassTag, ExternalCallTelemetry.GetStatusClass(statusCode) },
        });
    }

    /// <summary>
    /// Counts a token an HTTP-based connector needed.
    /// </summary>
    /// <param name="auth">The <see cref="AuthTag"/> value.</param>
    /// <param name="result"><c>cache_hit</c>, <c>acquired</c> or <c>failed</c>.</param>
    internal static void RecordTokenRequest(string auth, string result)
    {
        Add(TokenRequests, new TagList
        {
            { AuthTag, auth },
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
        });
    }

    /// <summary>
    /// Counts an error a relational database answered.
    /// </summary>
    /// <param name="connector">The name of the connector.</param>
    /// <param name="stage"><c>connect</c>, <c>query</c> or <c>read</c>.</param>
    /// <param name="transient">Whether the error may pass when tried again.</param>
    internal static void RecordDbError(string connector, string stage, bool transient)
    {
        Add(DbErrors, new TagList
        {
            { ConnectorTag, connector ?? CrestAppsOrchardCoreTelemetry.Unknown },
            { StageTag, stage },
            { TransientTag, transient },
        });
    }

    /// <summary>
    /// Keeps the oldest last successful refresh among a tenant's scheduled imported data sets, for
    /// <see cref="LastSuccessAge"/> to report.
    /// </summary>
    /// <param name="tenant">The name of the tenant.</param>
    /// <param name="oldestSuccessUtc">The oldest last successful refresh, or <see langword="null"/> when the tenant
    /// has no scheduled imported data set.</param>
    /// <param name="utcNow">The current time, from the site clock.</param>
    internal static void SetOldestLastSuccess(string tenant, DateTime? oldestSuccessUtc, DateTime utcNow)
    {
        tenant ??= string.Empty;

        if (oldestSuccessUtc is not { } oldest)
        {
            _lastSuccess.TryRemove(tenant, out _);

            return;
        }

        var age = Math.Max(0, (utcNow - oldest).TotalSeconds);

        _lastSuccess[tenant] = new LastSuccessSnapshot(age, Stopwatch.GetTimestamp());
    }

    private static IEnumerable<Measurement<double>> ObserveLastSuccessAge()
    {
        var oldest = double.NaN;

        foreach (var (tenant, snapshot) in _lastSuccess)
        {
            var sinceSnapshot = Stopwatch.GetElapsedTime(snapshot.Timestamp);

            // A snapshot no run of the refresh task renewed belongs to a tenant that stopped or released its shell.
            if (sinceSnapshot > _lastSuccessSnapshotLifetime)
            {
                continue;
            }

            var age = snapshot.AgeSeconds + sinceSnapshot.TotalSeconds;

            if (CrestAppsOrchardCoreTelemetry.IncludeTenantName)
            {
                yield return new Measurement<double>(age, new KeyValuePair<string, object>(CrestAppsOrchardCoreTelemetry.TenantTag, tenant));

                continue;
            }

            if (double.IsNaN(oldest) || age > oldest)
            {
                oldest = age;
            }
        }

        if (!double.IsNaN(oldest))
        {
            yield return new Measurement<double>(oldest);
        }
    }

    private readonly record struct LastSuccessSnapshot(double AgeSeconds, long Timestamp);
}
