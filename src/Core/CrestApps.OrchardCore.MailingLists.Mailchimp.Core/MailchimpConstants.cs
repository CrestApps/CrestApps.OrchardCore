namespace CrestApps.OrchardCore.MailingLists.Mailchimp;

/// <summary>
/// The names the Mailchimp provider of mailing lists uses.
/// </summary>
public static class MailchimpConstants
{
    /// <summary>
    /// The provider's permanent technical name, stored as a mailing list's <c>ProviderName</c>.
    /// </summary>
    public const string ProviderName = "Mailchimp";

    /// <summary>
    /// The canonical provider name Mailchimp webhook deliveries are keyed by in the provider webhook inbox.
    /// </summary>
    public const string WebhookProviderName = "mailchimp";

    /// <summary>
    /// The technical name of the provider webhook inbox handler that applies Mailchimp webhooks.
    /// </summary>
    public const string WebhookHandlerName = "mailchimp-mailing-list-webhook";

    /// <summary>
    /// The name of the route that receives a list's Mailchimp webhooks.
    /// </summary>
    public const string WebhookRouteName = "CrestApps.MailingLists.Mailchimp.Webhook";

    /// <summary>
    /// The path of the route that receives a list's Mailchimp webhooks.
    /// </summary>
    public const string WebhookRoutePattern = "mailing-lists/mailchimp/webhooks/{listId}/{secret}";

    /// <summary>
    /// The header Mailchimp signs a webhook with when the webhook has a signing secret.
    /// </summary>
    public const string SignatureHeaderName = "X-Mailchimp-Signature";

    /// <summary>
    /// The named HTTP client of the Mailchimp Marketing API.
    /// </summary>
    public const string HttpClientName = "Mailchimp";

    /// <summary>
    /// The default name of the Orchard Core text secret that holds the Mailchimp API key.
    /// </summary>
    public const string DefaultSecretName = "Mailchimp.ApiKey";

    /// <summary>
    /// The default tag of the text merge field the welcome coupon code is written to.
    /// </summary>
    public const string DefaultCouponMergeTag = "COUPON";

    /// <summary>
    /// The data protection purpose of a list's webhook URL secret and signing secret. Never change it: stored secrets
    /// could no longer be read.
    /// </summary>
    public const string WebhookSecretPurpose = "CrestApps.OrchardCore.MailingLists.Mailchimp.Webhook";

    /// <summary>
    /// The feature ids of the Mailchimp provider.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// Keeps mailing lists in Mailchimp audiences.
        /// </summary>
        public const string Mailchimp = "CrestApps.OrchardCore.MailingLists.Mailchimp";
    }
}
