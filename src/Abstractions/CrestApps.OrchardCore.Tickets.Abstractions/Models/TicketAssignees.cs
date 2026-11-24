namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// The special assignees a <see cref="TicketQuery"/> can name.
/// </summary>
public static class TicketAssignees
{
    /// <summary>
    /// The user who lists the tickets.
    /// </summary>
    public const string Me = "me";

    /// <summary>
    /// Nobody.
    /// </summary>
    public const string Unassigned = "none";
}
