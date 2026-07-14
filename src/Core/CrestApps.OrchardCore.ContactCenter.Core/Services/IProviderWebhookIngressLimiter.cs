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

    /// <summary>
    /// Determines whether a provider-signed UTC event timestamp is inside the configured freshness window.
    /// </summary>
    /// <param name="occurredUtc">The provider-signed event occurrence time, or <see langword="null"/> when omitted.</param>
    /// <returns><see langword="true"/> when the timestamp is present, UTC, and inside the accepted window.</returns>
    bool IsFresh(DateTime? occurredUtc);
}
