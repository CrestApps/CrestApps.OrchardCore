using CrestApps.OrchardCore.Customers.Core.Models;

namespace CrestApps.OrchardCore.Customers.Core.Services;

/// <summary>
/// Reacts to the customer store making a customer, whoever makes it: an administrator, a recipe, a checkout or an
/// account's first visit to its customer record.
/// </summary>
public interface ICustomerHandler
{
    /// <summary>
    /// Called after a new customer is added to the session. The customer is committed with the session.
    /// </summary>
    /// <param name="customer">The customer that was created.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the handler has run.</returns>
    Task CreatedAsync(Customer customer, CancellationToken cancellationToken = default);
}
