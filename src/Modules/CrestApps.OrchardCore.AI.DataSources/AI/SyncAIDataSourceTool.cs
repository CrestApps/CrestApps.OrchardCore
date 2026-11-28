using System.Text.Json;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.DataSources.AI;

/// <summary>
/// Queues a full synchronization of one data source into its AI knowledge base index, as the Sync index action of the
/// Data Sources page does.
/// </summary>
public sealed class SyncAIDataSourceTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "syncAIDataSource";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the data source to synchronize, from listAIDataSources."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Queues a full synchronization of one data source into its AI knowledge base index, which re-reads the source and generates embeddings in the background. Use it when answers are stale; it costs embedding tokens in proportion to the size of the source. A Web data source re-crawls its websites.";

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

        if (!await AIModuleTools.IsAuthorizedAsync(services, AIPermissions.ManageAIDataSources))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var id = AIModuleTools.ReadString(arguments, "id");

        if (id is null)
        {
            return "Nothing was queued: give the ID of the data source in id.";
        }

        var dataSource = await services.GetRequiredService<ISourceCatalogManager<AIDataSource>>().FindByIdAsync(id, cancellationToken);

        if (dataSource is null)
        {
            return $"Nothing was queued: no data source has the ID '{id}'. Call listAIDataSources to find it.";
        }

        await services.GetRequiredService<IAIDataSourceIndexingQueue>().QueueSyncDataSourceAsync(dataSource, cancellationToken);

        return AIModuleTools.Json(new
        {
            dataSource = new
            {
                id = dataSource.ItemId,
                displayText = dataSource.DisplayText,
                aiKnowledgeBaseIndexProfileName = dataSource.AIKnowledgeBaseIndexProfileName,
            },
            queued = true,
            message = "The data source index synchronization was queued. It runs in the background, re-reads the whole source and generates embeddings; it has not finished yet, so answers change only once it has.",
        });
    }
}
