using System.Text.Json.Nodes;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Recipes;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.AI.Mcp.Recipes;

/// <summary>
/// Imports MCP client connections, matched by their item id.
/// </summary>
internal sealed class McpConnectionStep : CatalogRecipeStepBase<McpConnection>
{
    public const string StepKey = "McpConnection";

    private readonly ISourceCatalogManager<McpConnection> _manager;
    private readonly McpClientAIOptions _mcpClientOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpConnectionStep"/> class.
    /// </summary>
    /// <param name="manager">The manager.</param>
    /// <param name="mcpClientOptions">The mcp client options.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public McpConnectionStep(
        ISourceCatalogManager<McpConnection> manager,
        IOptions<McpClientAIOptions> mcpClientOptions,
        IStringLocalizer<McpConnectionStep> stringLocalizer)
        : base(StepKey, "Connections")
    {
        _manager = manager;
        _mcpClientOptions = mcpClientOptions.Value;
        S = stringLocalizer;
    }

    protected override ValueTask<McpConnection> FindByIdAsync(string id)
        => _manager.FindByIdAsync(id);

    protected override ValueTask UpdateAsync(McpConnection entry, JsonObject token)
        => _manager.UpdateAsync(entry, token);

    protected override async ValueTask<McpConnection> NewAsync(JsonObject token, RecipeExecutionContext context)
    {
        var source = token[nameof(McpConnection.Source)]?.GetValue<string>();

        if (string.IsNullOrEmpty(source))
        {
            context.Errors.Add(S["Could not find provider name. The deployment will not be imported."]);

            return null;
        }

        if (!_mcpClientOptions.TransportTypes.TryGetValue(source, out _))
        {
            context.Errors.Add(S["Invalid source used for MCP connection '{0}'.", source]);

            return null;
        }

        return await _manager.NewAsync(source, token);
    }

    protected override ValueTask<ValidationResultDetails> ValidateAsync(McpConnection entry)
        => _manager.ValidateAsync(entry);

    protected override ValueTask CreateAsync(McpConnection entry)
        => _manager.CreateAsync(entry);
}
