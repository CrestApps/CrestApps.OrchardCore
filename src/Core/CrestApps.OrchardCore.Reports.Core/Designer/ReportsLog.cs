using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// The log messages of the report engine and the coded reports. Their event ids are
/// <see cref="CrestAppsEventIds.Reports"/> to <see cref="CrestAppsEventIds.Reports"/> + 99; the Reports module uses the
/// next 300, the PDF export the 50 after them.
/// </summary>
internal static partial class ReportsLog
{
    [LoggerMessage(
        EventId = CrestAppsEventIds.Reports + 1,
        Level = LogLevel.Warning,
        Message = "The report '{ReportName}' read {RowCount} index rows at once, at least {Threshold}. Narrow its filter or read the index a page at a time.")]
    public static partial void CodedIndexRowsAboveThreshold(ILogger logger, string reportName, long rowCount, long threshold);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Reports + 2,
        Level = LogLevel.Debug,
        Message = "The temporary files of a report query could not be deleted from '{Directory}'; the system's temporary file clean-up removes them.")]
    public static partial void SpillCleanupFailed(ILogger logger, Exception exception, string directory);
}
