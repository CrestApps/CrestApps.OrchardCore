using CrestApps.OrchardCore.ContactCenter.Recipes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the recipe steps that import the dialer profiles owned by the dialer feature.
/// </summary>
[Feature(ContactCenterConstants.Feature.Dialer)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class DialerRecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<ContactCenterDialerProfileStep>();
    }
}
