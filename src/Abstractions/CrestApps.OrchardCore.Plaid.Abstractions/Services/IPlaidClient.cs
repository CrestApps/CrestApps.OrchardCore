using CrestApps.OrchardCore.Plaid.Models;

namespace CrestApps.OrchardCore.Plaid.Services;

/// <summary>
/// The Plaid calls other modules make with the tenant's Plaid setup: the credentials come from
/// <see cref="IPlaidCredentialsProvider"/>, so every module of a tenant shares one Plaid client id and secret, and the
/// webhooks of the items they link reach the Plaid webhook endpoint and every <see cref="IPlaidWebhookHandler"/>. Every
/// call throws <see cref="InvalidOperationException"/> when Plaid is not set up (check <see cref="IsConfiguredAsync"/>
/// first) and <see cref="PlaidApiException"/> when Plaid answers with an error.
/// </summary>
public interface IPlaidClient
{
    /// <summary>
    /// Returns whether the tenant has Plaid credentials.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when Plaid can be called.</returns>
    Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes a link token that opens Plaid Link, for a new item or, with <see cref="PlaidLinkTokenRequest.AccessToken"/>,
    /// in update mode. Start from <see cref="IPlaidLinkSite.CreateLinkTokenRequestAsync"/> so the person's id is opaque
    /// and the webhook URL is this tenant's Plaid webhook endpoint.
    /// </summary>
    /// <param name="request">What the link token is made for.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The link token.</returns>
    Task<PlaidLinkTokenResponse> CreateLinkTokenAsync(PlaidLinkTokenRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges the public token Plaid Link returned for the item's access token. Never retried: a public token works
    /// once.
    /// </summary>
    /// <param name="publicToken">The public token.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The access token and the item's id.</returns>
    Task<PlaidPublicTokenExchangeResponse> ExchangePublicTokenAsync(string publicToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every account of an item, with its balances, so one item can be mapped to several accounts.
    /// </summary>
    /// <param name="accessToken">The item's access token.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The accounts and the item.</returns>
    Task<PlaidAccountsResponse> GetAccountsAsync(string accessToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one page of <c>/transactions/sync</c> for an item, or for one of its accounts.
    /// </summary>
    /// <param name="request">The access token, cursor and page size.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The page.</returns>
    Task<PlaidTransactionsSyncResponse> SyncTransactionsAsync(PlaidTransactionsSyncRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a bank's name and id.
    /// </summary>
    /// <param name="institutionId">Plaid's id of the bank.</param>
    /// <param name="countryCodes">The two-letter country codes to search, or <see langword="null"/> for the United States.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The bank.</returns>
    Task<PlaidInstitution> GetInstitutionAsync(string institutionId, IEnumerable<string> countryCodes = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the webhook URL Plaid sends an item's webhooks to (<c>/item/webhook/update</c>), such as when the site moves
    /// to another address or an app replaces its own webhook endpoint with the Plaid webhook endpoint. Plaid sends a
    /// <c>WEBHOOK_UPDATE_ACKNOWLEDGED</c> webhook to the new URL. Get this tenant's URL from
    /// <see cref="IPlaidLinkSite.GetWebhookUrlAsync"/>.
    /// </summary>
    /// <param name="accessToken">The item's access token.</param>
    /// <param name="webhookUrl">The new webhook URL, an absolute HTTPS address Plaid can reach.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The item, with the webhook URL Plaid now holds.</returns>
    Task<PlaidItemInfo> UpdateItemWebhookAsync(string accessToken, string webhookUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an item, so its access token stops working and Plaid stops billing for it.
    /// </summary>
    /// <param name="accessToken">The item's access token.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    Task RemoveItemAsync(string accessToken, CancellationToken cancellationToken = default);
}
