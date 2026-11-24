using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers what builds on cohorts.
/// </summary>
public static class CohortServiceCollectionExtensions
{
    /// <summary>
    /// Registers an observer of the people who join and leave cohorts.
    /// </summary>
    /// <typeparam name="TObserver">The observer.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddCohortMembershipObserver<TObserver>(this IServiceCollection services)
        where TObserver : class, ICohortMembershipObserver
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICohortMembershipObserver, TObserver>());

        return services;
    }

    /// <summary>
    /// Registers something that uses cohorts, so a cohort it uses cannot be deleted.
    /// </summary>
    /// <typeparam name="TUsage">The usage.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddCohortUsage<TUsage>(this IServiceCollection services)
        where TUsage : class, ICohortUsage
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICohortUsage, TUsage>());

        return services;
    }
}
