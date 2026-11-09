using CrestApps.OrchardCore.Teams.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Teams;

/// <summary>
/// Registration helpers for modules that build on teams.
/// </summary>
public static class TeamsServiceCollectionExtensions
{
    /// <summary>
    /// Registers a provider of team members, so <see cref="ITeamMembershipService"/> answers with its members.
    /// </summary>
    /// <typeparam name="TProvider">The provider.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddTeamMemberProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, ITeamMemberProvider
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<ITeamMemberProvider, TProvider>());

        return services;
    }
}
