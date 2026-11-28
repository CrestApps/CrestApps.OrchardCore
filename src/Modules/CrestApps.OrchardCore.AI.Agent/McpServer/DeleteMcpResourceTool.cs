using System.Text.Json;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Mcp.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.McpServer;

/// <summary>
/// Deletes resources of the site's MCP server, as the Delete action and bulk Delete of the MCP Resources page do.
/// </summary>
public sealed class DeleteMcpResourceTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteMcpResource";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "resourceIds": {
          "type": "array",
          "items": { "type": "string" },
          "minItems": 1,
          "maxItems": 50,
          "description": "The IDs of the resources to delete, from listMcpResources."
        }
      },
      "required": ["resourceIds"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes MCP server resources by id. MCP clients can no longer read them.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, McpPermissions.ManageMcpResources))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!AgentToolArguments.TryReadStrings(arguments, "resourceIds", out var resourceIds) || resourceIds is not { Count: > 0 })
        {
            return "Nothing was deleted: give the IDs of the resources to delete in resourceIds.";
        }

        if (resourceIds.Count > McpServerToolSupport.MaxEntries)
        {
            return $"Nothing was deleted: at most {McpServerToolSupport.MaxEntries} resources can be deleted at once.";
        }

        var manager = services.GetRequiredService<ISourceCatalogManager<McpResource>>();
        var deleted = new List<object>();
        var skipped = new List<object>();

        foreach (var resourceId in resourceIds)
        {
            var resource = await manager.FindByIdAsync(resourceId, cancellationToken);

            if (resource is null)
            {
                skipped.Add(new { id = resourceId, reason = "No MCP resource has this ID." });

                continue;
            }

            if (!await manager.DeleteAsync(resource, cancellationToken))
            {
                skipped.Add(new { id = resource.ItemId, title = resource.DisplayText, reason = "The resource manager did not delete it." });

                continue;
            }

            deleted.Add(new { id = resource.ItemId, title = resource.DisplayText, type = resource.Source, uri = resource.Resource?.Uri });
        }

        return AgentToolResults.Json(new { deleted, skipped });
    }
}
