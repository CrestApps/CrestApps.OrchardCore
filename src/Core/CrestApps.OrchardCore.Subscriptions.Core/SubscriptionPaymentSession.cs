using System.Text.Json;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Subscriptions.Core;

public sealed class SubscriptionPaymentSession
{
    private readonly IDistributedCache _distributedCache;
    private readonly SubscriptionPaymentSessionOptions _options;
    private readonly ShellSettings _shellSettings;

    public SubscriptionPaymentSession(
        IDistributedCache distributedCache,
        IOptions<SubscriptionPaymentSessionOptions> options,
        ShellSettings shellSettings)
    {
        _distributedCache = distributedCache;
        _options = options.Value;
        _shellSettings = shellSettings;
    }

    public async Task<T> GetAsync<T>(string sessionId, string purpose)
        where T : class
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentException.ThrowIfNullOrEmpty(purpose);

        var key = GetKey(sessionId, purpose);

        var data = await _distributedCache.GetAsync(key);

        if (data != null)
        {
            return JsonSerializer.Deserialize<T>(data);
        }

        return null;
    }

    public async Task SetAsync<T>(string sessionId, string purpose, T value, DistributedCacheEntryOptions options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentException.ThrowIfNullOrEmpty(purpose);
        ArgumentNullException.ThrowIfNull(value);

        var key = GetKey(sessionId, purpose);

        var data = JsonSerializer.SerializeToUtf8Bytes(value);

        await _distributedCache.SetAsync(key, data, options ?? new DistributedCacheEntryOptions()
        {
            AbsoluteExpirationRelativeToNow = _options.MaxLiveSession,
        });
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

    public static Task<InitialPaymentInfo> GetInitialPaymentInfoAsync(this SubscriptionPaymentSession session, string sessionId)
        => session.GetAsync<InitialPaymentInfo>(sessionId, InitialPaymentPurpose);

    public static Task SetAsync(this SubscriptionPaymentSession session, string sessionId, InitialPaymentInfo info)
        => session.SetAsync(sessionId, InitialPaymentPurpose, info);

    public static Task<SubscriptionPaymentInfo> GetSubscriptionPaymentInfoAsync(this SubscriptionPaymentSession session, string sessionId)
        => session.GetAsync<SubscriptionPaymentInfo>(sessionId, SubscriptionPaymentInfoPurpose);

    public static Task SetAsync(this SubscriptionPaymentSession session, string sessionId, SubscriptionPaymentInfo info)
        => session.SetAsync(sessionId, SubscriptionPaymentInfoPurpose, info);

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

    public static async Task SetUserPasswordAsync(this SubscriptionPaymentSession session, string sessionId, string rawPassword, IDataProtectionProvider dataProtectionProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawPassword);

        await session.SetAsync(sessionId,
            purpose: SubscriptionPaymentInfoPurpose,
            value: GetPasswordProtector(dataProtectionProvider).Protect(rawPassword));
    }

    private static IDataProtector GetPasswordProtector(IDataProtectionProvider protectionProvider)
        => protectionProvider.CreateProtector("Subscription_UserRegistration_Password");
}
