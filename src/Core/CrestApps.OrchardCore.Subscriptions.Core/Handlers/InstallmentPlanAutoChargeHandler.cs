using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

/// <summary>
/// Brings an installment plan up to date when the saved card charge service charged one of its payments and the card
/// declined: the plan records the retry and tells the customer, or, once no retry is left, hands the payment to the
/// customer to pay. The charge and its retries are the shared service's; the schedule and the plan settings' retry days
/// are the plan's, set on each payment's transaction.
/// </summary>
public sealed class InstallmentPlanAutoChargeHandler : TransactionEventHandlerBase
{
    private readonly IInstallmentPlanService _planService;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstallmentPlanAutoChargeHandler"/> class.
    /// </summary>
    /// <param name="planService">The installment plan service.</param>
    public InstallmentPlanAutoChargeHandler(IInstallmentPlanService planService)
    {
        _planService = planService;
    }

    /// <inheritdoc/>
    public override async Task AutoChargeDeclinedAsync(Transaction transaction, string reason, DateTime nextAttemptUtc, CancellationToken cancellationToken = default)
    {
        if (IsPlanPayment(transaction) && _planService is DefaultInstallmentPlanService plans)
        {
            await plans.RecordDeclinedChargeAsync(transaction, reason, nextAttemptUtc, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public override async Task AutoChargeAbandonedAsync(Transaction transaction, string reason, CancellationToken cancellationToken = default)
    {
        if (IsPlanPayment(transaction) && _planService is DefaultInstallmentPlanService plans)
        {
            await plans.RecordAbandonedChargeAsync(transaction, reason, cancellationToken);
        }
    }

    private static bool IsPlanPayment(Transaction transaction)
        => transaction is not null &&
            string.Equals(transaction.ReferenceType, SubscriptionConstants.InstallmentPlans.ReferenceType, StringComparison.Ordinal) &&
            !string.IsNullOrEmpty(transaction.ReferenceId);
}
