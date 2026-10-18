using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Names the owner of a guest's transaction, who has no user account, from the contact captured at checkout.
/// </summary>
internal static class TransactionOwnerNames
{
    /// <summary>
    /// Returns the guest's name, or email, in the given format, such as "{0} (guest)", or <see langword="null"/>
    /// when the transaction holds neither.
    /// </summary>
    public static string GuestName(Transaction transaction, string format)
    {
        var name = !string.IsNullOrWhiteSpace(transaction?.GuestContactName)
            ? transaction.GuestContactName
            : transaction?.GuestContactEmail;

        return string.IsNullOrWhiteSpace(name) ? null : string.Format(System.Globalization.CultureInfo.CurrentCulture, format, name);
    }
}
