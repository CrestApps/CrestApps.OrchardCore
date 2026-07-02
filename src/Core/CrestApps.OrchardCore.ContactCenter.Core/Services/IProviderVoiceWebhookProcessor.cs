using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Validates and ingests provider voice webhooks: it resolves the provider adapter, verifies the
/// provider signature, enforces mandatory idempotency keys, and forwards normalized events to the
/// provider voice event pipeline.
/// </summary>
public interface IProviderVoiceWebhookProcessor
{
    /// <summary>
    /// Processes a provider voice webhook request end to end.
    /// </summary>
    /// <param name="request">The webhook request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The processing outcome.</returns>
    Task<ProviderVoiceWebhookOutcome> ProcessAsync(ProviderVoiceWebhookRequest request, CancellationToken cancellationToken = default);
}
