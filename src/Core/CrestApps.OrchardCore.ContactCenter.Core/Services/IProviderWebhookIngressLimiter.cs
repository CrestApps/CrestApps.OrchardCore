using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Enforces tenant-local concurrency and authenticated provider rate limits for webhook ingress.
/// </summary>
public interface IProviderWebhookIngressLimiter
{
    /// <summary>
    /// Attempts to acquire capacity before a webhook request body is buffered.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A lease that must be disposed after request processing.</returns>
    ValueTask<ProviderWebhookIngressLease> AcquireConcurrencyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to consume an authenticated delivery permit for a canonical provider.
    /// </summary>
    /// <param name="provider">The canonical provider technical name.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A lease that must be disposed after the rate-limit decision is consumed.</returns>
    ValueTask<ProviderWebhookIngressLease> AcquireRateAsync(string provider, CancellationToken cancellationToken = default);
}
