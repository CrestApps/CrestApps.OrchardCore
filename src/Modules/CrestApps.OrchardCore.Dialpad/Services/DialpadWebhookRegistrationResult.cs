namespace CrestApps.OrchardCore.Dialpad.Services;

/// <summary>
/// Represents the result of creating a Dialpad call-event webhook and its matching subscription.
/// </summary>
public sealed class DialpadWebhookRegistrationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DialpadWebhookRegistrationResult"/> class.
    /// </summary>
    /// <param name="webhookId">The created Dialpad webhook identifier.</param>
    /// <param name="callEventSubscriptionId">The created Dialpad call-event subscription identifier.</param>
    public DialpadWebhookRegistrationResult(string webhookId, string callEventSubscriptionId)
    {
        WebhookId = webhookId;
        CallEventSubscriptionId = callEventSubscriptionId;
    }

    /// <summary>
    /// Gets the created Dialpad webhook identifier.
    /// </summary>
    public string WebhookId { get; }

    /// <summary>
    /// Gets the created Dialpad call-event subscription identifier.
    /// </summary>
    public string CallEventSubscriptionId { get; }
}
