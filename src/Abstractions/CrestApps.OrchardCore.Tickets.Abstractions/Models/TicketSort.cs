namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// The order of a ticket list.
/// </summary>
public enum TicketSort
{
    /// <summary>
    /// Most recently updated first.
    /// </summary>
    Updated,

    /// <summary>
    /// Newest first.
    /// </summary>
    Created,

    /// <summary>
    /// The soonest service-level due time first.
    /// </summary>
    Due,

    /// <summary>
    /// The most urgent priority first.
    /// </summary>
    Priority,
}
