namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// A ticket query narrowed to what a user may view: per desk, every ticket, those of the user's teams, or those
/// assigned to the user.
/// </summary>
public sealed class TicketScopedQuery
{
    /// <summary>
    /// Gets the query.
    /// </summary>
    public TicketQuery Query { get; init; }

    /// <summary>
    /// Gets the desks whose every ticket may be viewed.
    /// </summary>
    public IReadOnlyList<string> AllDeskIds { get; init; } = [];

    /// <summary>
    /// Gets the desks whose tickets may be viewed when they belong to <see cref="TeamIds"/> or are assigned to the user.
    /// </summary>
    public IReadOnlyList<string> TeamDeskIds { get; init; } = [];

    /// <summary>
    /// Gets the desks whose tickets may be viewed when they are assigned to the user.
    /// </summary>
    public IReadOnlyList<string> AssignedDeskIds { get; init; } = [];

    /// <summary>
    /// Gets the user's teams.
    /// </summary>
    public IReadOnlyList<string> TeamIds { get; init; } = [];

    /// <summary>
    /// Gets the user's id.
    /// </summary>
    public string UserId { get; init; }
}
