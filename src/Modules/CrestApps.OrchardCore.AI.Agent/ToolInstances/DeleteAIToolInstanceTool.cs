using System.Text.Json;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.ToolInstances;

/// <summary>
/// Deletes tool instances, as the Delete action and bulk Delete of the Tool Instances page do. Instances owned by
/// someone else are skipped unless the caller may manage those, and instances the running assistant uses are skipped.
/// </summary>
public sealed class DeleteAIToolInstanceTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteAIToolInstance";

    private const int MaxInstances = 50;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "instanceIds": {
          "type": "array",
          "items": { "type": "string" },
          "minItems": 1,
          "maxItems": 50,
          "description": "The IDs of the tool instances to delete, from listAIToolInstances."
        }
      },
      "required": ["instanceIds"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes tool instances by id. Instances owned by someone else are skipped unless the user may manage those, and instances the running assistant uses are skipped. Profiles that used a deleted instance lose that tool.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageAIToolInstances))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!AgentToolArguments.TryReadStrings(arguments, "instanceIds", out var instanceIds) || instanceIds is not { Count: > 0 })
        {
            return "Nothing was deleted: give the IDs of the tool instances to delete in instanceIds.";
        }

        if (instanceIds.Count > MaxInstances)
        {
            return $"Nothing was deleted: at most {MaxInstances} tool instances can be deleted at once.";
        }

        var manager = services.GetRequiredService<ISourceCatalogManager<AIToolInstance>>();
        var protectedConfiguration = await AIConfigurationGuard.GetProtectedAsync(services, cancellationToken);
        var deleted = new List<object>();
        var skipped = new List<object>();

        foreach (var instanceId in instanceIds)
        {
            var instance = await manager.FindByIdAsync(instanceId, cancellationToken);

            if (instance is null)
            {
                skipped.Add(new { id = instanceId, reason = "No tool instance has this ID." });

                continue;
            }

            if (!await ToolInstanceSettings.CanManageAsync(services, instance))
            {
                skipped.Add(new { id = instance.ItemId, name = instance.Name, reason = "It was created by another user, and you may not manage other people's tool instances." });

                continue;
            }

            if (protectedConfiguration.IsProtectedToolInstance(instance.Name))
            {
                skipped.Add(new { id = instance.ItemId, name = instance.Name, reason = AIProfileToolSupport.RunningAssistantRefusal($"the tool instance '{instance.Name}'", AIProfileToolSupport.GetToolInstanceEditPath(services, instance.ItemId)) });

                continue;
            }

            if (await manager.DeleteAsync(instance, cancellationToken))
            {
                deleted.Add(new { id = instance.ItemId, name = instance.Name, source = instance.Source });
            }
            else
            {
                skipped.Add(new { id = instance.ItemId, name = instance.Name, reason = "The catalog did not delete it." });
            }
        }

        return AgentToolResults.Json(new
        {
            deleted,
            skipped,
        });
    }
}
