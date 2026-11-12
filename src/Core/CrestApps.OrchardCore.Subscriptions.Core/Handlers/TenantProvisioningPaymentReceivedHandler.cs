using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

/// <summary>
/// Releases a site that was bought with a promise to pay once its transaction is paid: the provisioning job waiting for
/// the payment becomes due.
/// </summary>
public sealed class TenantProvisioningPaymentReceivedHandler : TransactionEventHandlerBase
{
    private readonly ITenantProvisioningJobStore _jobStore;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantProvisioningPaymentReceivedHandler"/> class.
    /// </summary>
    public TenantProvisioningPaymentReceivedHandler(ITenantProvisioningJobStore jobStore, IClock clock)
    {
        _jobStore = jobStore;
        _clock = clock;
    }

    /// <inheritdoc/>
    public override async Task StatusChangedAsync(Transaction transaction, TransactionStatus previous, TransactionStatus current, CancellationToken cancellationToken = default)
    {
        if (current != TransactionStatus.Paid || string.IsNullOrEmpty(transaction?.CheckoutSessionId))
        {
            return;
        }

        var job = await _jobStore.GetByCheckoutSessionAsync(transaction.CheckoutSessionId, cancellationToken);

        if (job is null || job.Status != TenantProvisioningStatus.AwaitingPayment)
        {
            return;
        }

        job.Status = TenantProvisioningStatus.Pending;
        job.NextAttemptUtc = null;
        job.UpdatedUtc = _clock.UtcNow;

        await _jobStore.UpdateAsync(job, cancellationToken);
    }
}
