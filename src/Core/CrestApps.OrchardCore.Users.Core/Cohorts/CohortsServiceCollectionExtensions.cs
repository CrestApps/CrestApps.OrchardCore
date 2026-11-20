using CrestApps.OrchardCore.Users.Core.Cohorts;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Users.Handlers;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers cohorts.
/// </summary>
public static class CohortsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the cohort service, the "Cohort member" audience rule and the handler that takes a deleted user out
    /// of every cohort. The module's feature registers the index providers and the migration.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddCohorts(this IServiceCollection services)
    {
        services.TryAddScoped<CrestApps.OrchardCore.Users.ICohortService, DefaultCohortService>();
        services.AddUserAudienceRuleSource<CohortUserAudienceRuleSource>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IUserEventHandler, CohortUserEventHandler>());

        return services;
    }
}
