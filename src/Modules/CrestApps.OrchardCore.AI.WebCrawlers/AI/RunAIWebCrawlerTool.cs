using System.Text.Json;
using CrestApps.Core.AI.WebCrawlers;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.WebCrawlers.AI;

/// <summary>
/// Re-crawls one website now, as the Sync action of the Web Crawlers page does: the site's sitemap is read during the
/// call, and the new, changed and removed pages are queued for indexing in the background.
/// </summary>
public sealed class RunAIWebCrawlerTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "runAIWebCrawler";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the crawler to run, from listAIWebCrawlers."
        }
      },
      "required": ["id"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Re-crawls one website now and queues its new, changed and removed pages for indexing, returning the three counts. It sends requests to the website and spends embedding tokens for every changed page; the indexing runs in the background after the call.";

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
            return "Nothing was run: give the ID of the crawler in id.";
        }

        var crawler = await WebCrawlerToolSupport.FindAsync(services, id, cancellationToken);

        if (crawler is null)
        {
            return $"Nothing was run: no web crawler has the ID '{id}'. Call listAIWebCrawlers to find it.";
        }

        var result = await services.GetRequiredService<IWebCrawlerReindexPlanner>().PlanAndEnqueueAsync(crawler, cancellationToken);

        if (result.Status is WebCrawlerReindexStatus.DiscoveryFailed or WebCrawlerReindexStatus.NoPagesDiscovered)
        {
            return $"The website of '{crawler.DisplayText}' was not crawled: {result.Message} Nothing was queued for indexing.";
        }

        return AIModuleTools.Json(new
        {
            crawler = new
            {
                id = crawler.ItemId,
                displayText = crawler.DisplayText,
            },
            status = result.Status.ToString(),
            discovered = result.DiscoveredCount,
            newPages = result.NewCount,
            changedPages = result.ChangedCount,
            removedPages = result.RemovedCount,
            unchangedPages = result.UnchangedCount,
            message = result.Message,
            indexing = result.NewCount + result.ChangedCount + result.RemovedCount == 0
                ? "Nothing changed, so nothing was queued for indexing."
                : "The new, changed and removed pages were queued for indexing. Indexing runs in the background and has not finished yet.",
        });
    }
}
