namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email;

/// <summary>
/// The constants of the email messaging channel.
/// </summary>
public static class EmailChannelConstants
{
    /// <summary>
    /// The stable technical name of the provider inbox handler that receives inbound email.
    /// </summary>
    public const string InboxHandlerName = "email-inbound";

    /// <summary>
    /// The purpose the address passwords and webhook secrets are protected under.
    /// </summary>
    public const string SecretProtectorPurpose = "CrestApps.Omnichannel.Messaging.Email.Secrets";

    /// <summary>
    /// The purpose unsubscribe links are signed under.
    /// </summary>
    public const string UnsubscribeProtectorPurpose = "CrestApps.Omnichannel.Messaging.Email.Unsubscribe";

    /// <summary>
    /// The route of the inbound email webhook, relative to the tenant; <c>{provider}</c> names the payload format.
    /// </summary>
    public const string InboundWebhookRoute = "api/omnichannel/email/inbound/{provider}";

    /// <summary>
    /// The route of the unsubscribe link, relative to the tenant.
    /// </summary>
    public const string UnsubscribeRoute = "omnichannel/email/unsubscribe/{token}";

    /// <summary>
    /// The header a webhook call can carry its secret key in, instead of the <c>key</c> query value.
    /// </summary>
    public const string WebhookKeyHeaderName = "X-Webhook-Key";

    /// <summary>
    /// The largest inbound email body kept, in characters. Longer bodies are cut, so one enormous email cannot bloat the
    /// thread or the durable inbox.
    /// </summary>
    public const int MaxBodyLength = 50_000;

    /// <summary>
    /// The largest inbound email accepted, in bytes, attachments included.
    /// </summary>
    public const long MaxInboundEmailBytes = 30L * 1024 * 1024;

    /// <summary>
    /// The technical names of the built-in sending transports.
    /// </summary>
    public static class Transports
    {
        /// <summary>
        /// Orchard Core's email service, with the tenant's default provider or a named one.
        /// </summary>
        public const string OrchardCore = "OrchardCore";

        /// <summary>
        /// The address's own SMTP server.
        /// </summary>
        public const string Smtp = "Smtp";
    }

    /// <summary>
    /// The provider names inbound email is recorded under in the durable inbox.
    /// </summary>
    public static class Providers
    {
        /// <summary>
        /// Mail read from the address's mailbox over IMAP.
        /// </summary>
        public const string Imap = "imap";

        /// <summary>
        /// SendGrid Inbound Parse.
        /// </summary>
        public const string SendGrid = "sendgrid";

        /// <summary>
        /// Mailgun Routes.
        /// </summary>
        public const string Mailgun = "mailgun";

        /// <summary>
        /// Postmark inbound processing.
        /// </summary>
        public const string Postmark = "postmark";

        /// <summary>
        /// Amazon SES receiving, delivered through Amazon SNS.
        /// </summary>
        public const string AmazonSes = "ses";

        /// <summary>
        /// A raw RFC 822 message posted by anything that can relay mail, such as Cloudflare Email Workers or a Lambda.
        /// </summary>
        public const string Mime = "mime";

        /// <summary>
        /// The channel's own JSON shape, for automation platforms such as Power Automate, Zapier or Make.
        /// </summary>
        public const string Json = "json";
    }
}
