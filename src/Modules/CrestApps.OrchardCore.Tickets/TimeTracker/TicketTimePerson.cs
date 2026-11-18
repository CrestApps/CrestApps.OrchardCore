namespace CrestApps.OrchardCore.Tickets.TimeTracker;

/// <summary>
/// One person's time on a ticket.
/// </summary>
/// <param name="UserId">The user.</param>
/// <param name="Name">Their name.</param>
/// <param name="Minutes">Their minutes.</param>
public sealed record TicketTimePerson(string UserId, string Name, int Minutes);
