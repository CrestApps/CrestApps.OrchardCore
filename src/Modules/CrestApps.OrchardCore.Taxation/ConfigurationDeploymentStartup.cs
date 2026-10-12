using CrestApps.OrchardCore.Taxation.Deployments.Drivers;
using CrestApps.OrchardCore.Taxation.Deployments.Sources;
using CrestApps.OrchardCore.Taxation.Deployments.Steps;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Taxation;

/// <summary>
/// Registers the deployment steps that export taxation configuration.
/// </summary>
[RequireFeatures("OrchardCore.Deployment")]
public sealed class ConfigurationDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<TaxCategoryDeploymentSource, TaxCategoryDeploymentStep>();
        services.AddDeployment<TaxTypeDeploymentSource, TaxTypeDeploymentStep>();
        services.AddDeployment<TaxJurisdictionDeploymentSource, TaxJurisdictionDeploymentStep>();
        services.AddDeployment<TaxRuleDeploymentSource, TaxRuleDeploymentStep>();
        services.AddDeployment<TaxTableDeploymentSource, TaxTableDeploymentStep>();

        services.AddDisplayDriver<DeploymentStep, TaxCategoryDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, TaxTypeDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, TaxJurisdictionDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, TaxRuleDeploymentStepDisplayDriver>();
        services.AddDisplayDriver<DeploymentStep, TaxTableDeploymentStepDisplayDriver>();
    }
}
