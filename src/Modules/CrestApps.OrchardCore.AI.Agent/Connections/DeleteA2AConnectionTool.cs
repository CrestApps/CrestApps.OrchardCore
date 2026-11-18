using System.Text.Json;
using CrestApps.Core.AI.A2A.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.Connections;

/// <summary>
/// Deletes A2A connections, as the Delete action and bulk Delete of the Agent to Agent Hosts page do. A connection
/// the running assistant uses is skipped; the result names the profiles that lose those remote agents.
/// </summary>
public sealed class DeleteA2AConnectionTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteA2AConnection";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "connectionIds": {
          "type": "array",
          "items": { "type": "string" },
          "minItems": 1,
          "maxItems": 50,
          "description": "The IDs of the A2A connections to delete, from listA2AConnections."
        }
      },
      "required": ["connectionIds"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes A2A connections by id. Profiles that used them lose those remote agents; the result names them. A connection the assistant running this chat uses is skipped.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIModulePermissions.ManageA2AConnections))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!AgentToolArguments.TryReadStrings(arguments, "connectionIds", out var connectionIds) || connectionIds is not { Count: > 0 })
        {
            return "Nothing was deleted: give the IDs of the connections to delete in connectionIds.";
        }

        if (connectionIds.Count > ConnectionToolSupport.MaxConnections)
        {
            return $"Nothing was deleted: at most {ConnectionToolSupport.MaxConnections} connections can be deleted at once.";
        }

        var manager = services.GetRequiredService<ICatalogManager<A2AConnection>>();
        var protectedConfiguration = await AIConfigurationGuard.GetProtectedAsync(services, cancellationToken);
        var users = await ConnectionToolSupport.GetA2AUsersAsync(services.GetRequiredService<IAIProfileManager>(), cancellationToken);
        var deleted = new List<object>();
        var skipped = new List<object>();

        foreach (var connectionId in connectionIds)
        {
            var connection = await manager.FindByIdAsync(connectionId, cancellationToken);

            if (connection is null)
            {
                skipped.Add(new { id = connectionId, reason = "No A2A connection has this ID." });

                continue;
            }

            if (protectedConfiguration.IsProtectedA2AConnection(connection.ItemId))
            {
                skipped.Add(new
                {
                    id = connection.ItemId,
                    title = connection.DisplayText,
                    reason = ConnectionToolSupport.RunningAssistantRefusal($"the A2A connection '{connection.DisplayText}'", ConnectionToolSupport.GetA2AConnectionEditPath(services, connection.ItemId)),
                });

                continue;
            }

            if (!await manager.DeleteAsync(connection, cancellationToken))
            {
                skipped.Add(new { id = connection.ItemId, title = connection.DisplayText, reason = "The connection manager did not delete it." });

                continue;
            }

            deleted.Add(new
            {
                id = connection.ItemId,
                title = connection.DisplayText,
                endpoint = connection.Endpoint,
                profilesThatLoseItsAgents = users.GetValueOrDefault(connection.ItemId) ?? [],
            });
        }

        return AgentToolResults.Json(new { deleted, skipped });
    }
}
