using CrestApps.OrchardCore.Users.Core.Handlers;
using CrestApps.OrchardCore.Users.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.Users.Handlers;

namespace CrestApps.OrchardCore.Users.Core;

/// <summary>
/// Provides extension methods for service collection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Performs the try add basic display name provider operation.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection TryAddBasicDisplayNameProvider(this IServiceCollection services)
    {
        services.TryAddScoped<IDisplayNameProvider, DefaultDisplayNameProvider>();

        return services;
    }

    /// <summary>
    /// Adds the password confirmation sensitive pages ask for (<see cref="IPasswordConfirmation"/>).
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddPasswordConfirmation(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.TryAddScoped<IPasswordConfirmation, PasswordConfirmation>();

        return services;
    }

    /// <summary>
    /// Adds the display name provider.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddDisplayNameProvider(this IServiceCollection services)
    {
        services.AddScoped<IDisplayNameProvider, DisplayNameProvider>();

        return services;
    }

    /// <summary>
    /// Adds the shared <see cref="IUserSearchService"/> behind the user pickers, and the basic display name provider
    /// when no other one is registered. Call it from every feature that searches users; it adds each service once.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddUserSearchService(this IServiceCollection services)
    {
        services.TryAddBasicDisplayNameProvider();
        services.TryAddScoped<IUserSearchService, DefaultUserSearchService>();

        return services;
    }

    /// <summary>
    /// Adds <see cref="IRoleGrantService"/>, which puts users in roles on behalf of sources such as memberships and
    /// subscriptions and takes a role away only when no source still grants it. Safe to call from every module that
    /// grants roles; register an <see cref="IRoleGrantSource"/> for the grants a module made before.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <summary>
    /// Registers the audience service, the audiences administrators define, and the role rule.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddUserAudiences(this IServiceCollection services)
    {
        services.TryAddScoped<IUserAudienceService, Audiences.DefaultUserAudienceService>();
        services.AddUserAudienceRuleSource<Audiences.RoleUserAudienceRuleSource>();

        return services;
    }

    public static IServiceCollection AddRoleGrants(this IServiceCollection services)
    {
        services.TryAddScoped<IRoleGrantService, DefaultRoleGrantService>();

        return services;
    }

    /// <summary>
    /// Records an address as proved (<see cref="UserEmailOwnership"/>) when the user follows the link e-mailed to
    /// confirm it, and binds that link to the address it was sent to, so an old link does not confirm an address the user
    /// changed to afterwards. Call it from every feature that relies on the marker, before registering a user event
    /// handler that reads it on <c>ConfirmedAsync</c>; it adds each service once. The feature must depend on Orchard
    /// Core's Users feature, whose token configuration this replaces.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddEmailOwnershipConfirmation(this IServiceCollection services)
    {
        services.TryAddTransient<EmailBoundConfirmationTokenProvider>();
        services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<IdentityOptions>, EmailBoundConfirmationIdentityOptions>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IUserEventHandler, EmailOwnershipConfirmationHandler>());

        return services;
    }

    /// <summary>
    /// Adds the user cache service.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddUserCacheService(this IServiceCollection services)
    {
        services.AddScoped<IUserCacheService, DefaultUserCacheService>();
        services.AddScoped<IUserEventHandler, UserComponentsEventHandler>();

        return services;
    }
}
