using CrestApps.OrchardCore.Core.Configuration;
using CrestApps.OrchardCore.Omnichannel.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements;

/// <summary>
/// Registers the recipe step that imports Omnichannel configuration.
/// </summary>
[Feature(OmnichannelConstants.Features.Activities)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class ConfigurationRecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddConfigurationCatalogRecipeStep();
    }
}
