using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Customers.Models;

/// <summary>
/// Who a store credit account belongs to: a customer record, as every account always has, or another party, such as a
/// business account.
/// </summary>
/// <param name="CustomerId">The customer record's id, or <see langword="null"/> for another party.</param>
/// <param name="Party">The party, when it is not a customer record.</param>
public readonly record struct StoreCreditHolder(string CustomerId, PartyRef Party)
{
    /// <summary>
    /// Gets a value indicating whether the holder names nobody.
    /// </summary>
    public bool IsEmpty
        => string.IsNullOrEmpty(CustomerId) && Party.IsEmpty;

    /// <summary>
    /// Gets the key the lock that makes the holder's first account is taken on.
    /// </summary>
    public string LockKey
        => string.IsNullOrEmpty(CustomerId) ? $"{Party.Kind}:{Party.Id}" : CustomerId;

    /// <summary>
    /// Returns the holder of a customer record.
    /// </summary>
    /// <param name="customerId">The customer record's id.</param>
    public static StoreCreditHolder ForCustomer(string customerId)
        => new(customerId, default);

    /// <summary>
    /// Returns the holder of another party.
    /// </summary>
    /// <param name="party">The party.</param>
    public static StoreCreditHolder ForParty(PartyRef party)
        => new(null, party);
}
