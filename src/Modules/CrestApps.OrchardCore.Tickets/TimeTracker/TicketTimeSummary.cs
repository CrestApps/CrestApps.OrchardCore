namespace CrestApps.OrchardCore.Tickets.TimeTracker;

/// <summary>
/// The time worked on a ticket.
/// </summary>
/// <param name="People">Each person's minutes, most first.</param>
/// <param name="TotalMinutes">All of it.</param>
/// <param name="WorkingSinceUtc">When the user asking started working on it, while they are.</param>
public sealed record TicketTimeSummary(IReadOnlyList<TicketTimePerson> People, int TotalMinutes, DateTime? WorkingSinceUtc)
{
    /// <summary>
    /// Gets the entries, newest first.
    /// </summary>
    public IReadOnlyList<TicketTimeEntry> Entries { get; init; } = [];

    /// <summary>
    /// Gets the minutes not billed to the customer.
    /// </summary>
    public int NotBilledMinutes { get; init; }

    /// <summary>
    /// Gets whether the ticket's desk records work not billed in a stage of its own, so logged time may be billable or
    /// not.
    /// </summary>
    public bool CanChooseBillable { get; init; }
}
