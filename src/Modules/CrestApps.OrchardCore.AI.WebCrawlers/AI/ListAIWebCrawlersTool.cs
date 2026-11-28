using System.Text.Json;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.WebCrawlers.AI;

/// <summary>
/// Lists the web crawlers, as the Web Crawlers page does, or returns one by ID.
/// </summary>
public sealed class ListAIWebCrawlersTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listAIWebCrawlers";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of one crawler to return. Leave it out to list crawlers."
        },
        "search": {
          "type": "string",
          "description": "Text the crawler's name contains."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, from 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "How many crawlers a page holds, at most 50. Defaults to 20."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the web crawlers that feed Web AI data sources, with site, sitemap, limits, URL patterns, data source, enabled state and schedule, with optional name search or one crawler by id. Also returns the crawl strategies enabled on this site.";

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

        if (id is not null)
        {
            var crawler = await WebCrawlerToolSupport.FindAsync(services, id, cancellationToken);

            if (crawler is null)
            {
                return $"No web crawler has the ID '{id}'. Call {TheName} without an id to see the crawlers.";
            }

            return AIModuleTools.Json(await WebCrawlerToolSupport.DescribeAsync(services, crawler, cancellationToken));
        }

        var strategies = WebCrawlerToolSupport.GetStrategies(services);
        var manager = services.GetRequiredService<ISourceCatalogManager<WebCrawler>>();
        var search = AIModuleTools.ReadString(arguments, "search");
        var (page, pageSize) = AIModuleTools.ReadPaging(arguments);
        var crawlers = new List<WebCrawler>();

        // The same records the Web Crawlers page lists: those of a crawl strategy enabled on this site.
        foreach (var strategy in strategies)
        {
            crawlers.AddRange(await manager.GetAsync(strategy.Strategy, cancellationToken));
        }

        var matches = crawlers
            .Where(crawler => search is null || crawler.DisplayText?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
            .OrderBy(crawler => crawler.DisplayText, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var items = new List<object>();

        foreach (var crawler in matches.Skip((page - 1) * pageSize).Take(pageSize))
        {
            items.Add(await WebCrawlerToolSupport.DescribeAsync(services, crawler, cancellationToken));
        }

        return AIModuleTools.Json(new
        {
            items,
            total = matches.Count,
            page,
            pageSize,
            truncated = page * pageSize < matches.Count,
            strategies = strategies.Select(strategy => new
            {
                name = strategy.Strategy,
                displayName = strategy.DisplayName.Value,
                description = strategy.Description.Value,
            }),
        });
    }
}
