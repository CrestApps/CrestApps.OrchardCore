using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.Services;

/// <summary>
/// The named actions an agent can run on a ticket from its page, such as "Escalate to tier 2". With Workflows on, each
/// is a published workflow that starts with <b>Ticket Action Requested</b>.
/// </summary>
public interface ITicketActionSource
{
    /// <summary>
    /// Lists the actions offered on a ticket, by name.
    /// </summary>
    /// <param name="ticket">The ticket.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The action names.</returns>
    Task<IReadOnlyList<string>> ListAsync(ContentItem ticket, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs an action on a ticket.
    /// </summary>
    /// <param name="ticket">The ticket.</param>
    /// <param name="actionName">The action's name.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when the action is offered on the ticket and was started.</returns>
    Task<bool> RequestAsync(ContentItem ticket, string actionName, CancellationToken cancellationToken = default);
}
