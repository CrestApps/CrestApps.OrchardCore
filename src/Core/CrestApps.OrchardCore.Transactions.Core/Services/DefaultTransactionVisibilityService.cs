using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;

namespace CrestApps.OrchardCore.Transactions.Core.Services;

/// <summary>
/// The default <see cref="ITransactionVisibilityService"/>: a user sees what they own (as a signed-in owner, never a guest
/// obligation that happens to share an id) and what is owed by a party they have <see cref="PartyAccess.Billing"/> for, as
/// <see cref="IPartyMembershipService"/> answers: their customer record, or an account they are an admin of. A plain member of
/// an account (a buyer) sees only the transactions they own, also those billed to the account.
/// </summary>
public sealed class DefaultTransactionVisibilityService : ITransactionVisibilityService
{
    private readonly IPartyMembershipService _membership;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTransactionVisibilityService"/> class.
    /// </summary>
    /// <param name="membership">The service that knows which parties a user acts for.</param>
    public DefaultTransactionVisibilityService(IPartyMembershipService membership)
    {
        _membership = membership;
    }

    /// <inheritdoc/>
    public async Task<bool> CanViewAsync(Transaction transaction, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (string.IsNullOrEmpty(userId))
        {
            return false;
        }

        if (transaction.OwnerKind == CustomerOwnerKind.Authenticated &&
            string.Equals(transaction.OwnerId, userId, StringComparison.Ordinal))
        {
            return true;
        }

        var payer = transaction.GetPayer();

        return !payer.IsEmpty &&
            !string.Equals(payer.Kind, PartyKinds.Guest, StringComparison.Ordinal) &&
            await _membership.IsMemberAsync(payer, userId, PartyAccess.Billing, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<TransactionQuery> CreateQueryAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var parties = await _membership.GetPartiesAsync(userId, PartyAccess.Billing, cancellationToken);

        return new TransactionQuery
        {
            OwnerId = userId,
            OwnerKind = CustomerOwnerKind.Authenticated,
            Payers = [.. (parties ?? []).Where(party => !party.IsEmpty)],
        };
    }
}
