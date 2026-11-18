using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tickets.Reports;

/// <summary>
/// Sections the ticket reports share.
/// </summary>
internal static class TicketReportSections
{
    /// <summary>
    /// The report of a reader who may see no ticket.
    /// </summary>
    public static ReportDocument NothingToShow(IStringLocalizer S)
    {
        var section = ReportSection.ForTable(S["Tickets"].Value, [], []);
        section.EmptyMessage = S["You can't see any tickets, so there is nothing to report."].Value;

        return new ReportDocument().Add(section);
    }
}
