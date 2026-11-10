using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Telemetry;

/// <summary>
/// The log messages of the Omnichannel management bulk work, with their event ids. They carry ids and counts, never
/// a phone number or any other contact data.
/// </summary>
internal static partial class OmnichannelLog
{
    private const int FirstEventId = CrestAppsEventIds.ContentTransfer + 500;

    /// <summary>
    /// Logs a load that was not run because this process is already running it.
    /// </summary>
    [LoggerMessage(
        EventId = FirstEventId,
        Level = LogLevel.Information,
        Message = "The activity load '{BatchId}' was not run because this process is already running it.")]
    public static partial void BatchLoadAlreadyRunning(ILogger logger, string batchId);

    /// <summary>
    /// Logs how a run of an activity load ended.
    /// </summary>
    [LoggerMessage(
        EventId = FirstEventId + 1,
        Level = LogLevel.Information,
        Message = "The {Trigger} run of the activity load '{BatchId}' ended as {Outcome} after {ElapsedSeconds} s.")]
    public static partial void BatchLoadEnded(ILogger logger, string trigger, string batchId, string outcome, double elapsedSeconds);

    /// <summary>
    /// Logs a load job that failed after the request that queued it ended.
    /// </summary>
    [LoggerMessage(
        EventId = FirstEventId + 2,
        Level = LogLevel.Error,
        Message = "The activity load '{BatchId}' failed to run after the request that queued it ended.")]
    public static partial void BatchLoadJobFailed(ILogger logger, Exception exception, string batchId);

    /// <summary>
    /// Logs the start of a bulk action on activities.
    /// </summary>
    [LoggerMessage(
        EventId = FirstEventId + 10,
        Level = LogLevel.Information,
        Message = "User '{UserId}' started the bulk action {Action} on {ActivityCount} activities (all matching the filter: {ApplyToAllMatching}); {InFlightCount} activities in a live dialer call were left out.")]
    public static partial void BulkActionStarting(ILogger logger, string userId, string action, int activityCount, bool applyToAllMatching, int inFlightCount);

    /// <summary>
    /// Logs the end of a bulk action on activities.
    /// </summary>
    [LoggerMessage(
        EventId = FirstEventId + 11,
        Level = LogLevel.Information,
        Message = "User '{UserId}' finished the bulk action {Action}: {ProcessedCount} of {ActivityCount} activities processed in {ElapsedSeconds} s.")]
    public static partial void BulkActionFinished(ILogger logger, string userId, string action, int processedCount, int activityCount, double elapsedSeconds);

    /// <summary>
    /// Logs a bulk action on activities that failed part-way.
    /// </summary>
    [LoggerMessage(
        EventId = FirstEventId + 12,
        Level = LogLevel.Error,
        Message = "The bulk action {Action} started by user '{UserId}' on {ActivityCount} activities failed after {ElapsedSeconds} s.")]
    public static partial void BulkActionFailed(ILogger logger, Exception exception, string action, string userId, int activityCount, double elapsedSeconds);

    /// <summary>
    /// Logs an import row a row filter skipped.
    /// </summary>
    [LoggerMessage(
        EventId = FirstEventId + 20,
        Level = LogLevel.Debug,
        Message = "Skipping row {RowIndex} of the import of entry '{EntryId}': {ReasonCode}.")]
    public static partial void ImportRowSkipped(ILogger logger, int rowIndex, string entryId, string reasonCode);

    /// <summary>
    /// Logs an import row whose number could not be screened against the do-not-call registries.
    /// </summary>
    [LoggerMessage(
        EventId = FirstEventId + 21,
        Level = LogLevel.Error,
        Message = "Skipping row {RowIndex} of the import of entry '{EntryId}': the do-not-call registry '{RegistryKey}' could not screen its numbers.")]
    public static partial void ImportRowScreeningFailed(ILogger logger, Exception exception, int rowIndex, string entryId, string registryKey);

    /// <summary>
    /// Logs an import row imported marked Do not call because a number is on a national registry.
    /// </summary>
    [LoggerMessage(
        EventId = FirstEventId + 22,
        Level = LogLevel.Debug,
        Message = "Importing row {RowIndex} of the import of entry '{EntryId}' marked Do not call: its {Label} is registered on a national do-not-call registry.")]
    public static partial void ImportRowMarkedDoNotCall(ILogger logger, int rowIndex, string entryId, string label);

    /// <summary>
    /// Logs an automated activity cancelled because its number is known not to be in service.
    /// </summary>
    [LoggerMessage(
        EventId = FirstEventId + 30,
        Level = LogLevel.Information,
        Message = "Activity '{ActivityId}' was cancelled instead of started on '{Channel}' because its number is known not to be in service.")]
    public static partial void AutomatedActivityNotInService(ILogger logger, string activityId, string channel);
}
