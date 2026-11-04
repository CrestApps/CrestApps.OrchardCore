using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Recipes.Schemas;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Mcp;

/// <summary>
/// Registers the JSON schemas of the MCP server prompt and resource recipe steps.
/// </summary>
[Feature(McpPermissions.Feature.Server)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class ServerRecipeSchemasStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IRecipeStep, McpPromptRecipeStep>();
        services.AddScoped<IRecipeStep, McpResourceRecipeStep>();
    }
}
