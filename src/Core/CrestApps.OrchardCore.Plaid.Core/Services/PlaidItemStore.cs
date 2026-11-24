using CrestApps.OrchardCore.Plaid.Indexes;
using CrestApps.OrchardCore.Plaid.Models;
using YesSql;

namespace CrestApps.OrchardCore.Plaid.Services;

/// <summary>
/// The YesSql <see cref="IPlaidItemStore"/>, in the <see cref="PlaidConstants.CollectionName"/> collection.
/// </summary>
public sealed class PlaidItemStore : IPlaidItemStore
{
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaidItemStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public PlaidItemStore(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc />
    public async Task<PlaidItem> FindByItemIdAsync(string itemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return null;
        }

        return await _session.Query<PlaidItem, PlaidItemIndex>(index => index.ItemId == itemId, collection: PlaidConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PlaidItem>> ListByItemIdAsync(string itemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return [];
        }

        // One per account of the item, and a bank holds a handful of accounts per sign-in.
        var items = await _session.Query<PlaidItem, PlaidItemIndex>(index => index.ItemId == itemId, collection: PlaidConstants.CollectionName)
            .Take(100)
            .ListAsync(cancellationToken);

        return items.ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PlaidItem>> ListByBankAccountAsync(string bankAccountId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(bankAccountId))
        {
            return [];
        }

        // An account has one item, or a few for a moment while a new link replaces an old one.
        var items = await _session.Query<PlaidItem, PlaidItemIndex>(index => index.BankAccountId == bankAccountId, collection: PlaidConstants.CollectionName)
            .Take(20)
            .ListAsync(cancellationToken);

        return items.ToList();
    }

    /// <inheritdoc />
    public async Task SaveAsync(PlaidItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        await _session.SaveAsync(item, collection: PlaidConstants.CollectionName, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteAsync(PlaidItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        _session.Delete(item, collection: PlaidConstants.CollectionName);

        return Task.CompletedTask;
    }
}
