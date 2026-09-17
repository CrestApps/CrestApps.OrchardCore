using CrestApps.Core.Sms;
using OrchardCore.Settings;
using OrchardSms = OrchardCore.Sms;

namespace CrestApps.OrchardCore.Core.Sms;

/// <summary>
/// Resolves framework SMS providers from the providers Orchard Core has registered and enabled.
/// </summary>
public sealed class OrchardCoreSmsProviderResolver : ISmsProviderResolver
{
    private readonly OrchardSms.ISmsProviderResolver _resolver;
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchardCoreSmsProviderResolver"/> class.
    /// </summary>
    /// <param name="resolver">The Orchard Core provider resolver.</param>
    /// <param name="siteService">The site service that holds the tenant's default provider name.</param>
    public OrchardCoreSmsProviderResolver(OrchardSms.ISmsProviderResolver resolver, ISiteService siteService)
    {
        _resolver = resolver;
        _siteService = siteService;
    }

    /// <inheritdoc/>
    public async Task<ISmsProvider> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var provider = await _resolver.GetAsync(name);

        if (provider is null)
        {
            return null;
        }

        // A provider that is only a shim over a framework provider is handed back unwrapped. Wrapping
        // it again would hide everything the inner provider does beyond the plain send contract - most
        // importantly reporting its own message id, which is what a later delivery receipt matches on.
        return provider is ICoreSmsProviderAdapter adapter
            ? adapter.InnerProvider
            : new OrchardCoreSmsProvider(name, provider);
    }

    /// <inheritdoc/>
    public async Task<string> GetDefaultProviderNameAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _siteService.GetSettingsAsync<OrchardSms.SmsSettings>();

        return settings?.DefaultProviderName;
    }
}
