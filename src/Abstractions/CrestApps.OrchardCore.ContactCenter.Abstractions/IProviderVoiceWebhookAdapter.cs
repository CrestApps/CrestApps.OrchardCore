using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Validates and normalizes provider-specific voice webhooks into provider-neutral
/// <see cref="ProviderVoiceEvent"/> instances. Each telephony/PBX provider that delivers signed
/// webhooks contributes one adapter so the Contact Center accepts provider callbacks safely.
/// </summary>
public interface IProviderVoiceWebhookAdapter
{
    /// <summary>
    /// Gets the technical name of the provider this adapter handles. Matched against the webhook route.
    /// </summary>
    string TechnicalName { get; }

    /// <summary>
    /// Validates the provider signature carried by the webhook request.
    /// </summary>
    /// <param name="request">The webhook request to validate.</param>
    /// <returns><see langword="true"/> when the signature is valid; otherwise, <see langword="false"/>.</returns>
    bool ValidateSignature(ProviderVoiceWebhookRequest request);

    /// <summary>
    /// Parses the webhook request into one or more normalized provider voice events.
    /// </summary>
    /// <param name="request">The validated webhook request.</param>
    /// <returns>The normalized provider voice events; may be empty when the payload carries no call state.</returns>
    IReadOnlyList<ProviderVoiceEvent> Parse(ProviderVoiceWebhookRequest request);
}
