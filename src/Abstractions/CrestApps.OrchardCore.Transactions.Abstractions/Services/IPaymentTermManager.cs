using CrestApps.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Creates, reads, changes and deletes the store's payment terms, running the catalog handlers. It is registered by the
/// Payment Terms feature.
/// </summary>
public interface IPaymentTermManager : ICatalogManager<PaymentTerm>
{
    /// <summary>
    /// Returns the terms that may be chosen for new documents, by name.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The enabled terms.</returns>
    Task<IReadOnlyList<PaymentTerm>> GetEnabledAsync(CancellationToken cancellationToken = default);
}
