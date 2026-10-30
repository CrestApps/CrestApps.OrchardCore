using CrestApps.OrchardCore.Calendars.Deployments;
using CrestApps.OrchardCore.Calendars.Recipes;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Calendars;

/// <summary>
/// Describes the Calendars recipe and deployment steps to CrestApps Recipes, so they can be checked and written with help.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.Recipes")]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, CalendarsRecipeStepSchema>();
        services.AddDeploymentStepSchema<CalendarsDeploymentStepSchema>();
    }
}
