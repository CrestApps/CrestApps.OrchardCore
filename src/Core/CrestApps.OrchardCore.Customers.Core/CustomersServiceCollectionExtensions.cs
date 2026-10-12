using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Customers.Core;

/// <summary>
/// Registration helpers for the reusable buyer-identity services.
/// </summary>
public static class CustomersServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default buyer-identity services (the contact resolver) used by commerce consumers to
    /// address authenticated and guest buyers uniformly.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddCustomersCore(this IServiceCollection services)
    {
        services.TryAddScoped<ICustomerContactResolver, DefaultCustomerContactResolver>();

        return services;
    }
}
