using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Parties;

/// <summary>
/// Registration helpers for parties.
/// </summary>
public static class PartiesServiceCollectionExtensions
{
    /// <summary>
    /// Registers a source of parties of one kind, and the <see cref="IPartyDirectory"/> that reads every source.
    /// </summary>
    /// <typeparam name="TSource">The source.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddPartySource<TSource>(this IServiceCollection services)
        where TSource : class, IPartySource
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddPartyDirectory();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPartySource, TSource>());

        return services;
    }

    /// <summary>
    /// Registers a source of parties of one kind and describes the kind to the shared party picker: its name, and the
    /// permission a user needs to search it.
    /// </summary>
    /// <typeparam name="TSource">The source.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="kind">The kind the source finds.</param>
    /// <param name="displayName">The name of the kind shown to users.</param>
    /// <param name="searchPermission">The permission needed to search the kind in a picker.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddPartySource<TSource>(this IServiceCollection services, string kind, LocalizationSource displayName, Permission searchPermission)
        where TSource : class, IPartySource
    {
        ArgumentException.ThrowIfNullOrEmpty(kind);
        ArgumentNullException.ThrowIfNull(searchPermission);

        services.AddPartySource<TSource>();
        services.Configure<PartySourceOptions>(options => options.Kinds[kind] = new PartyKindDescriptor
        {
            Kind = kind,
            DisplayName = displayName,
            SearchPermission = searchPermission,
        });

        return services;
    }

    /// <summary>
    /// Registers a source that tells which parties of one kind a signed-in user is.
    /// </summary>
    /// <typeparam name="TSource">The source.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddUserPartySource<TSource>(this IServiceCollection services)
        where TSource : class, IUserPartySource
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IUserPartySource, TSource>());

        return services;
    }

    /// <summary>
    /// Registers the <see cref="IPartyDirectory"/> once, for a feature that reads parties without keeping any.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddPartyDirectory(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IPartyDirectory, PartyDirectory>();

        return services;
    }

    /// <summary>
    /// Registers a source of billing details for parties of one kind, and the <see cref="IPartyBillingProfileService"/>
    /// that reads every source. Sources of one kind run in the order they are registered.
    /// </summary>
    /// <typeparam name="TSource">The source.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddPartyBillingProfileSource<TSource>(this IServiceCollection services)
        where TSource : class, IPartyBillingProfileSource
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddPartyBillingProfiles();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPartyBillingProfileSource, TSource>());

        return services;
    }

    /// <summary>
    /// Registers the <see cref="IPartyBillingProfileService"/> once, and the <see cref="IPartyDirectory"/> it names
    /// parties with, for a feature that reads billing profiles without keeping any parties.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddPartyBillingProfiles(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddPartyDirectory();
        services.TryAddScoped<IPartyBillingProfileService, PartyBillingProfileService>();

        return services;
    }

    /// <summary>
    /// Registers a source of the users who may act for parties of one kind, and the
    /// <see cref="IPartyMembershipService"/> that reads every source.
    /// </summary>
    /// <typeparam name="TSource">The source.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddPartyMemberSource<TSource>(this IServiceCollection services)
        where TSource : class, IPartyMemberSource
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddPartyMembership();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPartyMemberSource, TSource>());

        return services;
    }

    /// <summary>
    /// Registers the <see cref="IPartyMembershipService"/> once, for a feature that checks who may act for a party
    /// without keeping any parties.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddPartyMembership(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IPartyMembershipService, PartyMembershipService>();

        return services;
    }
}
