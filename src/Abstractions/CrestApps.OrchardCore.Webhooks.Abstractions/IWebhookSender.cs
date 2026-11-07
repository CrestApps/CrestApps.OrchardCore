using CrestApps.OrchardCore.Webhooks.Models;

namespace CrestApps.OrchardCore.Webhooks;

/// <summary>
/// Makes one signed POST of a webhook body to a URL, through the egress guard: a host on the site's own network is
/// refused, and redirects are not followed. The delivery outbox and the Send Signed Webhook workflow task both send
/// through it.
/// </summary>
public interface IWebhookSender
{
    /// <summary>
    /// Makes one attempt and reports what it came to. Never throws for a failed attempt.
    /// </summary>
    /// <param name="request">Where to send what, and the secret to sign it with.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>What the attempt came to.</returns>
    Task<WebhookSendResult> SendAsync(WebhookSendRequest request, CancellationToken cancellationToken = default);
}
