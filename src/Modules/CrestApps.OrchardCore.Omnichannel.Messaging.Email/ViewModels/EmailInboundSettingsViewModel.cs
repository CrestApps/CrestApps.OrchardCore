using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.ViewModels;

/// <summary>
/// Edits the tenant's inbound email webhook settings.
/// </summary>
public class EmailInboundSettingsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether a new webhook key is generated on save, which invalidates the old one.
    /// </summary>
    public bool RegenerateWebhookKey { get; set; }

    /// <summary>
    /// Gets or sets a new Mailgun signing key; empty keeps the stored one.
    /// </summary>
    public string MailgunSigningKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the stored Mailgun signing key is removed.
    /// </summary>
    public bool ClearMailgunSigningKey { get; set; }

    /// <summary>
    /// Gets or sets the allowed Amazon SNS topic ARNs, one per line.
    /// </summary>
    public string AmazonSnsTopicArns { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a Mailgun signing key is stored.
    /// </summary>
    [BindNever]
    public bool HasMailgunSigningKey { get; set; }

    /// <summary>
    /// Gets or sets the webhook URLs, by provider.
    /// </summary>
    [BindNever]
    public IReadOnlyList<KeyValuePair<string, string>> WebhookUrls { get; set; } = [];

    /// <summary>
    /// Gets or sets the delivery events webhook URLs (bounces, complaints, blocks), by provider.
    /// </summary>
    [BindNever]
    public IReadOnlyList<KeyValuePair<string, string>> DeliveryEventUrls { get; set; } = [];
}
