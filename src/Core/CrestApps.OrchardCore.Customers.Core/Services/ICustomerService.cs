using CrestApps.OrchardCore.Customers.Core.Models;

namespace CrestApps.OrchardCore.Customers.Core.Services;

/// <summary>
/// Finds and keeps the store's customers.
/// </summary>
public interface ICustomerService
{
    /// <summary>
    /// The customer of a user, made from the account the first time the user buys or opens their account page.
    /// Returns <see langword="null"/> when the user does not exist.
    /// </summary>
    Task<Customer> GetOrCreateForUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The customer of a user, or <see langword="null"/> when the user has none yet.
    /// </summary>
    Task<Customer> FindForUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notes that the customer bought or changed something, which keeps their data for the retention window.
    /// </summary>
    Task TouchAsync(Customer customer, CancellationToken cancellationToken = default);
}
