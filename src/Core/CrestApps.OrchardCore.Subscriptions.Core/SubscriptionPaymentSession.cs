using System.Text.Json;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Subscriptions.Core;

/// <summary>
/// Stores short-lived subscription payment data in the distributed cache by session and purpose.
/// </summary>
public sealed class SubscriptionPaymentSession
{
    private const int MaxLockTries = 20;

    private readonly IDistributedCache _distributedCache;
    private readonly IDistributedLock _distributedLock;
    private readonly SubscriptionPaymentSessionOptions _options;
    private readonly ShellSettings _shellSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionPaymentSession"/> class.
    /// </summary>
    /// <param name="distributedCache">The distributed cache used to store serialized payment session data.</param>
    /// <param name="options">The payment session options that control expiration and known purposes.</param>
    /// <param name="distributedLock">The distributed lock service used to serialize cache updates.</param>
    /// <param name="shellSettings">The shell settings used to scope cache keys to the tenant.</param>
    public SubscriptionPaymentSession(
        IDistributedCache distributedCache,
        IOptions<SubscriptionPaymentSessionOptions> options,
        IDistributedLock distributedLock,
        ShellSettings shellSettings)
    {
        _distributedCache = distributedCache;
        _distributedLock = distributedLock;
        _options = options.Value;
        _shellSettings = shellSettings;
    }

    /// <summary>
    /// Gets cached subscription payment data for the specified session and purpose.
    /// </summary>
    /// <typeparam name="T">The type of cached data to deserialize.</typeparam>
    /// <param name="sessionId">The subscription session identifier.</param>
    /// <param name="purpose">The cache purpose that identifies the data bucket.</param>
    /// <returns>The cached value, or <see langword="null"/> when no value exists.</returns>
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
    /// Stores subscription payment data for the specified session and purpose.
    /// </summary>
    /// <typeparam name="T">The type of value to serialize and store.</typeparam>
    /// <param name="sessionId">The subscription session identifier.</param>
    /// <param name="purpose">The cache purpose that identifies the data bucket.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="options">Optional cache entry options, or <see langword="null"/> to use the default payment session lifetime.</param>
    /// <returns>A task that represents the asynchronous cache update operation.</returns>
    public async Task SetAsync<T>(string sessionId, string purpose, T value, DistributedCacheEntryOptions options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentException.ThrowIfNullOrEmpty(purpose);
        ArgumentNullException.ThrowIfNull(value);

        var key = GetKey(sessionId, purpose);

        await LockCacheAsync(key, async () =>
        {
            var data = JsonSerializer.SerializeToUtf8Bytes(value);

            await _distributedCache.SetAsync(key, data, options ?? new DistributedCacheEntryOptions()
            {
                AbsoluteExpirationRelativeToNow = _options.MaxLiveSession,
            });
        });
    }

    /// <summary>
    /// Adds a new cached value or updates the existing cached value for the specified session and purpose.
    /// </summary>
    /// <typeparam name="T">The type of value to serialize and store.</typeparam>
    /// <param name="sessionId">The subscription session identifier.</param>
    /// <param name="purpose">The cache purpose that identifies the data bucket.</param>
    /// <param name="value">The value to store when no cached value exists.</param>
    /// <param name="updater">The action that mutates the existing cached value when one exists.</param>
    /// <param name="options">Optional cache entry options, or <see langword="null"/> to use the default payment session lifetime.</param>
    /// <returns>The value that was stored in the cache.</returns>
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

