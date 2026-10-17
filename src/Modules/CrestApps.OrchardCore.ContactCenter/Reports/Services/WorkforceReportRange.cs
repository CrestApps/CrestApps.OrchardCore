using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// Bounds the period an agent workforce report reads from the agent state log.
/// </summary>
/// <remarks>
/// These reports read every state change of every agent in the period, so the cost of one request grows with the
/// length of the period. A period past the configured limit is refused, with a message that says what happened and
/// what to change, rather than clamped: a payroll or utilization figure for a shorter period than the one asked for
/// would read as an answer to the question that was asked.
/// </remarks>
internal static class WorkforceReportRange
{
    /// <summary>
    /// Gets the widest period a report run may read.
    /// </summary>
    /// <param name="mode">Whether the report is shown or exported.</param>
    /// <param name="options">The reporting options.</param>
    /// <returns>The export limit for an export; otherwise the browser limit.</returns>
    public static TimeSpan LimitFor(ReportRunMode mode, ContactCenterReportingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return mode == ReportRunMode.Export
            ? options.MaximumWorkforceExportRange
            : options.MaximumWorkforceReportRange;
    }

    /// <summary>
    /// Decides whether a report run's period is longer than the limit, and describes the refusal when it is.
    /// </summary>
    /// <param name="context">The report run.</param>
    /// <param name="options">The reporting options.</param>
    /// <param name="timeZone">The tenant's time zone, in which the period was chosen and is measured.</param>
    /// <param name="localizer">The localizer for the message.</param>
    /// <param name="refusal">The document shown instead of the report, when the period is too long.</param>
    /// <returns><see langword="true"/> when the period is longer than the limit.</returns>
    public static bool TryRefuse(
        ReportContext context,
        ContactCenterReportingOptions options,
        ReportTimeZone timeZone,
        IStringLocalizer localizer,
        out ReportDocument refusal)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeZone);
        ArgumentNullException.ThrowIfNull(localizer);

        refusal = null;

        var range = context.Filter.GetDateRange();

        if (!range.FromUtc.HasValue || !range.ToUtc.HasValue)
        {
            return false;
        }

        // The period was chosen as local dates, so it is measured on the local clock: a calendar month that crosses a
        // daylight-saving change is still the month's days, not the month and an hour.
        var span = (timeZone.ToLocal(range.ToUtc.Value) - timeZone.ToLocal(range.FromUtc.Value)).Duration();
        var limit = LimitFor(context.Mode, options);

        if (span <= limit)
        {
            return false;
        }

        var limitDays = limit.TotalDays.ToString("0.##", CultureInfo.CurrentCulture);
        var chosenDays = Math.Ceiling(span.TotalDays).ToString("0", CultureInfo.CurrentCulture);

        var section = new ReportSection
        {
            Title = localizer["This period is too long for this report"].Value,
            Description = context.Mode == ReportRunMode.Export
                ? localizer["An export of this report reads at most {0} days of agent activity at a time, and the chosen period covers {1} days, so nothing was exported. Choose a period of {0} days or fewer and export again.", limitDays, chosenDays].Value
                : localizer["This report reads at most {0} days of agent activity at a time, and the chosen period covers {1} days, so no figures are shown. Choose a period of {0} days or fewer and run the report again.", limitDays, chosenDays].Value,
            Kind = ReportSectionKind.Metrics,
        };

        refusal = new ReportDocument().Add(section);

        return true;
    }
}
