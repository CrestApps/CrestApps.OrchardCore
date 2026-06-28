using CrestApps.OrchardCore.Telephony;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// A telephony provider resolver that always returns a preset provider.
/// </summary>
internal sealed class StubTelephonyProviderResolver : ITelephonyProviderResolver
{
    private readonly ITelephonyProvider _provider;

    public StubTelephonyProviderResolver(ITelephonyProvider provider)
    {
        _provider = provider;
    }

    public Task<ITelephonyProvider> GetAsync(string name = null)
        => Task.FromResult(_provider);
}
