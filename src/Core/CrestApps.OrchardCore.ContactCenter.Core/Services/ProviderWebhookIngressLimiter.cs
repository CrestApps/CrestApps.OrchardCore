using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides tenant-local provider webhook concurrency and authenticated rate limiting.
/// </summary>
public sealed class ProviderWebhookIngressLimiter : IProviderWebhookIngressLimiter, IDisposable
{
    private readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _providerRateLimiters = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrencyLimiter _concurrencyLimiter;
    private readonly ProviderWebhookIngressOptions _options;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderWebhookIngressLimiter"/> class.
    /// </summary>
    /// <param name="options">The webhook ingress limit options.</param>
    /// <param name="clock">The clock used to evaluate signed delivery timestamps.</param>
    public ProviderWebhookIngressLimiter(
        IOptions<ProviderWebhookIngressOptions> options,
        IClock clock)
    {
        _options = options.Value;
        _clock = clock;

        if (!AreOptionsValid(_options))
        {
            throw new OptionsValidationException(
                nameof(ProviderWebhookIngressOptions),
                typeof(ProviderWebhookIngressOptions),
                ["Webhook ingress rate, concurrency, period, delivery-age, or future-skew values are outside their supported ranges."]);
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
    public bool IsFresh(DateTime? occurredUtc)
    {
        if (!occurredUtc.HasValue ||
            occurredUtc.Value == default ||
            occurredUtc.Value.Kind != DateTimeKind.Utc)
        {
            return false;
        }

        var now = _clock.UtcNow;

        return occurredUtc.Value >= now.AddSeconds(-_options.MaximumDeliveryAgeSeconds) &&
            occurredUtc.Value <= now.AddSeconds(_options.MaximumFutureSkewSeconds);
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

    private static bool AreOptionsValid(ProviderWebhookIngressOptions options)
    {
        return options.ConcurrencyPermitLimit is > 0 and <= 1024 &&
            options.RatePermitLimit is > 0 and <= 100_000 &&
            options.RatePeriodSeconds is > 0 and <= 3600 &&
            options.MaximumDeliveryAgeSeconds is > 0 and <= 86_400 &&
            options.MaximumFutureSkewSeconds is >= 0 and <= 3600;
    }
}
