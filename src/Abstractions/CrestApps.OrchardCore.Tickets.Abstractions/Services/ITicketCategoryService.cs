using CrestApps.OrchardCore.Tickets.Models;

namespace CrestApps.OrchardCore.Tickets.Services;

/// <summary>
/// Reads the categories of ticket types: the terms of the taxonomy a type names
/// (<see cref="TicketPartSettings.CategoryTaxonomyId"/>), with the routing each gives its tickets.
/// </summary>
public interface ITicketCategoryService
{
    /// <summary>
    /// Lists the categories of a ticket type, in tree order.
    /// </summary>
    /// <param name="contentType">The ticket type.</param>
    /// <param name="customersOnly">Whether to keep only those customers can choose.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The categories; none when the type has no taxonomy.</returns>
    Task<IReadOnlyList<TicketCategory>> ListAsync(string contentType, bool customersOnly = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds one category of a ticket type.
    /// </summary>
    /// <param name="contentType">The ticket type.</param>
    /// <param name="categoryId">The term's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The category, or <see langword="null"/> when it isn't one of the type's.</returns>
    Task<TicketCategory> FindAsync(string contentType, string categoryId, CancellationToken cancellationToken = default);
}
