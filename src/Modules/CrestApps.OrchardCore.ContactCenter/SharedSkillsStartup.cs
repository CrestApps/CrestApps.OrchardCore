using CrestApps.OrchardCore.ContactCenter.SharedSkills;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Keeps the Contact Center skills in the shared Skills catalog: Contact Center reads and writes skills through the
/// catalog, with the same ids, and skills kept by Contact Center before it used the catalog are copied there once.
/// </summary>
[Feature(ContactCenterConstants.Feature.Queues)]
public sealed class SharedSkillsStartup : StartupBase
{
    /// <inheritdoc />
    public override int Order => 1000;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ContactCenterSharedSkills.AddCatalog(services);
    }
}

/// <summary>
/// Keeps agents' routing proficiencies and the users' skills in the shared catalog in step.
/// </summary>
[Feature(ContactCenterConstants.Feature.Agents)]
public sealed class SharedAgentSkillsStartup : StartupBase
{
    /// <inheritdoc />
    public override int Order => 1000;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ContactCenterSharedSkills.AddAgentSync(services);
        services.AddSingleton<IBackgroundTask, SharedAgentSkillsBackfillBackgroundTask>();
    }
}
