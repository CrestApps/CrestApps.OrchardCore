using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

/// <summary>
/// Releases a site that was bought with a promise to pay once what pays for it is paid: the transaction of the site's
/// obligation, or the last payment of the installment plan that replaced it. The provisioning job waiting for the payment
/// becomes due.
/// </summary>
public sealed class TenantProvisioningPaymentReceivedHandler : TransactionEventHandlerBase
{
    private readonly TenantProvisioningPaymentRelease _release;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantProvisioningPaymentReceivedHandler"/> class.
    /// </summary>
    /// <param name="release">The service that releases a site once it is paid for.</param>
    public TenantProvisioningPaymentReceivedHandler(TenantProvisioningPaymentRelease release)
    {
        _release = release;
    }

    /// <inheritdoc/>
    public override Task StatusChangedAsync(Transaction transaction, TransactionStatus previous, TransactionStatus current, CancellationToken cancellationToken = default)
    {
        if (current != TransactionStatus.Paid || transaction is null)
        {
            return Task.CompletedTask;
        }

        return _release.ReleaseForPaymentAsync(transaction, cancellationToken);
    }
}
