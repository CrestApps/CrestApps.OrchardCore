using CrestApps.OrchardCore.Tickets.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.Services;

/// <summary>
/// The reply templates an agent can insert in a ticket's composer, with their placeholders filled in for the ticket.
/// Registered when a module keeps reply templates (Messaging's templates of the Tickets scope); without one, the composer
/// offers none.
/// </summary>
public interface ITicketReplyTemplateSource
{
    /// <summary>
    /// Lists the templates for a ticket, by name.
    /// </summary>
    /// <param name="ticket">The ticket the reply is for.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The templates, their bodies filled in for the ticket.</returns>
    Task<IReadOnlyList<TicketReplyTemplate>> ListAsync(ContentItem ticket, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds one template for a ticket.
    /// </summary>
    /// <param name="templateId">The template.</param>
    /// <param name="ticket">The ticket the reply is for.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The template, or <see langword="null"/>.</returns>
    Task<TicketReplyTemplate> FindAsync(string templateId, ContentItem ticket, CancellationToken cancellationToken = default);
}
