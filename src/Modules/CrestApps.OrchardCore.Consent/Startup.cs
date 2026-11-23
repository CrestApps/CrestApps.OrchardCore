using CrestApps.OrchardCore.Consent.Core.Indexes;
using CrestApps.OrchardCore.Consent.Deployments;
using CrestApps.OrchardCore.Consent.Migrations;
using CrestApps.OrchardCore.Consent.Recipes;
using CrestApps.OrchardCore.Consent.Services;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.PersonalData.Core;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.Consent;

/// <summary>
/// Registers the consent service, the address opt-outs and their consent source, the signed unsubscribe links, and the
/// e-mail preferences page they open.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddConsent();
        services.AddConsentAddressOptOuts();
        services.AddUnsubscribeLinks();
        services
            .AddDataMigration<ConsentAddressOptOutMigrations>()
            .AddIndexProvider<ConsentAddressOptOutIndexProvider>();
        services.AddPersonalDataContributor<ConsentAddressOptOutPersonalDataContributor>();
    }
}

/// <summary>
/// Registers the <c>ConsentAddressOptOuts</c> recipe step, so address opt-outs move between sites.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RecipesCore)]
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<ConsentAddressOptOutsStep>();
    }
}

/// <summary>
/// Registers the deployment step that exports the address opt-outs.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.Deployment)]
public sealed class DeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<ConsentAddressOptOutsDeploymentSource, ConsentAddressOptOutsDeploymentStep, SummaryDeploymentStepDisplayDriver<ConsentAddressOptOutsDeploymentStep>>();
    }
}

/// <summary>
/// Describes the address opt-outs recipe and deployment steps to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, ConsentAddressOptOutsRecipeStep>();
        services.AddDeploymentStepSchema<ConsentAddressOptOutsDeploymentStepSchema>();
    }
}
