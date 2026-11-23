using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Services;

/// <summary>
/// The meter of the Mailchimp provider of mailing lists, <c>CrestApps.OrchardCore.MailingLists.Mailchimp</c>: the
/// webhooks received, by what became of them. Pushes are counted by the mailing lists meter. Addresses, list ids and
/// audience ids never go on a metric.
/// </summary>
public static class MailchimpTelemetry
{
    /// <summary>
    /// The webhook was stored in the provider webhook inbox.
    /// </summary>
    public const string WebhookAccepted = "accepted";

    /// <summary>
    /// The webhook was stored before; nothing was stored again.
    /// </summary>
    public const string WebhookDuplicate = "duplicate";

    /// <summary>
    /// The webhook is about something the site does not act on (a campaign); nothing was stored.
    /// </summary>
    public const string WebhookIgnored = "ignored";

    /// <summary>
    /// The list is unknown, not kept in Mailchimp, has no webhook, or the URL secret is wrong.
    /// </summary>
    public const string WebhookRejectedUrl = "rejected_url";

    /// <summary>
    /// The signature is missing, malformed, wrong or stale.
    /// </summary>
    public const string WebhookRejectedSignature = "rejected_signature";

    /// <summary>
    /// The body is too large or could not be read.
    /// </summary>
    public const string WebhookRejectedBody = "rejected_body";

    /// <summary>
    /// The webhook is about another audience than the list's.
    /// </summary>
    public const string WebhookRejectedAudience = "rejected_audience";

    /// <summary>
    /// The ingress limiter or the inbox was busy; Mailchimp is asked to send it again.
    /// </summary>
    public const string WebhookBusy = "busy";

    /// <summary>
    /// The webhook could not be stored; Mailchimp is asked to send it again.
    /// </summary>
    public const string WebhookError = "error";

    /// <summary>
    /// The tag that records the webhook's event type (a bounded set: <c>subscribe</c>, <c>unsubscribe</c>,
    /// <c>profile</c>, <c>upemail</c>, <c>cleaned</c>, <c>campaign</c>, <c>other</c>).
    /// </summary>
    public const string TypeTag = "type";

    /// <summary>
    /// The meter of the Mailchimp provider.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("MailingLists.Mailchimp");

    /// <summary>
    /// Gets the webhooks received, by result and event type.
    /// </summary>
    public static Counter<long> Webhooks { get; } = Meter.CreateCounter<long>(
        "crestapps.mailing_lists.mailchimp.webhooks",
        "{webhook}",
        "The Mailchimp webhooks received, by what became of them and their event type.");

    /// <summary>
    /// Counts one received webhook.
    /// </summary>
    /// <param name="result">What became of it (one of the <c>Webhook...</c> constants).</param>
    /// <param name="type">Its event type, or <see langword="null"/> when it was not read.</param>
    public static void RecordWebhook(string result, string type)
    {
        if (!Webhooks.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
            { TypeTag, NormalizeType(type) },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        Webhooks.Add(1, tags);
    }

    private static string NormalizeType(string type)
        => type switch
        {
            null => CrestAppsOrchardCoreTelemetry.Unknown,
            MailchimpWebhookParser.Subscribe
                or MailchimpWebhookParser.Unsubscribe
                or MailchimpWebhookParser.Profile
                or MailchimpWebhookParser.AddressChange
                or MailchimpWebhookParser.Cleaned
                or MailchimpWebhookParser.Campaign => type,
            _ => "other",
        };
}
