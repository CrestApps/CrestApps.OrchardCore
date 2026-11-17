using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tool that runs the site's health checks, when the Health Checks feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.HealthChecks)]
public sealed class HealthChecksToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthChecksToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public HealthChecksToolsStartup(IStringLocalizer<HealthChecksToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<GetSiteHealthTool>(GetSiteHealthTool.TheName)
            .WithTitle(S["Get Site Health"])
            .WithDescription(S["Runs the site's registered health checks and returns the overall status and each check's status and description. Use it when the user asks whether the site or one of its dependencies is working."])
            .WithCategory(S["Health Checks"])
            .Selectable();
    }
}
