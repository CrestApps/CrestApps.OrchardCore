using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Transactions.Core.Services;

/// <summary>
/// The YesSql store of the payment terms.
/// </summary>
public sealed class PaymentTermStore : DocumentCatalog<PaymentTerm, PaymentTermIndex>, IPaymentTermStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentTermStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public PaymentTermStore(ISession session)
        : base(session)
    {
        CollectionName = TransactionsConstants.CollectionName;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PaymentTerm>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        var terms = await Session.Query<PaymentTerm, PaymentTermIndex>(index => index.Enabled, collection: CollectionName)
            .OrderBy(index => index.Name)
            .ListAsync(cancellationToken);

        return terms.ToArray();
    }
}
