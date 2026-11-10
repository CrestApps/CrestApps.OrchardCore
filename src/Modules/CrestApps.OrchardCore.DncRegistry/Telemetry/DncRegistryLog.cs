using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.DncRegistry.Telemetry;

/// <summary>
/// The log messages of the do-not-call registry module, with their event ids. They never carry a phone number.
/// </summary>
internal static partial class DncRegistryLog
{
    private const int FirstEventId = CrestAppsEventIds.ContentTransfer + 400;

    /// <summary>
    /// Logs how one run of the import of a local list ended.
    /// </summary>
    [LoggerMessage(
        EventId = FirstEventId,
        Level = LogLevel.Information,
        Message = "The import run of local DNC list '{ListId}' ended as {Outcome} after {ElapsedSeconds} s: {ImportedRows} rows saved and {RejectedRows} rows rejected in this run.")]
    public static partial void ImportRunEnded(ILogger logger, string listId, string outcome, double elapsedSeconds, long importedRows, long rejectedRows);

    /// <summary>
    /// Logs what one run of the local do-not-call background task did.
    /// </summary>
    [LoggerMessage(
        EventId = FirstEventId + 1,
        Level = LogLevel.Information,
        Message = "The local DNC task worked on {ImportCount} imports and {DeletionCount} deletions: {FailedCount} failed and {ContendedCount} were held by another worker.")]
    public static partial void TaskRunSummary(ILogger logger, int importCount, int deletionCount, int failedCount, int contendedCount);

    /// <summary>
    /// Logs a job that failed after the request that queued it ended.
    /// </summary>
    [LoggerMessage(
        EventId = FirstEventId + 2,
        Level = LogLevel.Error,
        Message = "The {Job} job of local DNC list '{ListId}' failed after the request that queued it ended.")]
    public static partial void BackgroundJobFailed(ILogger logger, Exception exception, string job, string listId);
}
