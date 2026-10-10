using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Transactions.Services;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

/// <summary>
/// Brings an installment plan up to date when one of its payments is received, however it was paid: by the saved
/// card, by the customer from their transactions, or recorded by hand. Receiving the down payment is what starts the
/// plan's schedule.
/// </summary>
public sealed class InstallmentPlanTransactionPaymentHandler : ITransactionPaymentHandler
{
    private readonly IInstallmentPlanService _planService;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstallmentPlanTransactionPaymentHandler"/> class.
    /// </summary>
    /// <param name="planService">The installment plan service.</param>
    public InstallmentPlanTransactionPaymentHandler(IInstallmentPlanService planService)
    {
        _planService = planService;
    }

    /// <inheritdoc/>
    public async Task PaymentRecordedAsync(TransactionPaymentRecordedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var transaction = context.Transaction;

        if (!string.Equals(transaction.ReferenceType, SubscriptionConstants.InstallmentPlans.ReferenceType, StringComparison.Ordinal) ||
            string.IsNullOrEmpty(transaction.ReferenceId))
        {
            return;
        }

        // Not waiting: if the plan is locked, it is the plan collecting this very payment, and it reads the result
        // itself when the charge returns.
        await _planService.ProcessAsync(transaction.ReferenceId, waitForLock: false, cancellationToken);
    }
}
