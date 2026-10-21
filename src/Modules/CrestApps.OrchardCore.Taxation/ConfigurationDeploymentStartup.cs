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
        services.AddDeployment<TaxCategoryDeploymentSource, TaxCategoryDeploymentStep, TaxCategoryDeploymentStepDisplayDriver>();
        services.AddDeployment<TaxTypeDeploymentSource, TaxTypeDeploymentStep, TaxTypeDeploymentStepDisplayDriver>();
        services.AddDeployment<TaxJurisdictionDeploymentSource, TaxJurisdictionDeploymentStep, TaxJurisdictionDeploymentStepDisplayDriver>();
        services.AddDeployment<TaxRuleDeploymentSource, TaxRuleDeploymentStep, TaxRuleDeploymentStepDisplayDriver>();
        services.AddDeployment<TaxTableDeploymentSource, TaxTableDeploymentStep, TaxTableDeploymentStepDisplayDriver>();
    }
}
