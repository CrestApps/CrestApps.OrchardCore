using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;

namespace CrestApps.OrchardCore.Customers.Core.Services;

/// <summary>
/// The member of a commerce customer (party kind <see cref="PartyKinds.Customer"/>) is the user the customer record
/// belongs to; a guest's record has none. A user acts for their own customer record.
/// </summary>
public sealed class CustomerPartyMemberSource : IPartyMemberSource
{
    private readonly ICustomerStore _customerStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomerPartyMemberSource"/> class.
    /// </summary>
    /// <param name="customerStore">The customer store.</param>
    public CustomerPartyMemberSource(ICustomerStore customerStore)
    {
        _customerStore = customerStore;
    }

    /// <inheritdoc/>
    public string Kind => PartyKinds.Customer;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetMemberUserIdsAsync(PartyRef party, CancellationToken cancellationToken = default)
    {
        if (party.IsEmpty || await _customerStore.FindByIdAsync(party.Id, cancellationToken) is not { UserId.Length: > 0 } customer)
        {
            return [];
        }

        return [customer.UserId];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PartyRef>> GetPartiesAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || await _customerStore.FindByUserIdAsync(userId, cancellationToken) is not { } customer)
        {
            return [];
        }

        return [new PartyRef(PartyKinds.Customer, customer.ItemId)];
    }

    /// <inheritdoc/>
    /// <remarks>The one member has every access level.</remarks>
    public Task<IReadOnlyList<string>> GetMemberUserIdsAsync(PartyRef party, PartyAccess access, CancellationToken cancellationToken = default)
        => GetMemberUserIdsAsync(party, cancellationToken);

    /// <inheritdoc/>
    /// <remarks>The one member has every access level.</remarks>
    public Task<IReadOnlyList<PartyRef>> GetPartiesAsync(string userId, PartyAccess access, CancellationToken cancellationToken = default)
        => GetPartiesAsync(userId, cancellationToken);
}
