using CrestApps.OrchardCore.Customers.Models;

namespace CrestApps.OrchardCore.Transactions.Models;

/// <summary>
/// The buyer a transaction belongs to, as the customer services address them.
/// </summary>
public static class TransactionOwnerExtensions
{
    /// <summary>
    /// Returns the transaction's owner: a guest or a user, by the owner id.
    /// </summary>
    /// <param name="transaction">The transaction.</param>
    public static CustomerOwner GetOwner(this Transaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        return transaction.OwnerKind == CustomerOwnerKind.Guest
            ? CustomerOwner.ForGuest(transaction.OwnerId)
            : CustomerOwner.ForUser(transaction.OwnerId);
    }

    /// <summary>
    /// Returns the contact captured for a guest owner when they bought.
    /// </summary>
    /// <param name="transaction">The transaction.</param>
    public static CustomerContact GetGuestContact(this Transaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        return new CustomerContact
        {
            DisplayName = transaction.GuestContactName,
            Email = transaction.GuestContactEmail,
        };
    }
}
