using CrestApps.OrchardCore.PhoneNumberVerifications.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.PhoneNumberVerifications;

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
    /// <param name="displayName">The human-readable provider display name.</param>
    /// <param name="description">The human-readable provider description.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddPhoneNumberVerificationProvider<TProvider>(
        this IServiceCollection services,
        string key,
        string displayName,
        string description)
        where TProvider : class, IPhoneNumberVerificationProvider
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        services.AddKeyedScoped<IPhoneNumberVerificationProvider, TProvider>(key);

        services.Configure<PhoneNumberVerificationProviderOptions>(options =>
        {
            options.Providers[key] = new PhoneNumberVerificationProviderDescriptor
            {
                Key = key,
                DisplayName = displayName,
                Description = description,
            };
        });

        return services;
    }
}
