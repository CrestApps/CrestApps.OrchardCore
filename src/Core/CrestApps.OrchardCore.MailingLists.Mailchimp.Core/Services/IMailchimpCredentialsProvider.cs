using CrestApps.OrchardCore.MailingLists.Mailchimp.Models;

namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Services;

/// <summary>
/// Reads the tenant's Mailchimp credentials: the API key from the Orchard Core secret the settings name, and the API root.
/// </summary>
public interface IMailchimpCredentialsProvider
{
    /// <summary>
    /// Returns the credentials, or <see langword="null"/> when no secret is named or the secret is missing or empty.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    Task<MailchimpCredentials> GetAsync(CancellationToken cancellationToken = default);
}
