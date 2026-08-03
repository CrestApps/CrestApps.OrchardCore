using CrestApps.OrchardCore.ContactCenter.Recipes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the recipe steps that import the routing configuration owned by the queues feature.
/// </summary>
[Feature(ContactCenterConstants.Feature.Queues)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class QueuesRecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<ContactCenterSkillStep>();
        services.AddRecipeExecutionStep<ContactCenterAgentEntitlementStep>();
        services.AddRecipeExecutionStep<ContactCenterQueueGroupStep>();
        services.AddRecipeExecutionStep<ContactCenterBusinessHoursCalendarStep>();
        services.AddRecipeExecutionStep<ContactCenterQueueStep>();
    }
}
