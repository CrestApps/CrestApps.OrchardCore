using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// The log messages of the Reports module. Their event ids are <see cref="CrestAppsEventIds.Reports"/> + 100 to
/// <see cref="CrestAppsEventIds.Reports"/> + 399; the report engine in Reports.Core uses the hundred before them.
/// </summary>
internal static partial class ReportsModuleLog
{
    [LoggerMessage(
        EventId = CrestAppsEventIds.Reports + 101,
        Level = LogLevel.Warning,
        Message = "The designed report '{ReportId}' ({Source}) ran in {DurationMilliseconds} ms and could not show its result: {ErrorCount} errors.")]
    public static partial void RunEndedWithErrors(ILogger logger, string reportId, string source, long durationMilliseconds, int errorCount);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Reports + 102,
        Level = LogLevel.Warning,
        Message = "The designed report '{ReportId}' ({Source}) took {DurationMilliseconds} ms to run, more than {ThresholdMilliseconds} ms, for {RowCount} rows and {WarningCount} warnings.")]
    public static partial void SlowRun(ILogger logger, string reportId, string source, long durationMilliseconds, long thresholdMilliseconds, int rowCount, int warningCount);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Reports + 103,
        Level = LogLevel.Information,
        Message = "Report view refresh: {DueCount} views due, {RefreshedCount} refreshed, {FailedCount} failed, {SkippedCount} skipped because busy or no longer due.")]
    public static partial void ViewSweepCompleted(ILogger logger, int dueCount, int refreshedCount, int failedCount, int skippedCount);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Reports + 104,
        Level = LogLevel.Information,
        Message = "Report subscriptions: {DueCount} due, {SentCount} sent, {NotSentCount} not sent, {FailedCount} failed.")]
    public static partial void SubscriptionSweepCompleted(ILogger logger, int dueCount, int sentCount, int notSentCount, int failedCount);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Reports + 105,
        Level = LogLevel.Error,
        Message = "The report subscription '{SubscriptionId}' could not be processed; the other due subscriptions still run.")]
    public static partial void SubscriptionFailed(ILogger logger, Exception exception, string subscriptionId);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Reports + 106,
        Level = LogLevel.Warning,
        Message = "The report subscription '{SubscriptionId}' sent nothing ({Reason}).")]
    public static partial void SubscriptionNotSent(ILogger logger, string subscriptionId, string reason);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Reports + 107,
        Level = LogLevel.Debug,
        Message = "The report presence hub refused '{Operation}' for the report '{ReportId}': the caller may not edit it.")]
    public static partial void HubCallDenied(ILogger logger, string operation, string reportId);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Reports + 108,
        Level = LogLevel.Debug,
        Message = "A connection to the report presence hub opened.")]
    public static partial void HubConnected(ILogger logger);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Reports + 109,
        Level = LogLevel.Debug,
        Message = "A connection to the report presence hub closed after following {ReportCount} reports.")]
    public static partial void HubDisconnected(ILogger logger, Exception exception, int reportCount);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Reports + 110,
        Level = LogLevel.Debug,
        Message = "The report designer found {ErrorCount} problems in the query it checked.")]
    public static partial void DesignValidationFailed(ILogger logger, int errorCount);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Reports + 111,
        Level = LogLevel.Information,
        Message = "The report designs recipe step imported {DesignCount} reports, {ViewCount} views and {GroupCount} groups.")]
    public static partial void RecipeStepImported(ILogger logger, int designCount, int viewCount, int groupCount);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Reports + 112,
        Level = LogLevel.Information,
        Message = "The report designs deployment step exported {DesignCount} reports, {ViewCount} views and {GroupCount} groups.")]
    public static partial void DeploymentStepExported(ILogger logger, int designCount, int viewCount, int groupCount);
}
