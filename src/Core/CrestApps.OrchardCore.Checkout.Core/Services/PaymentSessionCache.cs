using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// A distributed, lock-coordinated key/value store used to relay short-lived checkout signals (such as a
/// webhook result) between the payment endpoints and the provider webhooks across a multi-node
/// deployment. It is a <em>notification/optimization</em> layer only: the authoritative record of what a
/// provider did is always the durable <see cref="CrestApps.OrchardCore.Checkout.Models.PaymentAttempt"/>
/// ledger, never this cache. A completion must re-verify against the provider, so an evicted or expired
/// cache entry can slow a checkout but can never lose money or strand a paid transaction.
/// </summary>
public sealed class PaymentSessionCache
{
    private const int MaxLockTries = 20;

    private readonly IDistributedCache _distributedCache;
    private readonly IDistributedLock _distributedLock;
    private readonly PaymentSessionCacheOptions _options;
    private readonly ShellSettings _shellSettings;

    public PaymentSessionCache(
        IDistributedCache distributedCache,
        IOptions<PaymentSessionCacheOptions> options,
        IDistributedLock distributedLock,
        ShellSettings shellSettings)
    {
        _distributedCache = distributedCache;
        _distributedLock = distributedLock;
        _options = options.Value;
        _shellSettings = shellSettings;
    }

    /// <summary>
    /// Gets the cached value for a session and purpose, or <c>null</c> when absent.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="sessionId">The checkout session id.</param>
    /// <param name="purpose">The logical purpose of the value.</param>
    public async Task<T> GetAsync<T>(string sessionId, string purpose)
        where T : class
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentException.ThrowIfNullOrEmpty(purpose);

        var key = GetKey(sessionId, purpose);

        T value = null;

        await LockCacheAsync(key, async () =>
        {
            var data = await _distributedCache.GetAsync(key);

            if (data != null)
            {
                value = JsonSerializer.Deserialize<T>(data);
            }
        });

        return value;
    }

    /// <summary>
    /// Sets the cached value for a session and purpose.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="sessionId">The checkout session id.</param>
    /// <param name="purpose">The logical purpose of the value.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="options">Optional cache entry options; defaults to the configured session lifetime.</param>
    public async Task SetAsync<T>(string sessionId, string purpose, T value, DistributedCacheEntryOptions options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentException.ThrowIfNullOrEmpty(purpose);
        ArgumentNullException.ThrowIfNull(value);

        var key = GetKey(sessionId, purpose);

        await LockCacheAsync(key, async () =>
        {
            var data = JsonSerializer.SerializeToUtf8Bytes(value);

            await _distributedCache.SetAsync(key, data, options ?? DefaultOptions());
        });
    }

    /// <summary>
    /// Atomically adds or updates the cached value for a session and purpose under the distributed lock.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="sessionId">The checkout session id.</param>
    /// <param name="purpose">The logical purpose of the value.</param>
    /// <param name="value">The value to store when none exists.</param>
    /// <param name="updater">The mutation applied to an existing value.</param>
    /// <param name="options">Optional cache entry options; defaults to the configured session lifetime.</param>
    public async Task<T> AddOrUpdateAsync<T>(
        string sessionId,
        string purpose,
        T value,
        Action<T> updater,
        DistributedCacheEntryOptions options = null)
        where T : class
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentException.ThrowIfNullOrEmpty(purpose);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(updater);

        var key = GetKey(sessionId, purpose);

        T finalValue = null;

        await LockCacheAsync(key, async () =>
        {
            var existingData = await _distributedCache.GetAsync(key);

            if (existingData != null)
            {
                finalValue = JsonSerializer.Deserialize<T>(existingData);

                updater(finalValue);
            }
            else
            {
                finalValue = value;
            }

            var data = JsonSerializer.SerializeToUtf8Bytes(finalValue);

            await _distributedCache.SetAsync(key, data, options ?? DefaultOptions());
        });

        return finalValue;
    }

    /// <summary>
    /// Removes the cached value for a session and purpose.
    /// </summary>
    /// <param name="sessionId">The checkout session id.</param>
    /// <param name="purpose">The logical purpose of the value.</param>
    public async Task RemoveAsync(string sessionId, string purpose)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentException.ThrowIfNullOrEmpty(purpose);

        await _distributedCache.RemoveAsync(GetKey(sessionId, purpose));
    }

    /// <summary>
    /// Removes every registered purpose for a session.
    /// </summary>
    /// <param name="sessionId">The checkout session id.</param>
    public async Task RemoveAsync(string sessionId)
    {
        foreach (var purpose in _options.Purposes)
        {
            await RemoveAsync(sessionId, purpose);
        }
    }

    private DistributedCacheEntryOptions DefaultOptions()
        => new() { AbsoluteExpirationRelativeToNow = _options.MaxLiveSession };

    private async Task LockCacheAsync(string key, Func<Task> callback)
    {
        var limit = TimeSpan.FromMilliseconds(2_000);

        var counter = 0;
        var lockKey = $"PAYMENT_{key}_LOCK";

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(lockKey, limit);

        while (!locked && counter++ < MaxLockTries)
        {
            await Task.Delay(500);
            (locker, locked) = await _distributedLock.TryAcquireLockAsync(lockKey, limit);
        }

        if (!locked)
        {
            throw new InvalidOperationException($"Exhausted {MaxLockTries} tries and could not create a lock.");
        }

        await using var acquiredLock = locker;

        await callback();
    }

    private string GetKey(string sessionId, string purpose)
        => $"{_shellSettings.Name}_{sessionId}__Checkout__{purpose}";
}
