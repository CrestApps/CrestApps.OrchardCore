using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Deployment;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list, save and delete deployment plans, when the Deployment feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeploymentToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DeploymentToolsStartup(IStringLocalizer<DeploymentToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListDeploymentPlansTool>(ListDeploymentPlansTool.TheName)
            .WithTitle(S["List Deployment Plans"])
            .WithDescription(S["Lists deployment plans, optionally filtered by name and with their ordered steps, plus the step types available on this site. Use it to find a plan before exporting or changing it."])
            .WithCategory(S["Deployment"])
            .Selectable();

        services.AddCoreAITool<SaveDeploymentPlanTool>(SaveDeploymentPlanTool.TheName)
            .WithTitle(S["Save Deployment Plan"])
            .WithDescription(S["Creates a deployment plan or replaces an existing one: its name and the ordered list of steps. Send every step to keep; step types and their fields come from listDeploymentPlans."])
            .WithCategory(S["Deployment"])
            .Selectable();

        services.AddCoreAITool<DeleteDeploymentPlanTool>(DeleteDeploymentPlanTool.TheName)
            .WithTitle(S["Delete Deployment Plan"])
            .WithDescription(S["Permanently deletes a deployment plan. It does not touch data already exported."])
            .WithCategory(S["Deployment"])
            .AsDestructive()
            .Selectable();
    }
}
