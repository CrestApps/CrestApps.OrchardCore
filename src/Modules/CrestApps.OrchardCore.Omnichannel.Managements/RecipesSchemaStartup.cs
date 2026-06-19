using CrestApps.OrchardCore.Omnichannel.Managements.Schemas;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements;

/// <summary>
/// Registers recipe schema contributors for the Omnichannel Management feature.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.Recipes")]
public sealed class RecipesSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, OmnichannelContactPartSchemaDefinition>();
    }
}
