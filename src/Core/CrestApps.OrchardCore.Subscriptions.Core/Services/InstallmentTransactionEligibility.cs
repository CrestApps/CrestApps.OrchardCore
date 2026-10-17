using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Decides whether an outstanding transaction may be turned into an installment plan by the customer at checkout.
/// </summary>
/// <remarks>
/// The rule is kept in one place because both the checkout step that offers installments and the service that
/// creates the plan apply it: offering a choice the service then refuses would leave the customer at a dead end.
/// </remarks>
public static class InstallmentTransactionEligibility
{
    /// <summary>
    /// Returns <see langword="true"/> when the transaction can be paid in installments: it is owed in full, by a
    /// customer with an account (a plan, and the card it keeps, belong to a person), and it is not already a payment
    /// of a plan, a recurring charge or a balance collected automatically.
    /// </summary>
    /// <param name="transaction">The transaction, or <see langword="null"/>.</param>
    public static bool CanSplit(Transaction transaction)
        => transaction is not null &&
            transaction.Status == TransactionStatus.Outstanding &&
            transaction.AmountPaid <= 0m &&
            transaction.TotalAmount > 0m &&
            !string.IsNullOrEmpty(transaction.Currency) &&
            transaction.OwnerKind == CustomerOwnerKind.Authenticated &&
            !string.IsNullOrEmpty(transaction.OwnerId) &&
            transaction.Recurrence is null &&
            transaction.AutoCollection is null &&
            !string.Equals(transaction.ReferenceType, SubscriptionConstants.InstallmentPlans.ReferenceType, StringComparison.Ordinal) &&
            !string.Equals(transaction.Source, SubscriptionConstants.InstallmentPlans.TransactionSource, StringComparison.OrdinalIgnoreCase);
}
