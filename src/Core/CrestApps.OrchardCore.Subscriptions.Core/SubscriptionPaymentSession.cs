using System.Text.Json;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Microsoft.Extensions.Caching.Distributed;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Subscriptions.Core;

public sealed class SubscriptionPaymentSession
{
    private readonly IDistributedCache _distributedCache;
    private readonly ShellSettings _shellSettings;

    public SubscriptionPaymentSession(
        IDistributedCache distributedCache,
        ShellSettings shellSettings)
    {
        _distributedCache = distributedCache;
        _shellSettings = shellSettings;
    }

    public async Task<InitialPaymentInfo> GetInitialPaymentInfoAsync(string sessionId)
    {
        var key = GetInitialPaymentKey(sessionId);

        var data = await _distributedCache.GetAsync(key);

        if (data != null)
        {
            return JsonSerializer.Deserialize<InitialPaymentInfo>(data);
        }

        return null;
    }

    public async Task SetAsync(string sessionId, InitialPaymentInfo info)
    {
        var key = GetInitialPaymentKey(sessionId);

        var data = JsonSerializer.SerializeToUtf8Bytes(info);

        await _distributedCache.SetAsync(key, data, new DistributedCacheEntryOptions()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
        });
    }


    public async Task<SubscriptionPaymentInfo> GetSubscriptionPaymentInfoAsync(string sessionId)
    {
        var key = GetSubscriptionPaymentInfoKey(sessionId);

        var data = await _distributedCache.GetAsync(key);

        if (data != null)
        {
            return JsonSerializer.Deserialize<SubscriptionPaymentInfo>(data);
        }

        return null;
    }

    public async Task SetAsync(string sessionId, SubscriptionPaymentInfo info)
    {
        var key = GetSubscriptionPaymentInfoKey(sessionId);

        var data = JsonSerializer.SerializeToUtf8Bytes(info);

        await _distributedCache.SetAsync(key, data, new DistributedCacheEntryOptions()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
        });
    }

    private string GetInitialPaymentKey(string sessionId)
        => $"{_shellSettings.Name}_{sessionId}_Subscription_InitialPayment";

    private string GetSubscriptionPaymentInfoKey(string sessionId)
        => $"{_shellSettings.Name}_{sessionId}_Subscription_SubscriptionPaymentInfo";

    public async Task RemoveAsync(string sessionId)
    {
        await _distributedCache.RemoveAsync(GetInitialPaymentKey(sessionId));
        await _distributedCache.RemoveAsync(GetSubscriptionPaymentInfoKey(sessionId));
    }
}
