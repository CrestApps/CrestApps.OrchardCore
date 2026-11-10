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
        EventId = CrestAppsEventIds.Reports + 107,
        Level = LogLevel.Debug,
        Message = "The report presence hub refused '{Operation}' for the report '{ReportId}': the caller may not edit it.")]
    public static partial void HubCallDenied(ILogger logger, string operation, string reportId);

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