            await _distributedCache.SetAsync(key, data, options ?? new DistributedCacheEntryOptions()
            {
                AbsoluteExpirationRelativeToNow = _options.MaxLiveSession,
            });
        });

        return finalValue;
    }

    /// <summary>
    /// Removes cached subscription payment data for the specified session and purpose.
    /// </summary>
    /// <param name="sessionId">The subscription session identifier.</param>
    /// <param name="purpose">The cache purpose that identifies the data bucket.</param>
    /// <returns>A task that represents the asynchronous cache removal operation.</returns>
    public async Task RemoveAsync(string sessionId, string purpose)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentException.ThrowIfNullOrEmpty(purpose);

        await _distributedCache.RemoveAsync(GetKey(sessionId, purpose));
    }

    /// <summary>
    /// Removes cached subscription payment data for all configured purposes in the specified session.
    /// </summary>
    /// <param name="sessionId">The subscription session identifier.</param>
    /// <returns>A task that represents the asynchronous cache removal operation.</returns>
    public async Task RemoveAsync(string sessionId)
    {
        foreach (var purpose in _options.Purposes)
        {
            await RemoveAsync(sessionId, purpose);
        }
    }

    private async Task LockCacheAsync(string key, Func<Task> callback)
    {
        var limit = TimeSpan.FromMilliseconds(2_000);

        var counter = 0;
        var lockKey = $"PAYMENT_{key}_LOCK";

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(lockKey, limit);

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

    private string GetKey(string sessionId, string key)
    => $"{GetPrefix(sessionId)}{key}";

    private string GetPrefix(string sessionId)
        => $"{_shellSettings.Name}_{sessionId}__Subscription__";
}

/// <summary>
/// Configures expiration and cache purposes for subscription payment sessions.
/// </summary>
public class SubscriptionPaymentSessionOptions
{
    /// <summary>
    /// Gets or sets the maximum amount of time that payment session data can live in the cache.
    /// </summary>
    public TimeSpan MaxLiveSession { get; set; }

    /// <summary>
    /// Gets the configured payment session purposes that are cleared when a session is removed.
    /// </summary>
    public List<string> Purposes { get; } = [];
}

/// <summary>
/// Provides typed helper methods for common subscription payment session data.
/// </summary>
public static class SubscriptionPaymentSessionExtensions
{
    /// <summary>
    /// The cache purpose used for initial payment metadata.
    /// </summary>
    public const string InitialPaymentPurpose = "InitialPayment";

    /// <summary>
    /// The cache purpose used for recurring subscription payment metadata.
    /// </summary>
    public const string SubscriptionPaymentInfoPurpose = "SubscriptionPaymentInfo";

    /// <summary>
    /// The cache purpose used for protected user registration password data.
    /// </summary>
    public const string UserRegistrationPurpose = "UserRegistration";

    /// <summary>
    /// Gets cached initial payment metadata for the specified subscription session.
    /// </summary>
    /// <param name="session">The subscription payment session store.</param>
    /// <param name="sessionId">The subscription session identifier.</param>
    /// <returns>The cached initial payment metadata, or <see langword="null"/> when none exists.</returns>
    public static Task<InitialPaymentMetadata> GetInitialPaymentInfoAsync(this SubscriptionPaymentSession session, string sessionId)
        => session.GetAsync<InitialPaymentMetadata>(sessionId, InitialPaymentPurpose);

    /// <summary>
    /// Stores initial payment metadata for the specified subscription session.
    /// </summary>
    /// <param name="session">The subscription payment session store.</param>
    /// <param name="sessionId">The subscription session identifier.</param>
    /// <param name="info">The initial payment metadata to store.</param>
    /// <returns>A task that represents the asynchronous cache update operation.</returns>
    public static Task SetAsync(this SubscriptionPaymentSession session, string sessionId, InitialPaymentMetadata info)
        => session.SetAsync(sessionId, InitialPaymentPurpose, info);

    /// <summary>
    /// Gets cached recurring subscription payment metadata for the specified subscription session.
    /// </summary>
    /// <param name="session">The subscription payment session store.</param>
    /// <param name="sessionId">The subscription session identifier.</param>
    /// <returns>The cached subscription payment metadata, or <see langword="null"/> when none exists.</returns>
    public static Task<SubscriptionPaymentsMetadata> GetSubscriptionPaymentInfoAsync(this SubscriptionPaymentSession session, string sessionId)
        => session.GetAsync<SubscriptionPaymentsMetadata>(sessionId, SubscriptionPaymentInfoPurpose);

    /// <summary>
    /// Stores recurring subscription payment metadata for the specified subscription session.
    /// </summary>
    /// <param name="session">The subscription payment session store.</param>
    /// <param name="sessionId">The subscription session identifier.</param>
    /// <param name="info">The subscription payment metadata to store.</param>
    /// <returns>A task that represents the asynchronous cache update operation.</returns>
    public static Task SetAsync(this SubscriptionPaymentSession session, string sessionId, SubscriptionPaymentsMetadata info)
        => session.SetAsync(sessionId, SubscriptionPaymentInfoPurpose, info);

