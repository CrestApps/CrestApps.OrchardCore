using CrestApps.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Core.Services;

/// <summary>
/// Stores the payment terms in the Transaction collection.
/// </summary>
public interface IPaymentTermStore : ICatalog<PaymentTerm>
{
    /// <summary>
    /// Returns the terms that may be chosen for new documents, by name.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The enabled terms.</returns>
    Task<IReadOnlyList<PaymentTerm>> GetEnabledAsync(CancellationToken cancellationToken = default);
}
