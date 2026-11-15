using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// Gives the link a requester follows to see their ticket on the site, for the e-mails they get. The Tickets Portal
/// feature registers one; without it, e-mails carry no link.
/// </summary>
public interface ITicketRequesterLinks
{
    /// <summary>
    /// Returns the absolute link to a ticket for its requester: their account page when they have a login, otherwise a
    /// signed link that works without one. Returns <see langword="null"/> when no link can be built (no site address).
    /// </summary>
    /// <param name="ticket">The ticket.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The link, or <see langword="null"/>.</returns>
    Task<string> GetLinkAsync(ContentItem ticket, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the absolute, signed link a requester follows to rate a ticket, or <see langword="null"/> when none can
    /// be built.
    /// </summary>
    /// <param name="ticket">The ticket.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The link, or <see langword="null"/>.</returns>
    Task<string> GetRatingLinkAsync(ContentItem ticket, CancellationToken cancellationToken = default);
}
