using CrestApps.OrchardCore.Plaid.Models;

namespace CrestApps.OrchardCore.Plaid.Services;

/// <summary>
/// What a link token needs from the site: an opaque id of the person, the site's name, the language and the webhook
/// URL of this tenant.
/// </summary>
public interface IPlaidLinkSite
{
    /// <summary>
    /// Builds the link token request for a person.
    /// </summary>
    /// <param name="userId">The signed-in user's id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The request.</returns>
    Task<PlaidLinkTokenRequest> CreateLinkTokenRequestAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns this tenant's Plaid webhook URL: from the current request, or, outside a request (a background task, a
    /// migration), from the site's base URL. Use it with <see cref="IPlaidClient.UpdateItemWebhookAsync"/>.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The URL, or <see langword="null"/> outside a request when the site has no base URL.</returns>
    Task<string> GetWebhookUrlAsync(CancellationToken cancellationToken = default);
}
