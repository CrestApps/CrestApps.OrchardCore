using System.Security.Claims;
using CrestApps.OrchardCore.Tickets.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.Services;

/// <summary>
/// Says what a user may do with tickets, from the per-desk permissions and the scope of what they may view. Lists, the
/// API and the report data sets narrow their queries with <see cref="ApplyScopeAsync"/>.
/// </summary>
public interface ITicketAccessService
{
    /// <summary>
    /// Returns whether a user may do something with a ticket.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="ticket">The ticket.</param>
    /// <param name="operation">What they want to do.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether they may.</returns>
    Task<bool> CanAsync(ClaimsPrincipal user, ContentItem ticket, TicketOperation operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns, for each desk, which of its tickets a user may view.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The scope per desk id; desks the user may not view are left out.</returns>
    Task<IReadOnlyDictionary<string, TicketViewScope>> GetViewScopesAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Narrows a query to the tickets a user may view.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The scoped query, or <see langword="null"/> when the user may view no tickets.</returns>
    Task<TicketScopedQuery> ApplyScopeAsync(ClaimsPrincipal user, TicketQuery query, CancellationToken cancellationToken = default);
}
