namespace CrestApps.OrchardCore.Plaid.Models;

/// <summary>
/// A verified Plaid webhook, handed to every <see cref="Services.IPlaidWebhookHandler"/>.
/// </summary>
public sealed class PlaidWebhookContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlaidWebhookContext"/> class.
    /// </summary>
    /// <param name="webhook">The webhook.</param>
    /// <param name="payload">The exact JSON body Plaid sent, for fields <see cref="PlaidWebhook"/> does not read.</param>
    public PlaidWebhookContext(PlaidWebhook webhook, string payload)
    {
        ArgumentNullException.ThrowIfNull(webhook);

        Webhook = webhook;
        Payload = payload;
    }

    /// <summary>
    /// Gets the webhook.
    /// </summary>
    public PlaidWebhook Webhook { get; }

    /// <summary>
    /// Gets the exact JSON body Plaid sent.
    /// </summary>
    public string Payload { get; }

    /// <summary>
    /// Returns whether the webhook has a type and a code, such as <c>TRANSACTIONS</c> and <c>SYNC_UPDATES_AVAILABLE</c>.
    /// </summary>
    /// <param name="type">The webhook type.</param>
    /// <param name="code">The webhook code.</param>
    /// <returns><see langword="true"/> when both match.</returns>
    public bool Is(string type, string code)
        => string.Equals(Webhook.WebhookType, type, StringComparison.Ordinal)
            && string.Equals(Webhook.WebhookCode, code, StringComparison.Ordinal);
}
