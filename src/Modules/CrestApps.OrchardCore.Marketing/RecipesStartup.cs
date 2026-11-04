using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Marketing.Deployments;
using CrestApps.OrchardCore.Marketing.Recipes;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.Marketing;

/// <summary>
/// Registers the <c>MarketingSegments</c> and <c>MarketingCampaigns</c> recipe steps and the deployment step that writes
/// them, so segments and campaigns move between sites. A recipe never sends email: imported campaigns are drafts.
/// </summary>
public sealed class RecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<MarketingSegmentsStep>();
        services.AddRecipeExecutionStep<MarketingCampaignsStep>();
        services.AddScoped<IRecipeStep, MarketingSegmentsRecipeStepSchema>();
        services.AddScoped<IRecipeStep, MarketingCampaignsRecipeStepSchema>();
        services.AddDeployment<MarketingDeploymentSource, MarketingDeploymentStep, SummaryDeploymentStepDisplayDriver<MarketingDeploymentStep>>();
        services.AddDeploymentStepSchema<MarketingDeploymentStepSchema>();
    }
}
