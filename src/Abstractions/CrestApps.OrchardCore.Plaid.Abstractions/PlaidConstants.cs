namespace CrestApps.OrchardCore.Plaid;

/// <summary>
/// The names the Plaid module uses.
/// </summary>
public static class PlaidConstants
{
    /// <summary>
    /// The provider's name, stored as a linked bank account's verification method.
    /// </summary>
    public const string ProviderName = "Plaid";

    /// <summary>
    /// The YesSql collection of linked items.
    /// </summary>
    public const string CollectionName = "Plaid";

    /// <summary>
    /// The named HTTP client.
    /// </summary>
    public const string HttpClientName = "Plaid";

    /// <summary>
    /// The site settings group.
    /// </summary>
    public const string SettingsGroupId = "plaid";

    /// <summary>
    /// The default name of the Orchard secret that holds the Plaid secret.
    /// </summary>
    public const string DefaultSecretName = "Plaid.Secret";

    /// <summary>
    /// The data protection purpose of stored access tokens. Never change it: stored tokens could no longer be read.
    /// </summary>
    public const string AccessTokenPurpose = "CrestApps.OrchardCore.Plaid.AccessToken";

    /// <summary>
    /// The shape that runs Plaid Link in the browser.
    /// </summary>
    public const string LinkShapeType = "BankAccountLink_Plaid";

    /// <summary>
    /// The webhook inbox handler's name.
    /// </summary>
    public const string WebhookHandlerName = "plaid-webhook";

    /// <summary>
    /// The webhook route's name.
    /// </summary>
    public const string WebhookRouteName = "CrestApps.Plaid.Webhook";

    /// <summary>
    /// The webhook endpoint's path, under the tenant's address.
    /// </summary>
    public const string WebhookPath = "plaid/webhook";

    /// <summary>
    /// The features.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// Plaid bank account linking.
        /// </summary>
        public const string Plaid = "CrestApps.OrchardCore.Plaid";
    }
}
