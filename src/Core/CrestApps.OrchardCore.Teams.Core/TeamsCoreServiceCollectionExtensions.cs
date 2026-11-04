using CrestApps.Core.Services;
using CrestApps.OrchardCore.Teams.Core.Handlers;
using CrestApps.OrchardCore.Teams.Core.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Teams.Core;

/// <summary>
/// Registers the Teams services.
/// </summary>
public static class TeamsCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the team store, the team manager and the catalog handler that keeps the hierarchy a tree. It is safe to
    /// call more than once, so a module that builds on teams can call it from its own registration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddTeamsCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<TeamStore>();
        services.TryAddScoped<ITeamManager, TeamManager>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<Team>, TeamCatalogHandler>());

        return services;
    }

    /// <summary>
    /// Registers a check that can refuse to have a team deleted while a module still depends on it.
    /// </summary>
    /// <typeparam name="TGuard">The guard.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddTeamDeletionGuard<TGuard>(this IServiceCollection services)
        where TGuard : class, ITeamDeletionGuard
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITeamDeletionGuard, TGuard>());

        return services;
    }
}
