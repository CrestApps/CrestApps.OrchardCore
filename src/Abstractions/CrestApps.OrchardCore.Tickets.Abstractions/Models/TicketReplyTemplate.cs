namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// A reply template as a ticket's composer offers it: its text for the ticket, and the changes it makes to the ticket
/// when used (a macro).
/// </summary>
public sealed class TicketReplyTemplate
{
    /// <summary>
    /// The keys of a template's properties that hold its changes to a ticket.
    /// </summary>
    public static class PropertyKeys
    {
        /// <summary>
        /// The status the ticket moves to.
        /// </summary>
        public const string StatusId = "Tickets.StatusId";

        /// <summary>
        /// The priority the ticket gets.
        /// </summary>
        public const string PriorityId = "Tickets.PriorityId";

        /// <summary>
        /// The team the ticket goes to.
        /// </summary>
        public const string TeamId = "Tickets.TeamId";
    }

    /// <summary>
    /// Gets the template's id.
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// Gets the template's name.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets the text, its placeholders filled in for the ticket.
    /// </summary>
    public string Body { get; init; }

    /// <summary>
    /// Gets the status the ticket moves to when the template is used, if any.
    /// </summary>
    public string StatusId { get; init; }

    /// <summary>
    /// Gets the priority the ticket gets when the template is used, if any.
    /// </summary>
    public string PriorityId { get; init; }

    /// <summary>
    /// Gets the team the ticket goes to when the template is used, if any.
    /// </summary>
    public string TeamId { get; init; }
}
