using CrestApps.OrchardCore.Banking.Core.Indexes;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Banking.Core.Services;

/// <summary>
/// Stores bank accounts in YesSql.
/// </summary>
public sealed class BankAccountStore : DocumentCatalog<BankAccount, BankAccountIndex>, IBankAccountStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BankAccountStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public BankAccountStore(ISession session)
        : base(session)
    {
        CollectionName = BankingConstants.CollectionName;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BankAccount>> ListByOwnersAsync(IEnumerable<PartyRef> owners, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owners);

        var accounts = new List<BankAccount>();

        // Owners of one kind are read together; the business's own accounts have no owner kind.
        foreach (var group in owners.Distinct().GroupBy(owner => owner.IsEmpty ? string.Empty : owner.Kind, StringComparer.Ordinal))
        {
            var kind = group.Key;

            if (kind.Length == 0)
            {
                accounts.AddRange(await Session.Query<BankAccount, BankAccountIndex>(index => index.OwnerKind == null || index.OwnerKind == string.Empty, collection: BankingConstants.CollectionName)
                    .ListAsync(cancellationToken));

                continue;
            }

            var ids = group.Select(owner => owner.Id).ToArray();

            accounts.AddRange(await Session.Query<BankAccount, BankAccountIndex>(index => index.OwnerKind == kind && index.OwnerId.IsIn(ids), collection: BankingConstants.CollectionName)
                .ListAsync(cancellationToken));
        }

        return accounts
            .DistinctBy(account => account.ItemId, StringComparer.Ordinal)
            .OrderBy(account => account.CreatedUtc)
            .ToList();
    }
}
