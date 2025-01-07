using System.Text.Json;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Subscriptions.Core;

public sealed class SubscriptionPaymentSession
{
    private const int MaxLockTries = 20;

    private readonly IDistributedCache _distributedCache;
    private readonly IDistributedLock _distributedLock;
    private readonly SubscriptionPaymentSessionOptions _options;
    private readonly ShellSettings _shellSettings;

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

    public async Task RemoveAsync(string sessionId, string purpose)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentException.ThrowIfNullOrEmpty(purpose);

        await _distributedCache.RemoveAsync(GetKey(sessionId, purpose));
    }

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

public class SubscriptionPaymentSessionOptions
{
    public TimeSpan MaxLiveSession { get; set; }

    public List<string> Purposes { get; } = [];
}

public static class SubscriptionPaymentSessionExtensions
{
    public const string InitialPaymentPurpose = "InitialPayment";
    public const string SubscriptionPaymentInfoPurpose = "SubscriptionPaymentInfo";
    public const string UserRegistrationPurpose = "SubscriptionPaymentInfo";

    public static Task<InitialPaymentMetadata> GetInitialPaymentInfoAsync(this SubscriptionPaymentSession session, string sessionId)
        => session.GetAsync<InitialPaymentMetadata>(sessionId, InitialPaymentPurpose);

    public static Task SetAsync(this SubscriptionPaymentSession session, string sessionId, InitialPaymentMetadata info)
        => session.SetAsync(sessionId, InitialPaymentPurpose, info);

    public static Task<SubscriptionPaymentsMetadata> GetSubscriptionPaymentInfoAsync(this SubscriptionPaymentSession session, string sessionId)
        => session.GetAsync<SubscriptionPaymentsMetadata>(sessionId, SubscriptionPaymentInfoPurpose);

    public static Task SetAsync(this SubscriptionPaymentSession session, string sessionId, SubscriptionPaymentsMetadata info)
        => session.SetAsync(sessionId, SubscriptionPaymentInfoPurpose, info);

    public static Task<SubscriptionPaymentsMetadata> AddOrUpdateAsync(this SubscriptionPaymentSession session, string sessionId, SubscriptionPaymentsMetadata info, Action<SubscriptionPaymentsMetadata> updater)
        => session.AddOrUpdateAsync(sessionId, SubscriptionPaymentInfoPurpose, info, updater);

    public static async Task RemovePaymentInfoAsync(this SubscriptionPaymentSession session, string sessionId)
    {
        await session.RemoveAsync(sessionId, InitialPaymentPurpose);
        await session.RemoveAsync(sessionId, SubscriptionPaymentInfoPurpose);
    }

    public static async Task<string> GetUserPasswordAsync(this SubscriptionPaymentSession session, string sessionId, IDataProtectionProvider dataProtectionProvider)
    {
        var protectedPassword = await session.GetAsync<string>(sessionId, UserRegistrationPurpose);

        if (!string.IsNullOrEmpty(protectedPassword))
        {
            return GetPasswordProtector(dataProtectionProvider).Unprotect(protectedPassword);
        }

        return null;
    }

    public static async Task<bool> UserPasswordExistsAsync(this SubscriptionPaymentSession session, string sessionId)
    {
        var protectedPassword = await session.GetAsync<string>(sessionId, UserRegistrationPurpose);

        return !string.IsNullOrEmpty(protectedPassword);
    }

    public static async Task SetUserPasswordAsync(this SubscriptionPaymentSession session, string sessionId, string rawPassword, IDataProtectionProvider dataProtectionProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawPassword);

        await session.SetAsync(sessionId,
            purpose: SubscriptionPaymentInfoPurpose,
            value: GetPasswordProtector(dataProtectionProvider).Protect(rawPassword));
    }

    public static Task RemoveUserPasswordAsync(this SubscriptionPaymentSession session, string sessionId)
        => session.RemoveAsync(sessionId, UserRegistrationPurpose);

    private static IDataProtector GetPasswordProtector(IDataProtectionProvider protectionProvider)
        => protectionProvider.CreateProtector("Subscription_UserRegistration_Password");
}
