using CrestApps.OrchardCore.Recipes.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Recipes;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<RecipeExecutionService>();
        services.AddScoped<RecipeStepsService>();
    }
}
