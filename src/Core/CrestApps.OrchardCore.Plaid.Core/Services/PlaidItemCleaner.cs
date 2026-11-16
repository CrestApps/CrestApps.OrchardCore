using CrestApps.OrchardCore.Plaid.Models;
using CrestApps.OrchardCore.SensitiveData;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Plaid.Services;

/// <summary>
/// Removes Plaid items: at Plaid, so their access tokens stop working and are no longer billed, and from the site.
/// Removing never fails the caller; a failure is logged and the item is dropped from the site anyway, since nothing
/// can use it any more.
/// </summary>
public sealed class PlaidItemCleaner
{
    private readonly PlaidClient _client;
    private readonly IPlaidCredentialsProvider _credentialsProvider;
    private readonly IPlaidItemStore _store;
    private readonly ISensitiveValueProtector _protector;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaidItemCleaner"/> class.
    /// </summary>
    /// <param name="client">The Plaid client.</param>
    /// <param name="credentialsProvider">The credentials.</param>
    /// <param name="store">The kept items.</param>
    /// <param name="protector">The protector of access tokens.</param>
    /// <param name="logger">The logger.</param>
    public PlaidItemCleaner(
        PlaidClient client,
        IPlaidCredentialsProvider credentialsProvider,
        IPlaidItemStore store,
        ISensitiveValueProtector protector,
        ILogger<PlaidItemCleaner> logger)
    {
        _client = client;
        _credentialsProvider = credentialsProvider;
        _store = store;
        _protector = protector;
        _logger = logger;
    }

    /// <summary>
    /// Removes an item at Plaid by its access token.
    /// </summary>
    /// <param name="accessToken">The access token.</param>
    /// <param name="itemId">Plaid's id of the item, for the log.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when Plaid removed it.</returns>
    public async Task<bool> RemoveAtPlaidAsync(string accessToken, string itemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            return false;
        }

        var credentials = await _credentialsProvider.GetAsync(cancellationToken);

        if (credentials is null)
        {
            _logger.LogWarning("The Plaid item {ItemId} could not be removed at Plaid because Plaid is no longer set up. Remove it from the Plaid dashboard.", itemId);

            return false;
        }

        try
        {
            await _client.RemoveItemAsync(credentials, accessToken, cancellationToken);

            return true;
        }
        catch (PlaidApiException exception) when (exception.ErrorCode is "ITEM_NOT_FOUND" or "INVALID_ACCESS_TOKEN")
        {
            // Already gone.
            return true;
        }
        catch (Exception exception) when (exception is PlaidApiException or HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "The Plaid item {ItemId} could not be removed at Plaid. Remove it from the Plaid dashboard.", itemId);

            return false;
        }
    }

    /// <summary>
    /// Removes a kept item at Plaid and from the site.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    public async Task RemoveAsync(PlaidItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (_protector.TryUnprotect(PlaidConstants.AccessTokenPurpose, item.AccessToken, out var accessToken))
        {
            await RemoveAtPlaidAsync(accessToken, item.ItemId, cancellationToken);
        }
        else
        {
            _logger.LogWarning("The access token of the Plaid item {ItemId} can't be read with this site's keys, so it could not be removed at Plaid. Remove it from the Plaid dashboard.", item.ItemId);
        }

        await _store.DeleteAsync(item, cancellationToken);
    }

    /// <summary>
    /// Removes the items kept for a bank account, except one.
    /// </summary>
    /// <param name="bankAccountId">The bank account's id.</param>
    /// <param name="exceptItemId">Plaid's id of an item to keep, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    public async Task RemoveForBankAccountAsync(string bankAccountId, string exceptItemId = null, CancellationToken cancellationToken = default)
    {
        foreach (var item in await _store.ListByBankAccountAsync(bankAccountId, cancellationToken))
        {
            if (exceptItemId is null || !string.Equals(item.ItemId, exceptItemId, StringComparison.Ordinal))
            {
                await RemoveAsync(item, cancellationToken);
            }
        }
    }
}
