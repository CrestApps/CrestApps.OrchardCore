using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.ContactCenter.SharedSkills;

/// <summary>
/// Puts Contact Center on the shared skill catalog when the Skills feature is on.
/// </summary>
internal static class ContactCenterSharedSkills
{
    /// <summary>
    /// Makes the Contact Center skill store read and write the shared catalog, keeping a copy in its own store.
    /// </summary>
    public static IServiceCollection AddCatalog(IServiceCollection services)
    {
        services.TryAddScoped<ContactCenterSkillStore>();
        services.RemoveAll<IContactCenterSkillStore>();
        services.AddScoped<IContactCenterSkillStore, SharedContactCenterSkillStore>();
        services.TryAddSingleton<SharedSkillsCatalogBackfillState>();
        services.TryAddScoped<SharedSkillsCatalogBackfill>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<Skill>, ContactCenterSkillMirrorHandler>());

        return services;
    }

    /// <summary>
    /// Keeps agents' routing proficiencies and the users' shared skills in step.
    /// </summary>
    public static IServiceCollection AddAgentSync(IServiceCollection services)
    {
        services.TryAddScoped<AgentSkillsSync>();
        services.TryAddScoped<SharedAgentSkillsBackfill>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<AgentProfile>, AgentSkillsToSharedHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISkillAssignmentHandler, SharedAgentSkillsHandler>());

        return services;
    }
}
