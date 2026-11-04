using CrestApps.Core.Services;
using CrestApps.OrchardCore.Skills.Core.Handlers;
using CrestApps.OrchardCore.Skills.Core.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Skills.Core;

/// <summary>
/// Registers the Skills services.
/// </summary>
public static class SkillsCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the skill and certification stores, managers, catalog handlers, the assignment and certification
    /// record services, and the daily check.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddSkillsCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<SkillStore>();
        services.TryAddScoped<CertificationStore>();
        services.TryAddScoped<SkillAssignmentStore>();
        services.TryAddScoped<CertificationRecordStore>();
        services.TryAddScoped<ISkillManager, SkillManager>();
        services.TryAddScoped<ICertificationManager, CertificationManager>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<Skill>, SkillCatalogHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<Certification>, CertificationCatalogHandler>());
        services.TryAddScoped<ISkillAssignmentService, DefaultSkillAssignmentService>();
        services.TryAddScoped<ICertificationRecordService, DefaultCertificationRecordService>();
        services.TryAddScoped<SkillsDailyCheck>();
        services.TryAddScoped<SkillsDailyCheckpointStore>();

        return services;
    }
}
