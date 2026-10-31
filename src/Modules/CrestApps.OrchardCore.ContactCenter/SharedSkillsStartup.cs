using CrestApps.OrchardCore.ContactCenter.SharedSkills;
using CrestApps.OrchardCore.Skills;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Puts the Contact Center skills on the shared skill catalog when the Skills feature is on: one catalog, the same ids,
/// and a copy kept in the Contact Center store.
/// </summary>
[Feature(ContactCenterConstants.Feature.Queues)]
[RequireFeatures(SkillsConstants.Features.Skills)]
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
/// Keeps agents' routing proficiencies and the users' skills in the shared catalog in step when the Skills feature is on.
/// </summary>
[Feature(ContactCenterConstants.Feature.Agents)]
[RequireFeatures(SkillsConstants.Features.Skills)]
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
