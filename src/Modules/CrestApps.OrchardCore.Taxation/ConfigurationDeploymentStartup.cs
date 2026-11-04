using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Taxation.Deployments;
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
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class ConfigurationDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<TaxCategoryDeploymentSource, TaxCategoryDeploymentStep, SummaryDeploymentStepDisplayDriver<TaxCategoryDeploymentStep>>();
        services.AddDeployment<TaxTypeDeploymentSource, TaxTypeDeploymentStep, SummaryDeploymentStepDisplayDriver<TaxTypeDeploymentStep>>();
        services.AddDeployment<TaxJurisdictionDeploymentSource, TaxJurisdictionDeploymentStep, SummaryDeploymentStepDisplayDriver<TaxJurisdictionDeploymentStep>>();
        services.AddDeployment<TaxRuleDeploymentSource, TaxRuleDeploymentStep, SummaryDeploymentStepDisplayDriver<TaxRuleDeploymentStep>>();
        services.AddDeployment<TaxTableDeploymentSource, TaxTableDeploymentStep, SummaryDeploymentStepDisplayDriver<TaxTableDeploymentStep>>();
        services.AddDeployment<MerchantTaxRegistrationDeploymentSource, MerchantTaxRegistrationDeploymentStep, SummaryDeploymentStepDisplayDriver<MerchantTaxRegistrationDeploymentStep>>();
        services.AddDeployment<ExemptionCertificateDeploymentSource, ExemptionCertificateDeploymentStep, SummaryDeploymentStepDisplayDriver<ExemptionCertificateDeploymentStep>>();
    }
}
