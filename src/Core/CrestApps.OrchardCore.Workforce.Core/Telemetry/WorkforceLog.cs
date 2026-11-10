using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Workforce.Core.Telemetry;

/// <summary>
/// The log messages of the workforce services, with event ids 13200 to 13299 of <see cref="CrestAppsEventIds.Scheduling"/>.
/// </summary>
internal static partial class WorkforceLog
{
    /// <summary>
    /// A payroll export started.
    /// </summary>
    [LoggerMessage(EventId = CrestAppsEventIds.Scheduling + 200, Level = LogLevel.Information, Message = "Exporting the payroll of the pay period calendar '{CalendarId}' for the period from {PeriodStart} to {PeriodEnd} through '{Provider}'.")]
    public static partial void PayrollExportStarted(ILogger logger, string calendarId, DateOnly periodStart, DateOnly periodEnd, string provider);

    /// <summary>
    /// A payroll export was not run because someone else is exporting the same pay period.
    /// </summary>
    [LoggerMessage(EventId = CrestAppsEventIds.Scheduling + 201, Level = LogLevel.Information, Message = "The payroll of the pay period calendar '{CalendarId}' for the period starting {PeriodStart} was not exported: someone else is exporting it.")]
    public static partial void PayrollExportBusy(ILogger logger, string calendarId, DateOnly periodStart);

    /// <summary>
    /// A payroll export finished.
    /// </summary>
    [LoggerMessage(EventId = CrestAppsEventIds.Scheduling + 202, Level = LogLevel.Information, Message = "Exported {TimesheetCount} timesheets in {RowCount} rows of the pay period calendar '{CalendarId}' for the period starting {PeriodStart} as batch '{BatchId}' in {ElapsedMilliseconds} ms.")]
    public static partial void PayrollExportCompleted(ILogger logger, int timesheetCount, int rowCount, string calendarId, DateOnly periodStart, string batchId, long elapsedMilliseconds);

    /// <summary>
    /// A payroll export found nothing to export.
    /// </summary>
    [LoggerMessage(EventId = CrestAppsEventIds.Scheduling + 203, Level = LogLevel.Information, Message = "The payroll of the pay period calendar '{CalendarId}' for the period starting {PeriodStart} was not exported: {Reason}.")]
    public static partial void PayrollExportRejected(ILogger logger, string calendarId, DateOnly periodStart, string reason);

    /// <summary>
    /// A payroll export failed.
    /// </summary>
    [LoggerMessage(EventId = CrestAppsEventIds.Scheduling + 204, Level = LogLevel.Error, Message = "The payroll export for the period starting {PeriodStart} failed.")]
    public static partial void PayrollExportFailed(ILogger logger, Exception exception, DateOnly periodStart);
}
