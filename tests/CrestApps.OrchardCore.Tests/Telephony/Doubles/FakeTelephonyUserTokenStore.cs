using System.Collections.Concurrent;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// An in-memory token store used to test the authentication service.
/// </summary>
internal sealed class FakeTelephonyUserTokenStore : ITelephonyUserTokenStore
{
    private readonly ConcurrentDictionary<string, TelephonyUserTokens> _store = new(StringComparer.Ordinal);

    public Task<TelephonyUserTokens> GetAsync(string providerName, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(providerName, out var tokens) ? tokens : null);

    public Task StoreAsync(string providerName, TelephonyUserTokens tokens, CancellationToken cancellationToken = default)
    {
        _store[providerName] = tokens;

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string providerName, CancellationToken cancellationToken = default)
    {
        _store.TryRemove(providerName, out _);

        return Task.CompletedTask;
    }
}
