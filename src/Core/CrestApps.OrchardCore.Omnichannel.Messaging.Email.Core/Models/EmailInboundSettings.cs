namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;

/// <summary>
/// The tenant's inbound email webhook settings: the secret every webhook call must carry and the provider signing
/// keys some providers sign their calls with.
/// </summary>
public sealed class EmailInboundSettings
{
    /// <summary>
    /// Gets or sets the secret key every inbound email webhook call must carry (in its <c>key</c> query value or the
    /// <c>X-Webhook-Key</c> header), protected with the tenant's data protection keys.
    /// </summary>
    public string WebhookKey { get; set; }

    /// <summary>
    /// Gets or sets the Mailgun HTTP webhook signing key, protected with the tenant's data protection keys. When it is
    /// set, a Mailgun call must also carry a valid signature.
    /// </summary>
    public string MailgunSigningKey { get; set; }

    /// <summary>
    /// Gets or sets the Amazon SNS topics allowed to deliver Amazon SES mail, by ARN. When any are listed, a message
    /// from another topic is refused, so an SNS subscription to a topic the tenant does not own is never confirmed. Empty
    /// accepts any topic signed by Amazon SNS that knows the webhook key.
    /// </summary>
    public IList<string> AmazonSnsTopicArns { get; set; } = [];
}
