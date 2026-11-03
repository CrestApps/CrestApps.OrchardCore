using CrestApps.OrchardCore.Accounts.B2B.Indexes;
using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using OrchardCore.ContentManagement;
using YesSql;

namespace CrestApps.OrchardCore.Accounts.B2B.Services;

/// <summary>
/// Reads and saves the accounts that buy as a business. An account is an ordinary content item; this store reads its
/// published version together with its <see cref="B2BAccountPart"/>.
/// </summary>
public interface IB2BAccountStore
{
    /// <summary>
    /// Returns the published account with the id, whether or not it buys as a business, or <see langword="null"/>.
    /// </summary>
    /// <param name="accountId">The account's content item id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<B2BAccount> FindByIdAsync(string accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the published account a user buys for, the account they are a member of, or <see langword="null"/>.
    /// </summary>
    /// <param name="userId">The buyer's user id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<B2BAccount> FindByBuyerAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the latest version of every account on a price list.
    /// </summary>
    /// <param name="priceListId">The price list's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IReadOnlyList<B2BAccount>> ListByPriceListAsync(string priceListId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a change to an account's <see cref="B2BAccountPart"/>, on the version that was read.
    /// </summary>
    /// <param name="account">The account.</param>
    Task SaveAsync(B2BAccount account);
}

/// <inheritdoc/>
public sealed class B2BAccountStore : IB2BAccountStore
{
    private readonly ISession _session;
    private readonly IContentManager _contentManager;
    private readonly IAccountMemberService _accountMemberService;

    public B2BAccountStore(ISession session, IContentManager contentManager, IAccountMemberService accountMemberService)
    {
        _session = session;
        _contentManager = contentManager;
        _accountMemberService = accountMemberService;
    }

    /// <inheritdoc/>
    public async Task<B2BAccount> FindByIdAsync(string accountId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(accountId))
        {
            return null;
        }

        var contentItem = await _contentManager.GetAsync(accountId, VersionOptions.Published);

        return contentItem?.Has<AccountPart>() == true ? new B2BAccount(contentItem) : null;
    }

    /// <inheritdoc/>
    public async Task<B2BAccount> FindByBuyerAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var contentItem = await _accountMemberService.FindAccountAsync(userId, cancellationToken);

        return contentItem?.Has<AccountPart>() == true ? new B2BAccount(contentItem) : null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<B2BAccount>> ListByPriceListAsync(string priceListId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(priceListId))
        {
            return [];
        }

        var contentItems = await _session
            .Query<ContentItem, B2BAccountIndex>(index => index.PriceListId == priceListId && index.Latest)
            .ListAsync(cancellationToken);

        return [.. contentItems.Select(contentItem => new B2BAccount(contentItem))];
    }

    /// <inheritdoc/>
    public async Task SaveAsync(B2BAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        account.ContentItem.Apply(account.Part);

        await _contentManager.UpdateAsync(account.ContentItem);
    }
}
