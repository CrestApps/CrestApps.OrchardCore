using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Skills.Workflows;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Skills;

/// <summary>
/// Registers the Skills workflow events and task.
/// </summary>
[RequireFeatures("OrchardCore.Workflows")]
public sealed class WorkflowsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<SkillsWorkflowObserver>();
        services.AddScoped<ICertificationExpiryObserver>(provider => provider.GetRequiredService<SkillsWorkflowObserver>());
        services.AddScoped<ISkillAssignmentHandler>(provider => provider.GetRequiredService<SkillsWorkflowObserver>());

        services.AddActivity<CertificationExpiringEvent, SkillsEventActivityDisplayDriver<CertificationExpiringEvent>>();
        services.AddActivity<CertificationExpiredEvent, SkillsEventActivityDisplayDriver<CertificationExpiredEvent>>();
        services.AddActivity<SkillsChangedEvent, SkillsEventActivityDisplayDriver<SkillsChangedEvent>>();
        services.AddActivity<AssignSkillTask, AssignSkillTaskDisplayDriver>();
    }
}
