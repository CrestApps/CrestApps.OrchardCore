using CrestApps.OrchardCore.Webhooks.Models;

namespace CrestApps.OrchardCore.Webhooks;

/// <summary>
/// Checks the signature a webhook sender put on a request, in constant time. An inbound endpoint authenticates a
/// callback with a validator before it accepts the delivery into the provider webhook inbox.
/// </summary>
public interface IWebhookSignatureValidator
{
    /// <summary>
    /// Gets the stable name of the signature scheme the validator checks, such as <c>hmac-sha256-hex</c>.
    /// </summary>
    string Scheme { get; }

    /// <summary>
    /// Returns whether the signature in the context was made with the secret over the signed content.
    /// </summary>
    /// <param name="context">The signature, the secret and what was signed.</param>
    /// <returns><see langword="true"/> when the signature is valid; otherwise <see langword="false"/>.</returns>
    bool IsValid(WebhookSignatureValidationContext context);
}
