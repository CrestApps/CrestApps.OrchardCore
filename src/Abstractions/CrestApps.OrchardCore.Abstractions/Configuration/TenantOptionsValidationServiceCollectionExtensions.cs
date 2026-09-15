using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Configuration;

/// <summary>
/// Service collection extensions that enable tenant-scoped options validation.
/// </summary>
public static class TenantOptionsValidationServiceCollectionExtensions
{
    /// <summary>
    /// Ensures every <c>ValidateOnStart</c> rule registered by any feature in this tenant is evaluated when the
    /// tenant activates, rather than when the option is first read.
    /// </summary>
    /// <remarks>
    /// Safe to call from more than one feature: the registration is de-duplicated, so the validation runs once
    /// per tenant activation no matter how many features ask for it.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection ValidateTenantOptionsOnActivation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IModularTenantEvents, TenantOptionsStartupValidator>());

        return services;
    }
}
