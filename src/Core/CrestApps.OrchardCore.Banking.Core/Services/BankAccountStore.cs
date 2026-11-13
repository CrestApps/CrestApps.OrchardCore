using CrestApps.OrchardCore.Banking.Core.Indexes;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.Extensions.Localization;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Banking.Core.Services;

/// <summary>
/// Stores bank accounts in YesSql.
/// </summary>
public sealed class BankAccountStore : DocumentCatalog<BankAccount, BankAccountIndex>, IBankAccountStore
{
    private readonly IEnumerable<IBankAccountUsage> _usages;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BankAccountStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="usages">The modules whose records point to accounts.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BankAccountStore(ISession session, IEnumerable<IBankAccountUsage> usages, IStringLocalizer<BankAccountStore> stringLocalizer)
        : base(session)
    {
        _usages = usages;
        S = stringLocalizer;
        CollectionName = BankingConstants.CollectionName;
    }

    /// <inheritdoc />
    protected override async ValueTask DeletingAsync(BankAccount model)
    {
        // Records that point to the account would lose it: an account in use is closed, never deleted. Checked here, in
        // the store, so no path deletes it; the admin page asks first and says why.
        var reasons = await BankAccountUsages.GetReasonsAsync(_usages, model);

        if (reasons.Count > 0)
        {
            throw new InvalidOperationException(S["The bank account {0} is in use and can't be deleted: {1} Close it instead.", model.Name, string.Join(" ", reasons)]);
        }
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
