using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Transactions.Models;

/// <summary>
/// Reads and sets the party that owes a <see cref="Transaction"/>.
/// </summary>
public static class TransactionPayerExtensions
{
    /// <summary>
    /// Gets the party that owes the transaction. For a transaction recorded before the payer was stored, it follows
    /// from what the transaction does record: the account named by its parent reference, as account-terms debts name
    /// one, and otherwise its owner, a user or a guest.
    /// </summary>
    /// <param name="transaction">The transaction.</param>
    /// <returns>The payer, or an empty reference when the transaction names nobody.</returns>
    public static PartyRef GetPayer(this Transaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (!string.IsNullOrEmpty(transaction.PayerKind) && !string.IsNullOrEmpty(transaction.PayerId))
        {
            return new PartyRef(transaction.PayerKind, transaction.PayerId);
        }

        if (string.Equals(transaction.ParentReferenceType, PartyKinds.Account, StringComparison.Ordinal) &&
            !string.IsNullOrEmpty(transaction.ParentReferenceId))
        {
            return new PartyRef(PartyKinds.Account, transaction.ParentReferenceId);
        }

        if (string.IsNullOrEmpty(transaction.OwnerId))
        {
            return default;
        }

        return new PartyRef(
            transaction.OwnerKind == CustomerOwnerKind.Guest ? PartyKinds.Guest : PartyKinds.User,
            transaction.OwnerId);
    }

    /// <summary>
    /// Sets the party that owes the transaction.
    /// </summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="payer">The payer. An empty reference clears it.</param>
    public static void SetPayer(this Transaction transaction, PartyRef payer)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        transaction.PayerKind = payer.IsEmpty ? null : payer.Kind;
        transaction.PayerId = payer.IsEmpty ? null : payer.Id;
    }
}
