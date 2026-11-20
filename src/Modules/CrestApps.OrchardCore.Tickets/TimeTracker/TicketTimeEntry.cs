namespace CrestApps.OrchardCore.Tickets.TimeTracker;

/// <summary>
/// One span of work on a ticket, from the timer or logged after the fact.
/// </summary>
/// <param name="Name">Who worked.</param>
/// <param name="StartUtc">When they started, in UTC.</param>
/// <param name="EndUtc">When they stopped, in UTC, or <see langword="null"/> while they are at it.</param>
/// <param name="Minutes">How long, up to now while they are at it.</param>
/// <param name="Note">What they did.</param>
/// <param name="Billable">Whether it is billed to the customer.</param>
public sealed record TicketTimeEntry(string Name, DateTime StartUtc, DateTime? EndUtc, int Minutes, string Note, bool Billable);
