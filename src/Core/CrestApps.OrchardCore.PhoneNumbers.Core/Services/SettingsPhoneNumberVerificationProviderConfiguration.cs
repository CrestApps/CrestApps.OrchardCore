using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.PhoneNumbers.Core.Services;

/// <summary>
/// A settings-backed <see cref="IPhoneNumberVerificationProviderConfiguration"/> that reports a
/// provider as enabled based on the <see cref="IPhoneNumberVerificationProviderSettings.IsEnabled"/>
/// flag stored in its site settings.
/// </summary>
/// <typeparam name="TSettings">The provider settings type.</typeparam>
public sealed class SettingsPhoneNumberVerificationProviderConfiguration<TSettings> : IPhoneNumberVerificationProviderConfiguration
    where TSettings : class, IPhoneNumberVerificationProviderSettings, new()
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsPhoneNumberVerificationProviderConfiguration{TSettings}"/> class.
    /// </summary>
    /// <param name="providerKey">The key of the provider this configuration applies to.</param>
    /// <param name="siteService">The site service used to read the provider settings.</param>
    public SettingsPhoneNumberVerificationProviderConfiguration(
        string providerKey,
        ISiteService siteService)
    {
        ProviderKey = providerKey;
        _siteService = siteService;
    }

    /// <inheritdoc/>
    public string ProviderKey { get; }

    /// <inheritdoc/>
    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _siteService.GetSettingsAsync<TSettings>();

        return settings.IsEnabled;
    }
}
