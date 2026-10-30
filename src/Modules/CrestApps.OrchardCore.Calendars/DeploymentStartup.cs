using CrestApps.OrchardCore.Calendars.Deployments;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Registers the Calendars deployment step.
/// </summary>
[RequireFeatures("OrchardCore.Deployment")]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<CalendarsDeploymentSource, CalendarsDeploymentStep, CalendarsDeploymentStepDisplayDriver>();
    }
}
