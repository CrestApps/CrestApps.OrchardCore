using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.DataConnectors.Telemetry;

/// <summary>
/// The log messages of data connectors, with event ids from <see cref="CrestAppsEventIds.DataConnectors"/>. No message
/// holds a secret, a token, a query string or a full URL; a URL without its query string is logged at Debug only.
/// </summary>
internal static partial class DataConnectorsLog
{
    private static readonly Func<ILogger, string, string, IDisposable> _refreshScope =
        LoggerMessage.DefineScope<string, string>("DataSetId:{DataSetId} RefreshId:{RefreshId}");

    /// <summary>
    /// Starts the log scope of one refresh, so every line it writes carries the data set and refresh ids.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="dataSetId">The identifier of the data set.</param>
    /// <param name="refreshId">The identifier of the refresh.</param>
    /// <returns>The scope, or <see langword="null"/>.</returns>
    public static IDisposable BeginRefreshScope(this ILogger logger, string dataSetId, string refreshId)
    {
        return _refreshScope(logger, dataSetId, refreshId);
    }

    [LoggerMessage(
        EventId = CrestAppsEventIds.DataConnectors,
        Level = LogLevel.Information,
        Message = "Refreshing the data set '{DataSet}' ({Trigger}, {Mode}).")]
    public static partial void RefreshStarted(this ILogger logger, string dataSet, string trigger, string mode);

    [LoggerMessage(
        EventId = CrestAppsEventIds.DataConnectors + 1,
        Level = LogLevel.Information,
        Message = "Refreshed the data set '{DataSet}' in {ElapsedMilliseconds} ms ({Mode}): {RowsRead} rows read, {RowCount} rows published.")]
    public static partial void RefreshSucceeded(this ILogger logger, string dataSet, long elapsedMilliseconds, string mode, long rowsRead, long rowCount);

    [LoggerMessage(
        EventId = CrestAppsEventIds.DataConnectors + 2,
        Level = LogLevel.Information,
        Message = "The fields of the data set '{DataSet}' changed at the source: {Changes}.")]
    public static partial void RefreshSchemaChanged(this ILogger logger, string dataSet, string changes);

    [LoggerMessage(
        EventId = CrestAppsEventIds.DataConnectors + 3,
        Level = LogLevel.Information,
        Message = "The incremental refresh of the data set '{DataSet}' reads every row instead ({Reason}).")]
    public static partial void RefreshDowngradedToFull(this ILogger logger, string dataSet, string reason);

    [LoggerMessage(
        EventId = CrestAppsEventIds.DataConnectors + 4,
        Level = LogLevel.Error,
        Message = "The refresh of the data set '{DataSetId}' failed before it could record how it ended. The data sets still due are refreshed on the next run.")]
    public static partial void RefreshAborted(this ILogger logger, Exception exception, string dataSetId);

    [LoggerMessage(
        EventId = CrestAppsEventIds.DataConnectors + 5,
        Level = LogLevel.Information,
        Message = "Imported data sets due: {Due}. Refreshed: {Succeeded}. Failed: {Failed}. Skipped because another refresh of the data set was running: {Skipped}. Left for the next run: {Deferred}.")]
    public static partial void RefreshSweepCompleted(this ILogger logger, int due, int succeeded, int failed, int skipped, int deferred);

    [LoggerMessage(
        EventId = CrestAppsEventIds.DataConnectors + 6,
        Level = LogLevel.Warning,
        Message = "The last successful refresh of the scheduled imported data sets couldn't be read for the refresh age metric.")]
    public static partial void LastSuccessSnapshotFailed(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = CrestAppsEventIds.DataConnectors + 10,
        Level = LogLevel.Warning,
        Message = "A data connector request is sent again after {Reason} (status {StatusCode}): attempt {Attempt} of {MaxAttempts}, in {DelayMilliseconds} ms.")]
    public static partial void HttpRetrying(this ILogger logger, string reason, int? statusCode, int attempt, int maxAttempts, long delayMilliseconds);

    [LoggerMessage(
        EventId = CrestAppsEventIds.DataConnectors + 11,
        Level = LogLevel.Debug,
        Message = "{Method} {Url} answered {StatusCode} in {ElapsedMilliseconds} ms (request {Attempt}).")]
    public static partial void HttpResponded(this ILogger logger, string method, string url, int statusCode, long elapsedMilliseconds, int attempt);

    [LoggerMessage(
        EventId = CrestAppsEventIds.DataConnectors + 12,
        Level = LogLevel.Debug,
        Message = "{Method} {Url} got no answer (request {Attempt}): {Reason}.")]
    public static partial void HttpNoAnswer(this ILogger logger, string method, string url, int attempt, string reason);

    [LoggerMessage(
        EventId = CrestAppsEventIds.DataConnectors + 13,
        Level = LogLevel.Warning,
        Message = "A data connector was refused a connection, because every address of the host is on the site's own network. Add the host to CrestApps:DataConnectors:Network:AllowedHosts if the connection is meant to reach it.")]
    public static partial void NetworkBlocked(this ILogger logger);

    [LoggerMessage(
        EventId = CrestAppsEventIds.DataConnectors + 14,
        Level = LogLevel.Warning,
        Message = "The read of the HTTP API data set '{DataSetId}' stopped at its limit of {PageLimit} pages, and the pages after it weren't read.")]
    public static partial void HttpPageLimitReached(this ILogger logger, string dataSetId, int pageLimit);

    [LoggerMessage(
        EventId = CrestAppsEventIds.DataConnectors + 15,
        Level = LogLevel.Debug,
        Message = "An Elasticsearch scroll couldn't be cleared after a read; it expires after its keep-alive.")]
    public static partial void ElasticsearchScrollNotCleared(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = CrestAppsEventIds.DataConnectors + 20,
        Level = LogLevel.Warning,
        Message = "The read of the data set '{DataSetId}' was stopped after {Timeout}, the longest a live read may run.")]
    public static partial void ReadTimedOut(this ILogger logger, string dataSetId, TimeSpan timeout);

    [LoggerMessage(
        EventId = CrestAppsEventIds.DataConnectors + 21,
        Level = LogLevel.Warning,
        Message = "The imported data set '{DataSet}' was read before its first refresh succeeded, so the read returned no rows. This is logged once per data set until the site restarts.")]
    public static partial void ReadBeforeFirstImport(this ILogger logger, string dataSet);
}
