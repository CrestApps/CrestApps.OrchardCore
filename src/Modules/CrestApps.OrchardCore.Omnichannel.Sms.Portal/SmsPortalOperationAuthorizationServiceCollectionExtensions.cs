using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal;

/// <summary>
/// Provides extension methods for answering the SMS portal's authorization operations.
/// </summary>
public static class SmsPortalOperationAuthorizationServiceCollectionExtensions
{
    /// <summary>
    /// Maps the SMS portal's authorization operations onto this host's permissions.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddSmsPortalOperationAuthorization(this IServiceCollection services)
    {
        // The handler consults the authorization service, which is what builds the handler, so the
        // reference is deferred rather than taken during construction.
        services.TryAddScoped(serviceProvider =>
            new Lazy<IAuthorizationService>(serviceProvider.GetRequiredService<IAuthorizationService>));

        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IAuthorizationHandler, SmsPortalOperationAuthorizationHandler>());

        return services;
    }
}
