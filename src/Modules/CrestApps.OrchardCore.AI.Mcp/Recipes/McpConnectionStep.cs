using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Mcp.Recipes;

internal sealed class McpConnectionStep : NamedRecipeStepHandler
{
    public const string StepKey = "McpConnection";

    private readonly ISourceCatalogManager<McpConnection> _manager;
    private readonly McpClientAIOptions _mcpClientOptions;

    internal readonly IStringLocalizer S;

    public McpConnectionStep(
        ISourceCatalogManager<McpConnection> manager,
        IOptions<McpClientAIOptions> mcpClientOptions,
        IStringLocalizer<McpConnectionStep> stringLocalizer)
         : base(StepKey)
    {
        _manager = manager;
        _mcpClientOptions = mcpClientOptions.Value;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<McpConnectionDeploymentStepModel>();
        var tokens = model.Connections.Cast<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            McpConnection connection = null;

            var id = token[nameof(McpConnection.ItemId)]?.GetValue<string>();

            var hasId = !string.IsNullOrEmpty(id);

            if (hasId)
            {
                connection = await _manager.FindByIdAsync(id);
            }

            var sourceName = token[nameof(McpConnection.Source)]?.GetValue<string>();
            var hasSource = !string.IsNullOrEmpty(sourceName);

            if (connection is not null)
            {
                await _manager.UpdateAsync(connection, token);
            }
            else
            {
                if (!hasSource)
                {
                    context.Errors.Add(S["Could not find provider name. The deployment will not be imported."]);
                    continue;
                }

                if (!_mcpClientOptions.TransportTypes.TryGetValue(sourceName, out _))
                {
                    context.Errors.Add(S["Invalid source used for MCP connection '{0}'.", sourceName]);

                    return;
                }

                connection = await _manager.NewAsync(sourceName, token);

                if (hasId && IdValidator.IsValid(id))
                {
                    connection.ItemId = id;
                }
            }

            var validationResult = await _manager.ValidateAsync(connection);

            if (!validationResult.Succeeded)
            {
                foreach (var error in validationResult.Errors)
                {
                    context.Errors.Add(error.ErrorMessage);
                }

                continue;
            }

            await _manager.CreateAsync(connection);
        }
    }

    private sealed class McpConnectionDeploymentStepModel
    {
        public JsonArray Connections { get; set; }
    }
}
