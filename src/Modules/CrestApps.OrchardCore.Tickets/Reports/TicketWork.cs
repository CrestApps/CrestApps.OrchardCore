using CrestApps.OrchardCore.TimeTracker.Models;

namespace CrestApps.OrchardCore.Tickets.Reports;

/// <summary>
/// A span of work on a ticket the reader may see, clipped to the report's period.
/// </summary>
/// <param name="Ticket">The ticket.</param>
/// <param name="Entry">The time entry.</param>
/// <param name="StartUtc">The start, within the period, in UTC.</param>
/// <param name="EndUtc">The end, within the period (now while the work goes on), in UTC.</param>
/// <param name="Billable">Whether it is billed to the customer.</param>
internal sealed record TicketWork(TicketFacts Ticket, TimeEntry Entry, DateTime StartUtc, DateTime EndUtc, bool Billable)
{
    public double Minutes => (EndUtc - StartUtc).TotalMinutes;
}
