using System.Text.Json;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.WebCrawlers.AI;

/// <summary>
/// Deletes a web crawler, as the Delete action of the Web Crawlers page does. The crawler catalog then forgets the
/// crawled pages and queues a full synchronization of the data source.
/// </summary>
public sealed class DeleteAIWebCrawlerTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteAIWebCrawler";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the crawler to delete, from listAIWebCrawlers."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes a web crawler. Its pages stop being refreshed in the knowledge base, and a full synchronization of its data source is queued as the knowledge-base cleanup; this cannot be undone.";

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

        if (!await AIModuleTools.IsAuthorizedAsync(services, WebCrawlerPermissions.ManageWebCrawlers))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var id = AIModuleTools.ReadString(arguments, "id");

        if (id is null)
        {
            return "Nothing was deleted: give the ID of the crawler in id.";
        }

        var crawler = await WebCrawlerToolSupport.FindAsync(services, id, cancellationToken);

        if (crawler is null)
        {
            return $"Nothing was deleted: no web crawler has the ID '{id}'. Call listAIWebCrawlers to find it.";
        }

        if (!await services.GetRequiredService<ISourceCatalogManager<WebCrawler>>().DeleteAsync(crawler, cancellationToken))
        {
            return $"The web crawler '{crawler.DisplayText}' could not be deleted.";
        }

        var dataSource = string.IsNullOrEmpty(crawler.AIDataSourceId)
            ? null
            : await services.GetRequiredService<ISourceCatalogManager<AIDataSource>>().FindByIdAsync(crawler.AIDataSourceId, cancellationToken);

        return AIModuleTools.Json(new
        {
            deleted = new
            {
                id = crawler.ItemId,
                displayText = crawler.DisplayText,
            },
            synchronization = dataSource is null
                ? null
                : $"A full synchronization of the data source '{dataSource.DisplayText}' was queued as the knowledge-base cleanup. It runs in the background and has not run yet.",
        });
    }
}
