using CrestApps.OrchardCore.ContactCenter.Recipes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the recipe steps that import the entry points owned by the entry points feature.
/// </summary>
[Feature(ContactCenterConstants.Feature.EntryPoints)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class EntryPointsRecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<ContactCenterEntryPointStep>();
    }
}
