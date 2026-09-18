using CrestApps.OrchardCore.ContactCenter.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Provides extension methods for answering the Contact Center's authorization operations.
/// </summary>
public static class ContactCenterOperationAuthorizationServiceCollectionExtensions
{
    /// <summary>
    /// Maps the Contact Center's authorization operations onto this host's permissions.
    /// </summary>
    /// <remarks>
    /// Called by every feature that registers a service asking one of those operations. An
    /// unhandled requirement is a denial, so a feature that forgets this loses the capability
    /// rather than opening it up — which is the safe direction, but still a bug.
    /// </remarks>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddContactCenterOperationAuthorization(this IServiceCollection services)
    {
        // The handler consults the authorization service, which is what builds the handler, so the
        // reference is deferred rather than taken during construction.
        services.TryAddScoped(serviceProvider =>
            new Lazy<IAuthorizationService>(serviceProvider.GetRequiredService<IAuthorizationService>));

        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IAuthorizationHandler, ContactCenterOperationAuthorizationHandler>());

        return services;
    }
}
