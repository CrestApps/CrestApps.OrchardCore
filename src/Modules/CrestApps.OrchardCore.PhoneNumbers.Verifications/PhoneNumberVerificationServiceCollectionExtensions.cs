using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications;

/// <summary>
/// Provides extension methods used by provider features to register their
/// <see cref="IPhoneNumberVerificationProvider"/> implementations.
/// </summary>
public static class PhoneNumberVerificationServiceCollectionExtensions
{
    /// <summary>
    /// Registers a phone number verification provider under a unique key and adds its
    /// descriptor so the core feature can discover it without referencing the implementation.
    /// </summary>
    /// <typeparam name="TProvider">The provider implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="key">The unique provider key.</param>
    /// <param name="configure">An optional configuration action for localized provider metadata.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddPhoneNumberVerificationProvider<TProvider>(
        this IServiceCollection services,
        string key,
        Action<PhoneNumberVerificationProviderDescriptor> configure = null)
        where TProvider : class, IPhoneNumberVerificationProvider
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        services.AddKeyedScoped<IPhoneNumberVerificationProvider, TProvider>(key);

        services.Configure<PhoneNumberVerificationProviderOptions>(options =>
        {
            var descriptor = new PhoneNumberVerificationProviderDescriptor(key);

            configure?.Invoke(descriptor);

            if (descriptor.DisplayName is null || string.IsNullOrEmpty(descriptor.DisplayName.Value))
            {
                descriptor.DisplayName = new LocalizedString(key, key);
            }

            options.Providers[key] = descriptor;
        });

        return services;
    }

    /// <summary>
    /// Registers a phone number verification provider under a unique key together with a settings type
    /// that controls whether the provider is enabled. The provider only appears for selection once its
    /// settings are enabled from the admin UI.
    /// </summary>
    /// <typeparam name="TProvider">The provider implementation type.</typeparam>
    /// <typeparam name="TSettings">The provider settings type controlling the enabled state.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="key">The unique provider key.</param>
    /// <param name="configure">An optional configuration action for localized provider metadata.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddPhoneNumberVerificationProvider<TProvider, TSettings>(
        this IServiceCollection services,
        string key,
        Action<PhoneNumberVerificationProviderDescriptor> configure = null)
        where TProvider : class, IPhoneNumberVerificationProvider
        where TSettings : class, IPhoneNumberVerificationProviderSettings, new()
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        services.AddPhoneNumberVerificationProvider<TProvider>(key, configure);

        services.AddScoped<IPhoneNumberVerificationProviderConfiguration>(serviceProvider =>
            new SettingsPhoneNumberVerificationProviderConfiguration<TSettings>(
                key,
                serviceProvider.GetRequiredService<ISiteService>()));

        return services;
    }
}
