using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Managements.Recipes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.Omnichannel.Managements;

/// <summary>
/// Registers the recipe steps that import Omnichannel configuration.
/// </summary>
[Feature(OmnichannelConstants.Features.Activities)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class ConfigurationRecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<OmnichannelDispositionStep>();
        services.AddRecipeExecutionStep<OmnichannelChannelEndpointStep>();
        services.AddRecipeExecutionStep<OmnichannelCampaignGroupStep>();
        services.AddRecipeExecutionStep<OmnichannelCampaignStep>();
        services.AddRecipeExecutionStep<OmnichannelSubjectActionStep>();
    }
}
