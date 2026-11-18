using System.Text.Json;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.FileSources.AI;

/// <summary>
/// Deletes a file source, as the Delete action of the File Sources page does. The file source catalog then forgets
/// what the source ingested and queues a full synchronization of its data source as the knowledge-base cleanup.
/// </summary>
public sealed class DeleteAIFileSourceTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteAIFileSource";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the file source to delete, from listAIFileSources."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes a file source and queues the removal of everything it ingested from the knowledge base, through a full synchronization of its data source. It cannot be undone; the stored credentials are deleted with it.";

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

        if (!await AIModuleTools.IsAuthorizedAsync(services, FileSourcePermissions.ManageFileSources))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var id = AIModuleTools.ReadString(arguments, "id");

        if (id is null)
        {
            return "Nothing was deleted: give the ID of the file source in id.";
        }

        var manager = services.GetRequiredService<ISourceCatalogManager<FileSource>>();
        var fileSource = await manager.FindByIdAsync(id, cancellationToken);

        if (fileSource is null)
        {
            return $"Nothing was deleted: no file source has the ID '{id}'. Call listAIFileSources to find it.";
        }

        if (!await manager.DeleteAsync(fileSource, cancellationToken))
        {
            return $"Unable to remove the file source '{fileSource.DisplayText}'.";
        }

        var dataSource = string.IsNullOrEmpty(fileSource.AIDataSourceId)
            ? null
            : await services.GetRequiredService<ISourceCatalogManager<AIDataSource>>().FindByIdAsync(fileSource.AIDataSourceId, cancellationToken);

        return AIModuleTools.Json(new
        {
            deleted = new
            {
                id = fileSource.ItemId,
                displayText = fileSource.DisplayText,
                connector = fileSource.Source,
            },
            cleanup = dataSource is null
                ? "The file source was deleted. Its data source was not found, so no knowledge-base cleanup was queued."
                : $"Knowledge-base cleanup has been queued: a full synchronization of the data source '{dataSource.DisplayText}' runs in the background and has not run yet.",
        });
    }
}
