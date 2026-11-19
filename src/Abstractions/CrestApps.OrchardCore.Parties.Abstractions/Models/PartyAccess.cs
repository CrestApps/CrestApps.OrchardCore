namespace CrestApps.OrchardCore.Parties.Models;

/// <summary>
/// What a member may do for a party. Every level includes the ones before it.
/// </summary>
public enum PartyAccess
{
    /// <summary>
    /// Acts for the party in what they do themselves, such as an order they place in the party's name. A plain member of a
    /// business account (a buyer) has this level.
    /// </summary>
    Member = 0,

    /// <summary>
    /// Also sees and settles the party's money: everything it owes (invoices, balances, statements), its receipts, paying
    /// for it and the payment methods it keeps. The user of their own user party and of their customer record have it; of
    /// an account, only its admins do.
    /// </summary>
    Billing = 1,
}
