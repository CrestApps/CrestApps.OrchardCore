using CrestApps.OrchardCore.Telephony.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Provides extension methods for answering telephony's authorization operations.
/// </summary>
public static class TelephonyOperationAuthorizationServiceCollectionExtensions
{
    /// <summary>
    /// Maps telephony's authorization operations onto this host's permissions.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddTelephonyOperationAuthorization(this IServiceCollection services)
    {
        // The handler consults the authorization service, which is what builds the handler, so the
        // reference is deferred rather than taken during construction.
        services.TryAddScoped(serviceProvider =>
            new Lazy<IAuthorizationService>(serviceProvider.GetRequiredService<IAuthorizationService>));

        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IAuthorizationHandler, TelephonyOperationAuthorizationHandler>());

        return services;
    }
}
