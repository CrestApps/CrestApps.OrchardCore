using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides tenant-local provider webhook concurrency and authenticated rate limiting.
/// </summary>
public sealed class ProviderWebhookIngressLimiter : IProviderWebhookIngressLimiter, IDisposable
{
    private readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _providerRateLimiters = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrencyLimiter _concurrencyLimiter;
    private readonly ProviderWebhookIngressOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderWebhookIngressLimiter"/> class.
    /// </summary>
    /// <param name="options">The webhook ingress limit options.</param>
    public ProviderWebhookIngressLimiter(IOptions<ProviderWebhookIngressOptions> options)
    {
        _options = options.Value;

        if (_options.ConcurrencyPermitLimit <= 0 ||
            _options.RatePermitLimit <= 0 ||
            _options.RatePeriodSeconds <= 0)
        {
            throw new OptionsValidationException(
                nameof(ProviderWebhookIngressOptions),
                typeof(ProviderWebhookIngressOptions),
                ["Webhook ingress concurrency, rate permit, and rate period values must be greater than zero."]);
        }

        _concurrencyLimiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = _options.ConcurrencyPermitLimit,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });
    }

    /// <inheritdoc/>
    public async ValueTask<ProviderWebhookIngressLease> AcquireConcurrencyAsync(CancellationToken cancellationToken = default)
    {
        var lease = await _concurrencyLimiter.AcquireAsync(1, cancellationToken);

        return new ProviderWebhookIngressLease(lease);
    }

    /// <inheritdoc/>
    public async ValueTask<ProviderWebhookIngressLease> AcquireRateAsync(string provider, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(provider);

        var limiter = _providerRateLimiters.GetOrAdd(provider, _ => CreateProviderRateLimiter());
        var lease = await limiter.AcquireAsync(1, cancellationToken);

        return new ProviderWebhookIngressLease(lease);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _concurrencyLimiter.Dispose();

        foreach (var limiter in _providerRateLimiters.Values)
        {
            limiter.Dispose();
        }

        _providerRateLimiters.Clear();
    }

    private TokenBucketRateLimiter CreateProviderRateLimiter()
    {
        return new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = _options.RatePermitLimit,
            TokensPerPeriod = _options.RatePermitLimit,
            ReplenishmentPeriod = TimeSpan.FromSeconds(_options.RatePeriodSeconds),
            AutoReplenishment = true,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });
    }
}
