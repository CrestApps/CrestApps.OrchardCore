using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CrestApps.Core;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.WebCrawlers;
using CrestApps.Core.AI.WebCrawlers.Strategies.Sitemap;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.WebCrawlers.AI;

/// <summary>
/// Creates a web crawler, or updates one by ID, with the checks of the crawler editor and the crawler catalog. Saving a
/// crawler queues a full synchronization of its data source, which re-crawls the site and generates embeddings, so the
/// tool asks for approval.
/// </summary>
public sealed class SaveAIWebCrawlerTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveAIWebCrawler";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the crawler to update, from listAIWebCrawlers. Leave it out to create a crawler."
        },
        "strategy": {
          "type": "string",
          "description": "When creating, the crawl strategy, from the strategies listAIWebCrawlers returns, such as Sitemap. Defaults to the only strategy when there is one. A crawler's strategy cannot change."
        },
        "displayText": {
          "type": "string",
          "description": "The crawler's name. Required when creating."
        },
        "aiDataSourceId": {
          "type": "string",
          "description": "The ID of the Web AI data source the pages go to, from listAIDataSources. Required when creating."
        },
        "enabled": {
          "type": "boolean",
          "description": "Whether the crawler is re-crawled on its schedule. New crawlers are enabled."
        },
        "reindexIntervalMinutes": {
          "type": "integer",
          "minimum": 1,
          "description": "How often the site is re-crawled, in minutes. Leave it out to keep the current value; a new crawler uses the site default."
        },
        "sitemap": {
          "type": "object",
          "description": "The settings of the Sitemap strategy. Only the given members change.",
          "properties": {
            "baseUrl": { "type": "string", "description": "The address of the website, for example https://docs.example.com. A base URL or a sitemap URL is required." },
            "sitemapUrl": { "type": "string", "description": "The address of the sitemap, when it is not found from the base URL." },
            "maxPages": { "type": "integer", "minimum": 1, "description": "The most pages one crawl reads." },
            "maxConcurrentRequests": { "type": "integer", "minimum": 1, "description": "How many pages are fetched at once." },
            "requestTimeoutSeconds": { "type": "integer", "minimum": 1, "description": "How long one page may take, in seconds." },
            "userAgent": { "type": "string", "description": "The user agent sent to the website." },
            "includeUrlPatterns": { "type": "array", "items": { "type": "string" }, "description": "Regular expressions; when given, only page addresses that match one are crawled. An empty list removes them." },
            "excludeUrlPatterns": { "type": "array", "items": { "type": "string" }, "description": "Regular expressions; page addresses that match one are skipped. An empty list removes them." }
          },
          "additionalProperties": false
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a web crawler for a website, or updates one by id: name, Web AI data source, enabled state, schedule and sitemap settings. Saving queues a full synchronization of the data source, which crawls the site and spends embedding tokens in the background. The addresses are checked as the crawler editor checks them (absolute http or https addresses); the server fetches whatever address is given, so confirm it with the user.";

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

        var secret = AIModuleTools.FindSecretArgument(arguments);

        if (secret is not null)
        {
            return $"Nothing was saved: '{secret}' looks like a secret, and tools never take secrets. A person enters it on the crawler's page: {WebCrawlerToolSupport.GetIndexPath(services)}";
        }

        var manager = services.GetRequiredService<ISourceCatalogManager<WebCrawler>>();
        var id = AIModuleTools.ReadString(arguments, "id");
        WebCrawler existing = null;
        WebCrawler working;

        if (id is not null)
        {
            existing = await WebCrawlerToolSupport.FindAsync(services, id, cancellationToken);

            if (existing is null)
            {
                return $"Nothing was saved: no web crawler has the ID '{id}'. Call listAIWebCrawlers to find it.";
            }

            working = existing.Clone();
        }
        else
        {
            var strategies = WebCrawlerToolSupport.GetStrategies(services);
            var strategyName = AIModuleTools.ReadString(arguments, "strategy") ?? (strategies.Count == 1 ? strategies[0].Strategy : null);
            var strategy = strategies.FirstOrDefault(entry => string.Equals(entry.Strategy, strategyName, StringComparison.OrdinalIgnoreCase));

            if (strategy is null)
            {
                return $"Nothing was saved: name the crawl strategy in strategy, one of {string.Join(", ", strategies.Select(entry => entry.Strategy))}.";
            }

            working = await manager.NewAsync(strategy.Strategy, cancellationToken: cancellationToken);
        }

        if (!AIModuleTools.TryReadObject(arguments, "sitemap", out var sitemap))
        {
            return "Nothing was saved: sitemap must be an object with the sitemap settings.";
        }

        var errors = new List<string>();
        var changes = ReadChanges(arguments, working, sitemap, errors);

        if (errors.Count > 0)
        {
            return "Nothing was saved: " + string.Join(" ", errors);
        }

        changes(working);

        var validation = await manager.ValidateAsync(working, cancellationToken);

        if (!validation.Succeeded)
        {
            return "Nothing was saved: " + string.Join(" ", validation.Errors.Select(error => error.ErrorMessage));
        }

        if (existing is null)
        {
            await manager.CreateAsync(working, cancellationToken);
            existing = working;
        }
        else
        {
            changes(existing);
            await manager.UpdateAsync(existing, cancellationToken: cancellationToken);
        }

        var dataSource = await services.GetRequiredService<ISourceCatalogManager<AIDataSource>>().FindByIdAsync(existing.AIDataSourceId, cancellationToken);

        return AIModuleTools.Json(new
        {
            saved = id is null ? "created" : "updated",
            crawler = await WebCrawlerToolSupport.DescribeAsync(services, existing, cancellationToken),
            synchronization = dataSource is null
                ? null
                : $"A full synchronization of the data source '{dataSource.DisplayText}' was queued. It runs in the background: it re-crawls every enabled crawler of that data source and generates embeddings, which costs embedding tokens. It has not run yet.",
        });
    }

    private static Action<WebCrawler> ReadChanges(AIFunctionArguments arguments, WebCrawler current, JsonObject sitemap, List<string> errors)
    {
        var displayText = AIModuleTools.ReadString(arguments, "displayText");
        var dataSourceId = AIModuleTools.ReadString(arguments, "aiDataSourceId");
        bool? enabled = arguments.TryGetValue("enabled", out var rawEnabled) && rawEnabled is not null
            ? arguments.GetFirstValueOrDefault<bool?>("enabled", null)
            : null;
        int? interval = null;

        if (arguments.TryGetValue("reindexIntervalMinutes", out var rawInterval) && rawInterval is not null)
        {
            interval = arguments.GetFirstValueOrDefault<int?>("reindexIntervalMinutes", null);

            // The editor's check: a re-index interval is a positive number of minutes.
            if (interval is null or < 1)
            {
                errors.Add("The re-index interval must be a positive number of minutes.");
            }
        }

        if (string.IsNullOrWhiteSpace(displayText ?? current.DisplayText))
        {
            errors.Add("The name is required: give it in displayText.");
        }

        if (string.IsNullOrWhiteSpace(dataSourceId ?? current.AIDataSourceId))
        {
            errors.Add("A target Web data source is required: give its ID in aiDataSourceId.");
        }

        SitemapWebCrawlerMetadata metadata = null;

        if (string.Equals(current.Source, WebCrawlerConstants.Strategies.Sitemap, StringComparison.OrdinalIgnoreCase))
        {
            metadata = ReadSitemap(current, sitemap, errors);
        }
        else if (sitemap is not null)
        {
            errors.Add($"The crawler uses the '{current.Source}' strategy, which has no sitemap settings.");
        }

        return crawler =>
        {
            if (displayText is not null)
            {
                crawler.DisplayText = displayText;
            }

            if (dataSourceId is not null)
            {
                crawler.AIDataSourceId = dataSourceId;
            }

            if (enabled is not null)
            {
                crawler.Enabled = enabled.Value;
            }

            if (interval is not null)
            {
                crawler.ReindexIntervalMinutes = interval;
            }

            if (metadata is not null)
            {
                crawler.Put(Copy(metadata));
            }
        };
    }

    private static SitemapWebCrawlerMetadata ReadSitemap(WebCrawler current, JsonObject sitemap, List<string> errors)
    {
        var metadata = current.TryGet<SitemapWebCrawlerMetadata>(out var stored) ? Copy(stored) : new SitemapWebCrawlerMetadata();

        if (sitemap is not null)
        {
            if (AIModuleTools.TryReadText(sitemap, "baseUrl", out var baseUrl))
            {
                metadata.BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.Trim();
            }

            if (AIModuleTools.TryReadText(sitemap, "sitemapUrl", out var sitemapUrl))
            {
                metadata.SitemapUrl = string.IsNullOrWhiteSpace(sitemapUrl) ? null : sitemapUrl.Trim();
            }

            if (AIModuleTools.TryReadText(sitemap, "userAgent", out var userAgent))
            {
                metadata.UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent.Trim();
            }

            ReadNumber(sitemap, "maxPages", value => metadata.MaxPages = value, errors);
            ReadNumber(sitemap, "maxConcurrentRequests", value => metadata.MaxConcurrentRequests = value, errors);
            ReadNumber(sitemap, "requestTimeoutSeconds", value => metadata.RequestTimeoutSeconds = value, errors);
            ReadPatterns(sitemap, "includeUrlPatterns", value => metadata.IncludeUrlPatterns = value, errors);
            ReadPatterns(sitemap, "excludeUrlPatterns", value => metadata.ExcludeUrlPatterns = value, errors);
        }

        // The sitemap editor's checks. The crawl strategy also asks for http or https when the crawler is validated.
        if (string.IsNullOrWhiteSpace(metadata.BaseUrl) && string.IsNullOrWhiteSpace(metadata.SitemapUrl))
        {
            errors.Add("Provide a base URL or an explicit sitemap URL.");
        }

        if (!string.IsNullOrWhiteSpace(metadata.BaseUrl) && !Uri.TryCreate(metadata.BaseUrl, UriKind.Absolute, out _))
        {
            errors.Add("The base URL must be an absolute URL, for example https://example.com.");
        }

        if (!string.IsNullOrWhiteSpace(metadata.SitemapUrl) && !Uri.TryCreate(metadata.SitemapUrl, UriKind.Absolute, out _))
        {
            errors.Add("The sitemap URL must be an absolute URL, for example https://example.com/sitemap.xml.");
        }

        return metadata;
    }

    private static void ReadNumber(JsonObject json, string name, Action<int?> apply, List<string> errors)
    {
        if (!json.ContainsKey(name))
        {
            return;
        }

        if (!AIModuleTools.TryReadInt(json, name, out var value) || value is < 1)
        {
            errors.Add($"sitemap.{name} must be a positive whole number.");

            return;
        }

        apply(value);
    }

    private static void ReadPatterns(JsonObject json, string name, Action<List<string>> apply, List<string> errors)
    {
        if (!json.TryGetPropertyValue(name, out var node))
        {
            return;
        }

        if (node is null)
        {
            apply([]);

            return;
        }

        if (node is not JsonArray array)
        {
            errors.Add($"sitemap.{name} must be a list of regular expressions.");

            return;
        }

        var patterns = new List<string>();

        foreach (var item in array)
        {
            var pattern = item is JsonValue text && text.TryGetValue<string>(out var value) ? value?.Trim() : null;

            if (string.IsNullOrEmpty(pattern))
            {
                continue;
            }

            try
            {
                _ = Regex.Match(string.Empty, pattern);
                patterns.Add(pattern);
            }
            catch (ArgumentException)
            {
                errors.Add($"'{pattern}' is not a valid regular expression.");
            }
        }

        apply(patterns);
    }

    private static SitemapWebCrawlerMetadata Copy(SitemapWebCrawlerMetadata metadata)
        => new()
        {
            BaseUrl = metadata.BaseUrl,
            SitemapUrl = metadata.SitemapUrl,
            MaxPages = metadata.MaxPages,
            MaxConcurrentRequests = metadata.MaxConcurrentRequests,
            RequestTimeoutSeconds = metadata.RequestTimeoutSeconds,
            UserAgent = metadata.UserAgent,
            IncludeUrlPatterns = metadata.IncludeUrlPatterns is null ? null : [.. metadata.IncludeUrlPatterns],
            ExcludeUrlPatterns = metadata.ExcludeUrlPatterns is null ? null : [.. metadata.ExcludeUrlPatterns],
        };
}
