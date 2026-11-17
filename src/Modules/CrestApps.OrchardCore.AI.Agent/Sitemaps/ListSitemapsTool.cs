using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Sitemaps;
using OrchardCore.Sitemaps.Cache;
using OrchardCore.Sitemaps.Services;

namespace CrestApps.OrchardCore.AI.Agent.Sitemaps;

/// <summary>
/// Lists the sitemaps and sitemap indexes of the site with their sources, and the cached sitemap files.
/// </summary>
public sealed class ListSitemapsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listSitemaps";

    private const int MaxCachedFiles = 100;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "Text the name of the sitemap must contain. Leave it out to list every sitemap and sitemap index."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of sitemaps on a page. The default is 20 and the most is 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the sitemaps and sitemap indexes with their path, enabled state and sources (content types with frequency and priority, custom paths), and the cached sitemap files. Use it to see what search engines are given. For a sitemap index it does not list the sitemaps the index contains.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, SitemapsPermissions.ManageSitemaps))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var sitemaps = await services.GetRequiredService<ISitemapManager>().GetSitemapsAsync();

        if (arguments.TryGetFirstString("search", out var search))
        {
            sitemaps = sitemaps.Where(sitemap => sitemap.Name?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) == true);
        }

        var matches = sitemaps
            .OrderBy(sitemap => sitemap.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);
        var cachedFiles = (await services.GetRequiredService<ISitemapCacheProvider>().ListAsync()).ToList();

        return AgentToolResults.Json(new
        {
            items = matches
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(SitemapContent.Describe),
            total = matches.Count,
            page,
            pageSize,
            cachedFiles = cachedFiles.Take(MaxCachedFiles),
            cachedFilesTotal = cachedFiles.Count,
            truncated = cachedFiles.Count > MaxCachedFiles
                ? true
                : (bool?)null,
        });
    }
}
