using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContentTransfer.Telemetry;

/// <summary>
/// The source-generated log messages of the Content Transfer module. The event ids start at
/// <see cref="CrestAppsEventIds.ContentTransfer"/>: 16000-16099 imports, 16100-16199 exports, 16200-16299 the admin
/// pages, the jobs they start and the upload cleanup. No message carries a cell value, a file name or a phone number.
/// </summary>
internal static partial class ContentTransferLog
{
    [LoggerMessage(
        EventId = CrestAppsEventIds.ContentTransfer,
        Level = LogLevel.Information,
        Message = "The import of entry '{EntryId}' is at row {CurrentRow} of {TotalRecords}, reading {RowsPerSecond} rows per second in this run.")]
    public static partial void ImportProgress(ILogger logger, string entryId, int currentRow, int totalRecords, double rowsPerSecond);

    [LoggerMessage(
        EventId = CrestAppsEventIds.ContentTransfer + 1,
        Level = LogLevel.Warning,
        Message = "Row {RowIndex} of the import of entry '{EntryId}' could not be imported because of a {ExceptionType}. {FailedRows} rows of this run have failed with an exception so far; only some are logged.")]
    public static partial void ImportRowFailed(ILogger logger, int rowIndex, string entryId, string exceptionType, int failedRows);

    [LoggerMessage(
        EventId = CrestAppsEventIds.ContentTransfer + 2,
        Level = LogLevel.Warning,
        Message = "The import of entry '{EntryId}' is tried again after a failed run, attempt {Attempt} of {MaxAttempts} in this run, after waiting {Delay}.")]
    public static partial void ImportRetrying(ILogger logger, string entryId, int attempt, int maxAttempts, TimeSpan delay);

    [LoggerMessage(
        EventId = CrestAppsEventIds.ContentTransfer + 3,
        Level = LogLevel.Warning,
        Message = "The run of the import of entry '{EntryId}' was abandoned ({Reason}) after {RunElapsed}; the row in flight had been running for {RowElapsed}.")]
    public static partial void ImportRunAbandoned(ILogger logger, string entryId, string reason, TimeSpan runElapsed, TimeSpan rowElapsed);

    [LoggerMessage(
        EventId = CrestAppsEventIds.ContentTransfer + 100,
        Level = LogLevel.Information,
        Message = "The export of entry '{EntryId}' of content type '{ContentType}' started with {TotalRecords} content items to read.")]
    public static partial void ExportStarted(ILogger logger, string entryId, string contentType, int totalRecords);

    [LoggerMessage(
        EventId = CrestAppsEventIds.ContentTransfer + 101,
        Level = LogLevel.Information,
        Message = "The export of entry '{EntryId}' is already being processed by another run.")]
    public static partial void ExportLockTaken(ILogger logger, string entryId);

    [LoggerMessage(
        EventId = CrestAppsEventIds.ContentTransfer + 102,
        Level = LogLevel.Information,
        Message = "The export of entry '{EntryId}' has read {Processed} of {TotalRecords} content items.")]
    public static partial void ExportProgress(ILogger logger, string entryId, int processed, int totalRecords);

    [LoggerMessage(
        EventId = CrestAppsEventIds.ContentTransfer + 103,
        Level = LogLevel.Information,
        Message = "The export of entry '{EntryId}' finished in {Elapsed}: {Written} of {Processed} content items written, {FileSize} bytes.")]
    public static partial void ExportFinished(ILogger logger, string entryId, TimeSpan elapsed, int written, int processed, long fileSize);

    [LoggerMessage(
        EventId = CrestAppsEventIds.ContentTransfer + 104,
        Level = LogLevel.Error,
        Message = "The export of entry '{EntryId}' failed and was marked as failed.")]
    public static partial void ExportFailed(ILogger logger, Exception exception, string entryId);

    [LoggerMessage(
        EventId = CrestAppsEventIds.ContentTransfer + 105,
        Level = LogLevel.Warning,
        Message = "The export of entry '{EntryId}' is complete, but the notification that it is ready could not be sent.")]
    public static partial void ExportNotificationFailed(ILogger logger, Exception exception, string entryId);

    [LoggerMessage(
        EventId = CrestAppsEventIds.ContentTransfer + 106,
        Level = LogLevel.Information,
        Message = "The export of entry '{EntryId}' stopped because the host is stopping. It starts over on the next run.")]
    public static partial void ExportStopped(ILogger logger, string entryId);

    [LoggerMessage(
        EventId = CrestAppsEventIds.ContentTransfer + 107,
        Level = LogLevel.Error,
        Message = "The export of entry '{EntryId}' failed and was marked as failed ({Reason}).")]
    public static partial void ExportEntryFailed(ILogger logger, string entryId, string reason);

    [LoggerMessage(
        EventId = CrestAppsEventIds.ContentTransfer + 200,
        Level = LogLevel.Information,
        Message = "The import of entry '{EntryId}' of content type '{ContentType}' was queued for a file of {FileSize} bytes.")]
    public static partial void ImportQueued(ILogger logger, string entryId, string contentType, long fileSize);

    [LoggerMessage(
        EventId = CrestAppsEventIds.ContentTransfer + 201,
        Level = LogLevel.Information,
        Message = "An export of content type '{ContentType}' matches {TotalCount} content items, the queue threshold is {Threshold} and an export option requires the queue: {RequiresQueue}. The export runs {Mode}.")]
    public static partial void ExportQueueDecision(ILogger logger, string contentType, int totalCount, int threshold, bool requiresQueue, string mode);

    [LoggerMessage(
        EventId = CrestAppsEventIds.ContentTransfer + 202,
        Level = LogLevel.Error,
        Message = "The '{Job}' job of entry '{EntryId}', started after the request that queued it, failed.")]
    public static partial void BackgroundJobFailed(ILogger logger, Exception exception, string job, string entryId);

    [LoggerMessage(
        EventId = CrestAppsEventIds.ContentTransfer + 210,
        Level = LogLevel.Information,
        Message = "Removed {Count} abandoned temporary files of chunked import uploads.")]
    public static partial void UploadTempFilesPurged(ILogger logger, int count);
}
