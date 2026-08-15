using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// A fixed-window payment attempt limiter backed by the distributed cache so the limit is enforced
/// consistently across every instance of a multi-instance deployment. Counting is serialized per
/// (scope, discriminator) key with a short distributed lock; payment attempts are infrequent, so the
/// added latency is negligible while the count stays accurate.
/// </summary>
public sealed class PaymentAttemptLimiter : IPaymentAttemptLimiter
{
    private readonly IDistributedCache _distributedCache;
    private readonly IDistributedLock _distributedLock;
    private readonly ShellSettings _shellSettings;
    private readonly IClock _clock;
    private readonly PaymentRateLimitOptions _options;

    public PaymentAttemptLimiter(
        IDistributedCache distributedCache,
        IDistributedLock distributedLock,
        ShellSettings shellSettings,
        IClock clock,
        IOptions<PaymentRateLimitOptions> options)
    {
        _distributedCache = distributedCache;
        _distributedLock = distributedLock;
        _shellSettings = shellSettings;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<bool> TryAcquireAsync(string scope, string discriminator)
    {
        ArgumentException.ThrowIfNullOrEmpty(scope);
        ArgumentException.ThrowIfNullOrEmpty(discriminator);

        // A non-positive limit disables throttling.
        if (_options.PermitLimit <= 0)
        {
            return true;
        }

        var key = $"{_shellSettings.Name}_PaymentRateLimit_{scope}_{discriminator}";
        var lockKey = $"{key}_LOCK";

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(
            lockKey,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10));

        if (!locked)
        {
            // If we cannot take the lock we fail closed and throttle, since being unable to account for
            // an attempt most likely coincides with contention/abuse.
            return false;
        }

        await using (locker)
        {
            var now = _clock.UtcNow;
            Window window = null;

            var data = await _distributedCache.GetAsync(key);

            if (data != null)
            {
                window = JsonSerializer.Deserialize<Window>(data);
            }

            if (window == null || now >= window.ExpiresAtUtc)
            {
                // Start a fresh window.
                window = new Window
                {
                    Count = 1,
                    ExpiresAtUtc = now.Add(_options.Window),
                };
            }
            else if (window.Count >= _options.PermitLimit)
            {
                // Over the limit for the current window; do not count this attempt.
                return false;
            }
            else
            {
                window.Count++;
            }

            var serialized = JsonSerializer.SerializeToUtf8Bytes(window);

            await _distributedCache.SetAsync(key, serialized, new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = window.ExpiresAtUtc,
            });

            return true;
        }
    }

    private sealed class Window
    {
        public int Count { get; set; }

        public DateTime ExpiresAtUtc { get; set; }
    }
}
