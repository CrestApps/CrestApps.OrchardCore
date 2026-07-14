using System.Threading.RateLimiting;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents an acquired or rejected provider webhook ingress permit.
/// </summary>
public sealed class ProviderWebhookIngressLease : IDisposable
{
    private readonly RateLimitLease _lease;

    internal ProviderWebhookIngressLease(RateLimitLease lease)
    {
        _lease = lease;

        if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            RetryAfter = retryAfter;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the webhook may proceed.
    /// </summary>
    public bool IsAcquired => _lease.IsAcquired;

    /// <summary>
    /// Gets the suggested delay before a rejected request retries, when supplied by the limiter.
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        _lease.Dispose();
    }
}
