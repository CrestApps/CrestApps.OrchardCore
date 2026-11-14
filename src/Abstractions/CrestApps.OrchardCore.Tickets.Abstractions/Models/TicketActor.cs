using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// Who changes a ticket, for its timeline: a person, or the site itself (a rule, a workflow, a background task).
/// </summary>
public sealed class TicketActor
{
    /// <summary>
    /// Gets the site itself.
    /// </summary>
    public static TicketActor Site { get; } = new();

    /// <summary>
    /// Gets or sets the person, as a party; empty for the site.
    /// </summary>
    public PartyRef Party { get; init; }

    /// <summary>
    /// Gets or sets the person's name, for someone no party source knows.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets or sets whether a workflow makes the change, so a chain of workflow changes can be stopped.
    /// </summary>
    public bool FromWorkflow { get; init; }

    /// <summary>
    /// Returns the actor of a user.
    /// </summary>
    /// <param name="userId">The user's id.</param>
    /// <param name="name">The user's name.</param>
    /// <returns>The actor.</returns>
    public static TicketActor User(string userId, string name = null)
        => string.IsNullOrEmpty(userId) ? Site : new TicketActor { Party = new PartyRef(PartyKinds.User, userId), Name = name };
}
