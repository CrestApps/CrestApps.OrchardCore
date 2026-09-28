namespace CrestApps.OrchardCore.Dialpad.Services;

/// <summary>
/// Provides low-level access to the Dialpad Admin API for managing company-level call-event webhooks and
/// their subscriptions. The methods are safe to call from a deferred task because they take only immutable
/// values and never depend on the current request.
/// </summary>
public interface IDialpadWebhookApiService
{
    /// <summary>
    /// Creates a Dialpad call-event webhook and a matching call-event subscription. When the subscription
    /// cannot be created the newly created webhook is removed so no orphaned resource remains.
    /// </summary>
    /// <param name="baseUrl">The Dialpad REST API base address.</param>
    /// <param name="bearerToken">The bearer token used to authenticate the request.</param>
    /// <param name="webhookUrl">The public callback URL that Dialpad delivers events to.</param>
    /// <param name="signingSecret">The signing secret Dialpad uses to sign delivered payloads.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created webhook and subscription identifiers, or <see langword="null"/> when creation failed.</returns>
    Task<DialpadWebhookRegistrationResult> CreateAsync(
        string baseUrl,
        string bearerToken,
        string webhookUrl,
        string signingSecret,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a Dialpad call-event subscription and its webhook, treating resources that no longer exist
    /// as already deleted.
    /// </summary>
    /// <param name="baseUrl">The Dialpad REST API base address.</param>
    /// <param name="bearerToken">The bearer token used to authenticate the request.</param>
    /// <param name="webhookId">The Dialpad webhook identifier to delete, if any.</param>
    /// <param name="callEventSubscriptionId">The Dialpad call-event subscription identifier to delete, if any.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the resources were deleted or already absent; otherwise <see langword="false"/>.</returns>
    Task<bool> DeleteAsync(
        string baseUrl,
        string bearerToken,
        string webhookId,
        string callEventSubscriptionId,
        CancellationToken cancellationToken);
}
