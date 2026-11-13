using CrestApps.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Transactions.Core.Services;

/// <summary>
/// The default <see cref="IPaymentTermManager"/>.
/// </summary>
public sealed class PaymentTermManager : CatalogManager<PaymentTerm>, IPaymentTermManager
{
    private readonly IPaymentTermStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentTermManager"/> class.
    /// </summary>
    /// <param name="store">The store of the terms.</param>
    /// <param name="handlers">The catalog handlers of the terms.</param>
    /// <param name="logger">The logger.</param>
    public PaymentTermManager(
        IPaymentTermStore store,
        IEnumerable<ICatalogEntryHandler<PaymentTerm>> handlers,
        ILogger<CatalogManager<PaymentTerm>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<PaymentTerm>> GetEnabledAsync(CancellationToken cancellationToken = default)
        => _store.GetEnabledAsync(cancellationToken);
}
