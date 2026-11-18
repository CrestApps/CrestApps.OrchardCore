using System.Text.Json;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Agent.AIModels;

/// <summary>
/// Deletes AI provider connections, as the Delete action and bulk Delete of the Provider Connections page do, and
/// reloads the site. Connections from configuration and the connection the running assistant talks through are
/// skipped; the result names the deployments that stop working.
/// </summary>
public sealed class DeleteAIProviderConnectionTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteAIProviderConnection";

    private const int MaxConnections = 50;

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
          "description": "The IDs of the connections to delete, from listAIProviderConnections."
        }
      },
      "required": ["connectionIds"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes provider connections by id and reloads the site, which can end the current chat. Deployments that used a deleted connection stop working; the result names them. Connections from configuration and the connection the running assistant uses are skipped.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageProviderConnections))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!AgentToolArguments.TryReadStrings(arguments, "connectionIds", out var connectionIds) || connectionIds is not { Count: > 0 })
        {
            return "Nothing was deleted: give the IDs of the connections to delete in connectionIds.";
        }

        if (connectionIds.Count > MaxConnections)
        {
            return $"Nothing was deleted: at most {MaxConnections} connections can be deleted at once.";
        }

        var manager = services.GetRequiredService<INamedSourceCatalogManager<AIProviderConnection>>();
        var deployments = (await services.GetRequiredService<INamedSourceCatalogManager<AIDeployment>>().GetAllAsync(cancellationToken)).ToList();
        var protectedModels = await AIConfigurationGuard.GetProtectedModelsAsync(services, cancellationToken);
        var deleted = new List<object>();
        var skipped = new List<object>();

        foreach (var connectionId in connectionIds)
        {
            var connection = await manager.FindByIdAsync(connectionId, cancellationToken);

            if (connection is null)
            {
                skipped.Add(new { id = connectionId, reason = "No provider connection has this ID." });

                continue;
            }

            var title = connection.GetDisplayName();

            if (connection.IsReadOnly)
            {
                skipped.Add(new { id = connection.ItemId, title, reason = "The connection is defined in configuration (appsettings) and cannot be deleted here." });

                continue;
            }

            if (protectedModels.IsProtected(connection))
            {
                skipped.Add(new { id = connection.ItemId, title, reason = AIModelToolSupport.RunningAssistantRefusal($"the connection '{title}'", AIModelToolSupport.GetConnectionEditPath(services, connection.ItemId)) });

                continue;
            }

            if (!await manager.DeleteAsync(connection, cancellationToken))
            {
                skipped.Add(new { id = connection.ItemId, title, reason = "The connection manager did not delete it." });

                continue;
            }

            deleted.Add(new
            {
                id = connection.ItemId,
                name = connection.Name,
                title,
                provider = connection.Source,
                deploymentsThatStopWorking = deployments
                    .Where(deployment => AIModelToolSupport.UsesConnection(deployment, connection))
                    .OrderBy(deployment => deployment.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(deployment => deployment.Name)
                    .ToList(),
            });
        }

        if (deleted.Count > 0)
        {
            services.GetRequiredService<IShellReleaseManager>().RequestRelease();
        }

        return AgentToolResults.Json(new
        {
            deleted,
            skipped,
            siteReloads = deleted.Count > 0,
            note = deleted.Count > 0 ? SaveAIProviderConnectionTool.ReloadNote : null,
        });
    }
}
