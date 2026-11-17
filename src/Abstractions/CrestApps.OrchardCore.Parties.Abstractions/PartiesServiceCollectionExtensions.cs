using CrestApps.OrchardCore.Parties.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
}
