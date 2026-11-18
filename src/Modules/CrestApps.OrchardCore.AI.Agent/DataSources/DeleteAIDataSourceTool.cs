using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Agent.Profiles;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.DataSources;

/// <summary>
/// Deletes an AI data source, as the Delete action of the Data Sources page does; the data source catalog then queues
/// the removal of its documents from the knowledge base index. The data source of the assistant running the turn
/// cannot be deleted.
/// </summary>
public sealed class DeleteAIDataSourceTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteAIDataSource";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the data source to delete, from listAIDataSources."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes an AI data source and queues the removal of its knowledge base documents. Profiles, web crawlers and file sources that point at it stop having knowledge to search; this cannot be undone. The data source the running assistant uses cannot be deleted.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, AIPermissions.ManageAIDataSources))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var id = AIModuleTools.ReadString(arguments, "id");

        if (id is null)
        {
            return "Nothing was deleted: give the ID of the data source in id.";
        }

        var manager = services.GetRequiredService<ISourceCatalogManager<AIDataSource>>();
        var dataSource = await manager.FindByIdAsync(id, cancellationToken);

        if (dataSource is null)
        {
            return $"Nothing was deleted: no data source has the ID '{id}'. Call listAIDataSources to find it.";
        }

        var protectedConfiguration = await AIConfigurationGuard.GetProtectedAsync(services, cancellationToken);

        if (protectedConfiguration.IsProtectedDataSource(dataSource.ItemId))
        {
            return AIProfileToolSupport.RunningAssistantRefusal($"the data source '{dataSource.DisplayText}'", AIDataSourceToolDescriptions.GetEditPath(services, dataSource.ItemId));
        }

        // The profiles that answer from it, so the reply can say who loses their knowledge.
        var profiles = (await services.GetRequiredService<IAIProfileManager>().GetAllAsync(cancellationToken))
            .Where(profile => profile.TryGet<DataSourceMetadata>(out var metadata) && string.Equals(metadata.DataSourceId, dataSource.ItemId, StringComparison.Ordinal))
            .Select(profile => AIConfigurationGuard.GetTitle(profile))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!await manager.DeleteAsync(dataSource, cancellationToken))
        {
            return $"The data source '{dataSource.DisplayText}' could not be deleted.";
        }

        return AgentToolResults.Json(new
        {
            deleted = new
            {
                id = dataSource.ItemId,
                displayText = dataSource.DisplayText,
                source = dataSource.Source,
            },
            profilesThatUsedIt = profiles,
            cleanup = "The removal of its documents from the AI knowledge base index was queued. It runs in the background and has not finished yet.",
        });
    }
}