    /// <summary>
    /// Adds or updates recurring subscription payment metadata for the specified subscription session.
    /// </summary>
    /// <param name="session">The subscription payment session store.</param>
    /// <param name="sessionId">The subscription session identifier.</param>
    /// <param name="info">The metadata to store when no cached value exists.</param>
    /// <param name="updater">The action that mutates the existing cached metadata when one exists.</param>
    /// <returns>The subscription payment metadata that was stored in the cache.</returns>
    public static Task<SubscriptionPaymentsMetadata> AddOrUpdateAsync(this SubscriptionPaymentSession session, string sessionId, SubscriptionPaymentsMetadata info, Action<SubscriptionPaymentsMetadata> updater)
        => session.AddOrUpdateAsync(sessionId, SubscriptionPaymentInfoPurpose, info, updater);

    /// <summary>
    /// Removes cached payment metadata for the specified subscription session.
    /// </summary>
    /// <param name="session">The subscription payment session store.</param>
    /// <param name="sessionId">The subscription session identifier.</param>
    /// <returns>A task that represents the asynchronous cache removal operation.</returns>
    public static async Task RemovePaymentInfoAsync(this SubscriptionPaymentSession session, string sessionId)
    {
        await session.RemoveAsync(sessionId, InitialPaymentPurpose);
        await session.RemoveAsync(sessionId, SubscriptionPaymentInfoPurpose);
    }

    /// <summary>
    /// Gets and unprotects the cached user registration password for the specified subscription session.
    /// </summary>
    /// <param name="session">The subscription payment session store.</param>
    /// <param name="sessionId">The subscription session identifier.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to unprotect the cached password.</param>
    /// <returns>The unprotected password, or <see langword="null"/> when no password is cached.</returns>
    public static async Task<string> GetUserPasswordAsync(this SubscriptionPaymentSession session, string sessionId, IDataProtectionProvider dataProtectionProvider)
    {
        var protectedPassword = await session.GetAsync<string>(sessionId, UserRegistrationPurpose);

        if (!string.IsNullOrEmpty(protectedPassword))
        {
            return GetPasswordProtector(dataProtectionProvider).Unprotect(protectedPassword);
        }

        return null;
    }

    /// <summary>
    /// Determines whether a protected user registration password is cached for the specified subscription session.
    /// </summary>
    /// <param name="session">The subscription payment session store.</param>
    /// <param name="sessionId">The subscription session identifier.</param>
    /// <returns><see langword="true"/> when a password is cached; otherwise, <see langword="false"/>.</returns>
    public static async Task<bool> UserPasswordExistsAsync(this SubscriptionPaymentSession session, string sessionId)
    {
        var protectedPassword = await session.GetAsync<string>(sessionId, UserRegistrationPurpose);

        return !string.IsNullOrEmpty(protectedPassword);
    }

    /// <summary>
    /// Protects and stores a user registration password for the specified subscription session.
    /// </summary>
    /// <param name="session">The subscription payment session store.</param>
    /// <param name="sessionId">The subscription session identifier.</param>
    /// <param name="rawPassword">The raw password to protect before caching.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to protect the password.</param>
    /// <returns>A task that represents the asynchronous cache update operation.</returns>
    public static async Task SetUserPasswordAsync(this SubscriptionPaymentSession session, string sessionId, string rawPassword, IDataProtectionProvider dataProtectionProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawPassword);

        await session.SetAsync(sessionId,
            purpose: UserRegistrationPurpose,
            value: GetPasswordProtector(dataProtectionProvider).Protect(rawPassword));
    }

    /// <summary>
    /// Removes the cached user registration password for the specified subscription session.
    /// </summary>
    /// <param name="session">The subscription payment session store.</param>
    /// <param name="sessionId">The subscription session identifier.</param>
    /// <returns>A task that represents the asynchronous cache removal operation.</returns>
    public static Task RemoveUserPasswordAsync(this SubscriptionPaymentSession session, string sessionId)
        => session.RemoveAsync(sessionId, UserRegistrationPurpose);

    private static IDataProtector GetPasswordProtector(IDataProtectionProvider protectionProvider)
        => protectionProvider.CreateProtector("Subscription_UserRegistration_Password");
}
