using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Transactions.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Refuses to take payment for a settlement checkout whose transactions no longer owe what it billed when it started:
/// the balance is fixed then, so a second tab of the same pay link, opened before the first was paid, would otherwise
/// be captured too and the surplus only logged.
/// </summary>
public sealed class TransactionSettlementPaymentGate : ICheckoutPaymentGate
{
    private readonly ITransactionManager _transactions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionSettlementPaymentGate"/> class.
    /// </summary>
    /// <param name="transactions">The transaction manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionSettlementPaymentGate(ITransactionManager transactions, IStringLocalizer<TransactionSettlementPaymentGate> stringLocalizer)
    {
        _transactions = transactions;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task BeginningAsync(CheckoutPaymentBeginningContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Flow.Session is not CheckoutSession session ||
            !string.Equals(session.ReferenceType, TransactionsConstants.ReferenceTypes.Transaction, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var billed = session.Steps
            .Where(step => string.Equals(step.Key, TransactionsConstants.SettlementStepKey, StringComparison.Ordinal))
            .SelectMany(step => step.BillingItems ?? [])
            .Where(item => !string.IsNullOrEmpty(item.ItemId))
            .ToList();

        // A payer of one transaction may have asked to pay part of it; several transactions are always paid in full.
        var requested = billed.Count == 1 && session.TryGet<CheckoutRequestedAmount>(out var requestedAmount)
            ? requestedAmount.Amount
            : (decimal?)null;

        foreach (var item in billed)
        {
            var transaction = await _transactions.FindByIdAsync(item.ItemId, cancellationToken);

            // The amount is worked out again the way the checkout did when it started: the whole balance, or the part the
            // payer asked for when the transaction takes part payments. Anything else means it was paid or changed since.
            if (transaction is null ||
                transaction.Status is TransactionStatus.Paid or TransactionStatus.Canceled ||
                !TransactionStateMachine.TryGetPaymentAmount(transaction, requested, out var owed) ||
                owed != item.Amount)
            {
                context.Refuse(S["What's owed changed since this payment page was opened: it was paid or changed. Open the pay link again to see what's owed now."]);

                return;
            }
        }
    }
}
