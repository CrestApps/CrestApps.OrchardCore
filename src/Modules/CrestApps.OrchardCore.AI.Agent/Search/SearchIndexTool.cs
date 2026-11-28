using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Agent.Indexing;
using CrestApps.OrchardCore.AI.Agent.Queries;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Indexing.Models;
using OrchardCore.Search.Abstractions;
using OrchardCore.Search.Models;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Agent.Search;

/// <summary>
/// Runs a plain-text search against an index profile with the search service of its provider, as the search page of
/// the site does.
/// </summary>
public sealed class SearchIndexTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "searchIndex";

    private const int MaxHighlightsPerField = 3;

    private const int MaxHighlightLength = 300;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "terms": {
          "type": "string",
          "description": "The text to search for, as a visitor types it in the search box."
        },
        "index": {
          "type": "string",
          "description": "The name of the index profile to search, from listIndexProfiles. Leave it out to search the default index of the site search settings."
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
          "description": "The number of results on a page. The default is 20 and the most is 50."
        }
      },
      "required": [
        "terms"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Runs a plain-text search against an index profile (the default site search index when none is named) the way the site's search page does, and returns the matching content items with highlights, paged. Use it to check what visitors find for a term or to verify an index after indexing.";

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

        // The permission is checked on the index, so the index is found first. Nothing is looked up for a caller who
        // is not signed in.
        if (AgentToolAuthorization.GetCaller(services) is null)
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("terms", out var terms) || string.IsNullOrWhiteSpace(terms))
        {
            return "The terms argument is required: the text to search for.";
        }

        var indexProfileManager = services.GetRequiredService<IIndexProfileManager>();
        IndexProfile indexProfile;

        if (arguments.TryGetFirstString("index", out var index))
        {
            indexProfile = await indexProfileManager.FindByNameAsync(index.Trim());

            if (indexProfile is null)
            {
                return $"The search was not run: there is no index profile named '{index}'. Use listIndexProfiles to get the names.";
            }
        }
        else
        {
            var site = await services.GetRequiredService<ISiteService>().GetSiteSettingsAsync();
            var defaultIndexProfileName = site.TryGet<SearchSettings>(out var searchSettings)
                ? searchSettings.DefaultIndexProfileName
                : null;

            indexProfile = string.IsNullOrEmpty(defaultIndexProfileName)
                ? null
                : await indexProfileManager.FindByNameAsync(defaultIndexProfileName);

            if (indexProfile is null)
            {
                return "The search was not run: no default search index is configured in the site search settings. Name an index profile in the index argument; listIndexProfiles lists them.";
            }
        }

        // The check of the search page: the permission to query any index, or the one of this index.
        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, IndexingPermissions.QuerySearchIndex, indexProfile))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var searchService = services.GetKeyedService<ISearchService>(indexProfile.ProviderName);

        if (searchService is null)
        {
            return $"The search was not run: the provider '{indexProfile.ProviderName}' of the index '{indexProfile.Name}' has no search service on this site.";
        }

        // Lucene builds its query parser with the profile's analyzer and throws a NullReferenceException without one, as
        // for a profile saveIndexProfile made before it set the default analyzer. The search isn't run then, so nothing
        // throws (a debugger would stop the whole site on the exception).
        if (IndexProfileAnalyzers.IsMissing(services, indexProfile))
        {
            return $"The search was not run: the index '{indexProfile.Name}' has no analyzer, so its provider can't read the terms. Call saveIndexProfile with the name '{indexProfile.Name}' to give it the default analyzer, then rebuildIndexProfile, and search it in a later message.";
        }

        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);

        // The paging of the search page: one result more than a page, to know whether there is a next page.
        var from = (page - 1) * pageSize;
        SearchResult searchResult;

        try
        {
            searchResult = await searchService.SearchAsync(indexProfile, terms.Trim(), from, from + pageSize + 1);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A provider can throw instead of failing the result, as Lucene does for an index profile without an
            // analyzer. The search page shows an error then; the tool answers so the model can tell the user.
            arguments.Services.GetService<ILoggerFactory>()?.CreateLogger<SearchIndexTool>()
                .LogWarning(ex, "AI tool '{ToolName}' could not search the index '{IndexName}'.", TheName, indexProfile.Name);

            return $"The search was not run: the provider '{indexProfile.ProviderName}' failed to search the index '{indexProfile.Name}' ({ex.GetType().Name}). Check its analyzer and default search fields on the index's admin page, then rebuild it. The site's log has the details.";
        }

        if (!searchResult.Success)
        {
            return $"The search was not run: the provider '{indexProfile.ProviderName}' could not search the index '{indexProfile.Name}'. Its physical index is missing, or the terms are not valid for this index. The site's log has the reason.";
        }

        // The handlers the search page calls after a search record what visitors search for. A search by this tool is
        // not a visitor's, so they are not called.
        var foundIds = searchResult.ContentItemIds ?? [];
        var contentItemIds = foundIds.Take(pageSize).ToList();
        var version = searchResult.Latest
            ? VersionOptions.Latest
            : VersionOptions.Published;

        var contentItems = contentItemIds.Count > 0
            ? await services.GetRequiredService<IContentManager>().GetAsync(contentItemIds, version)
            : [];

        var items = contentItems
            .OrderBy(contentItem => contentItemIds.IndexOf(contentItem.ContentItemId))
            .Select(contentItem => new
            {
                contentItemId = contentItem.ContentItemId,
                contentType = contentItem.ContentType,
                displayText = contentItem.DisplayText,
                published = contentItem.Published,
                highlights = DescribeHighlights(searchResult, contentItem.ContentItemId),
            })
            .ToList();

        return AgentToolResults.Json(new
        {
            index = indexProfile.Name,
            provider = indexProfile.ProviderName,
            terms = terms.Trim(),
            items,
            total = searchResult.TotalCount > 0
                ? searchResult.TotalCount
                : (long?)null,
            page,
            pageSize,
            hasMore = foundIds.Count > pageSize,
            notInContentStore = contentItemIds.Count > items.Count
                ? contentItemIds.Count - items.Count
                : (int?)null,
        });
    }

    private static Dictionary<string, List<string>> DescribeHighlights(SearchResult searchResult, string contentItemId)
    {
        if (searchResult.Highlights is null ||
            !searchResult.Highlights.TryGetValue(contentItemId, out var fields) ||
            fields is null ||
            fields.Count == 0)
        {
            return null;
        }

        var highlights = new Dictionary<string, List<string>>();

        foreach (var (field, fragments) in fields)
        {
            if (fragments is null || fragments.Count == 0)
            {
                continue;
            }

            highlights[field] = fragments
                .Take(MaxHighlightsPerField)
                .Select(fragment => QueryResultRows.Cut(fragment, MaxHighlightLength))
                .ToList();
        }

        return highlights.Count > 0
            ? highlights
            : null;
    }
}
