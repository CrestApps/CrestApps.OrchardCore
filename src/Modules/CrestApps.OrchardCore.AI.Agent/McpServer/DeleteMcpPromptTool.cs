using System.Text.Json;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Mcp.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.McpServer;

/// <summary>
/// Deletes prompts of the site's MCP server, as the Delete action and bulk Delete of the MCP Prompts page do.
/// </summary>
public sealed class DeleteMcpPromptTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteMcpPrompt";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "promptIds": {
          "type": "array",
          "items": { "type": "string" },
          "minItems": 1,
          "maxItems": 50,
          "description": "The IDs of the prompts to delete, from listMcpPrompts."
        }
      },
      "required": ["promptIds"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes MCP server prompts by id. MCP clients can no longer ask for them.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, McpPermissions.ManageMcpPrompts))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!AgentToolArguments.TryReadStrings(arguments, "promptIds", out var promptIds) || promptIds is not { Count: > 0 })
        {
            return "Nothing was deleted: give the IDs of the prompts to delete in promptIds.";
        }

        if (promptIds.Count > McpServerToolSupport.MaxEntries)
        {
            return $"Nothing was deleted: at most {McpServerToolSupport.MaxEntries} prompts can be deleted at once.";
        }

        var manager = services.GetRequiredService<INamedCatalogManager<McpPrompt>>();
        var deleted = new List<object>();
        var skipped = new List<object>();

        foreach (var promptId in promptIds)
        {
            var prompt = await manager.FindByIdAsync(promptId, cancellationToken);

            if (prompt is null)
            {
                skipped.Add(new { id = promptId, reason = "No MCP prompt has this ID." });

                continue;
            }

            if (!await manager.DeleteAsync(prompt, cancellationToken))
            {
                skipped.Add(new { id = prompt.ItemId, name = prompt.Name, reason = "The prompt manager did not delete it." });

                continue;
            }

            deleted.Add(new { id = prompt.ItemId, name = prompt.Name });
        }

        return AgentToolResults.Json(new { deleted, skipped });
    }
}
