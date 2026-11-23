using CrestApps.OrchardCore.Plaid.Models;

namespace CrestApps.OrchardCore.Plaid.Services;

/// <summary>
/// The kept Plaid items.
/// </summary>
public interface IPlaidItemStore
{
    /// <summary>
    /// Finds the first item kept with Plaid's id of an item. An item linked to several bank accounts is kept once per
    /// bank account; use <see cref="ListByItemIdAsync"/> to get them all.
    /// </summary>
    /// <param name="itemId">Plaid's id of the item.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The item, or <see langword="null"/>.</returns>
    Task<PlaidItem> FindByItemIdAsync(string itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the items kept with Plaid's id of an item: one per bank account linked to an account of the item.
    /// </summary>
    /// <param name="itemId">Plaid's id of the item.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The items.</returns>
    Task<IReadOnlyList<PlaidItem>> ListByItemIdAsync(string itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the items kept for a bank account.
    /// </summary>
    /// <param name="bankAccountId">The bank account's id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The items.</returns>
    Task<IReadOnlyList<PlaidItem>> ListByBankAccountAsync(string bankAccountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves an item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    Task SaveAsync(PlaidItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    Task DeleteAsync(PlaidItem item, CancellationToken cancellationToken = default);
}
