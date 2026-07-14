using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Validates and accepts provider voice webhooks: it resolves the provider adapter, verifies the
/// provider signature, enforces mandatory idempotency keys and freshness, and commits normalized
/// events to the durable provider webhook inbox before dispatch.
/// </summary>
public interface IProviderVoiceWebhookProcessor
{
    /// <summary>
    /// Authenticates, validates, and durably accepts a provider voice webhook request.
    /// </summary>
    /// <param name="request">The webhook request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The processing outcome.</returns>
    Task<ProviderVoiceWebhookOutcome> ProcessAsync(ProviderVoiceWebhookRequest request, CancellationToken cancellationToken = default);
}
