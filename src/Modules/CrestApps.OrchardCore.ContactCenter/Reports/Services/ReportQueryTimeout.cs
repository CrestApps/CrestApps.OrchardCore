using System.Data.Common;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// Recognizes a report query the database gave up on, and describes it to the person who asked for the report.
/// </summary>
/// <remarks>
/// A report over a wide period reads the whole window of the event log. When that read outlasts the database's
/// command timeout, the provider-specific exception would otherwise surface as an HTTP 500 and, on a busy tenant,
/// hold a database connection for the full timeout. Naming the cause and what to change is the actionable answer.
/// </remarks>
internal static class ReportQueryTimeout
{
    /// <summary>
    /// Determines whether an exception is a database command timeout.
    /// </summary>
    /// <param name="exception">The exception a report query raised.</param>
    /// <returns><see langword="true"/> when the exception, or one it wraps, reports that a command timed out.</returns>
    /// <remarks>
    /// Database providers do not share a timeout exception type: SQL Server reports "Execution Timeout Expired" through
    /// its own <see cref="DbException"/>, and other providers wrap a <see cref="TimeoutException"/>. A cancelled
    /// request is deliberately not a timeout, so a visitor who navigates away is not shown an error nobody reads.
    /// </remarks>
    public static bool IsTimeout(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is OperationCanceledException)
            {
                return false;
            }

            if (current is TimeoutException)
            {
                return true;
            }

            if (current is DbException && current.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Builds the document shown in place of a report whose query timed out.
    /// </summary>
    /// <param name="localizer">The localizer for the message.</param>
    /// <returns>A document that explains the timeout and carries no measurements.</returns>
    public static ReportDocument Describe(IStringLocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        var section = new ReportSection
        {
            Title = localizer["This period is too large to report"].Value,
            Description = localizer["The database did not finish reading this period's activity in time, so no figures are shown. Choose an agent or a shorter period and run the report again."].Value,
            Kind = ReportSectionKind.Metrics,
        };

        return new ReportDocument().Add(section);
    }
}
