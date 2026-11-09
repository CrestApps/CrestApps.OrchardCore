using CrestApps.OrchardCore.Webhooks.Models;

namespace CrestApps.OrchardCore.Webhooks.Core.Signatures;

/// <summary>
/// Validates a sender that authenticates with the shared key itself in a header, such as an Azure Event Grid
/// subscription's <c>aeg-sas-key</c>.
/// </summary>
public sealed class SharedKeyWebhookSignatureValidator : IWebhookSignatureValidator
{
    /// <summary>
    /// The scheme name.
    /// </summary>
    public const string SchemeName = "shared-key";

    /// <inheritdoc/>
    public string Scheme => SchemeName;

    /// <inheritdoc/>
    public bool IsValid(WebhookSignatureValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return !string.IsNullOrEmpty(context.Secret) &&
            !string.IsNullOrEmpty(context.Signature) &&
            WebhookSignatures.FixedTimeEquals(context.Secret, context.Signature);
    }
}
