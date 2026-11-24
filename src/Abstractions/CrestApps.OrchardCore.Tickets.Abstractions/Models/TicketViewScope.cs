namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// Which of a desk's tickets a user may view.
/// </summary>
public enum TicketViewScope
{
    /// <summary>
    /// None.
    /// </summary>
    None,

    /// <summary>
    /// The tickets assigned to them.
    /// </summary>
    Assigned,

    /// <summary>
    /// The tickets of their teams and those assigned to them.
    /// </summary>
    Teams,

    /// <summary>
    /// Every ticket.
    /// </summary>
    All,
}
