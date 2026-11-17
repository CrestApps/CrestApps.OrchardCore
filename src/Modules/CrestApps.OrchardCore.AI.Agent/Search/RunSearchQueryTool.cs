using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Agent.Indexing;
using CrestApps.OrchardCore.AI.Agent.Queries;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Elasticsearch;
using OrchardCore.Elasticsearch.Core.Models;
using OrchardCore.Elasticsearch.Models;
using OrchardCore.Entities;
using OrchardCore.Indexing;
using OrchardCore.Lucene;
using OrchardCore.Lucene.Models;
using OrchardCore.Queries;

namespace CrestApps.OrchardCore.AI.Agent.Search;

/// <summary>
/// Runs an ad-hoc JSON query against a Lucene or Elasticsearch index through the query source of the provider, which
/// runs it with the query service the "Run query" admin page of the provider uses.
/// </summary>
public sealed class RunSearchQueryTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "runSearchQuery";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "index": {
          "type": "string",
          "description": "The ID or the name of a Lucene or Elasticsearch index profile, from listIndexProfiles."
        },
        "query": {
          "type": "string",
          "description": "The search query as JSON text in the Elasticsearch-style DSL, for example { \"query\": { \"match_all\": {} }, \"size\": 10 }. Set size in it to limit the documents. Liquid is allowed and reads the parameters."
        },
        "parameters": {
          "type": "object",
          "description": "The values the Liquid of the query reads, by name. Leave it out when the query has no Liquid."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page of the returned documents to show, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of documents on a page. The default is 20 and the most is 50."
        }
      },
      "required": [
        "index",
        "query"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Runs an ad-hoc JSON query (Elasticsearch-style DSL, with optional Liquid parameters) against a Lucene or Elasticsearch index profile and returns the matching documents and the count. Use it to test a query before saving it with `saveQuery`; not available for Azure AI Search indexes. Use getIndexProfile first for the names of the indexed fields.";

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

        // The caller needs the permission of the "Run query" page of the provider of the index. A caller who holds
        // none of them is refused before an index is looked up.
        var allowedProviders = new List<string>();

        foreach (var providerName in SearchQueryProviders.Names)
        {
            if (await AgentToolAuthorization.IsAuthorizedAsync(services, SearchQueryProviders.GetRunQueryPermission(providerName)))
            {
                allowedProviders.Add(providerName);
            }
        }

        if (allowedProviders.Count == 0)
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("index", out var index))
        {
            return "The index argument is required: the ID or the name of a Lucene or Elasticsearch index profile.";
        }

        if (!TryReadQuery(arguments, out var template))
        {
            return "The query argument is required: a search query as JSON text.";
        }

        if (!QueryResultRows.TryReadParameters(arguments, out var parameters))
        {
            return "The query was not run: parameters must be a JSON object.";
        }

        var indexProfile = await IndexProfileContent.FindAsync(services.GetRequiredService<IIndexProfileManager>(), index);

        if (indexProfile is null)
        {
            return $"The query was not run: there is no index profile with the ID or the name '{index}'. Use listIndexProfiles to get them.";
        }

        if (!SearchQueryProviders.IsSupported(indexProfile.ProviderName))
        {
            return $"The query was not run: the index '{indexProfile.Name}' belongs to the provider '{indexProfile.ProviderName}', which takes no JSON query. This tool queries Lucene and Elasticsearch indexes; use searchIndex for a plain-text search.";
        }

        if (!allowedProviders.Exists(providerName => string.Equals(providerName, indexProfile.ProviderName, StringComparison.OrdinalIgnoreCase)))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var isLucene = SearchQueryProviders.IsLucene(indexProfile.ProviderName);

        if (!isLucene && services.GetService<IOptions<ElasticsearchConnectionOptions>>()?.Value.ConfigurationExists() != true)
        {
            return "The query was not run: Elasticsearch is not configured for this site.";
        }

        var indexManager = services.GetKeyedService<IIndexManager>(indexProfile.ProviderName);

        if (indexManager is not null && !await indexManager.ExistsAsync(indexProfile.IndexFullName))
        {
            return $"The query was not run: the index profile '{indexProfile.Name}' has no physical index. Rebuild it with rebuildIndexProfile.";
        }

        // As the query API of the provider does: a query that is not saved, run by the query source of the provider.
        var queryManager = services.GetRequiredService<IQueryManager>();
        var sourceName = isLucene
            ? LuceneConstants.ProviderName
            : ElasticsearchConstants.ProviderName;

        var query = await queryManager.NewAsync(sourceName);

        if (query is null)
        {
            return $"The query was not run: the query source of the provider '{indexProfile.ProviderName}' is not available on this site.";
        }

        query.ReturnContentItems = false;

        if (isLucene)
        {
            query.Put(new LuceneQueryMetadata
            {
                Index = indexProfile.Name,
                Template = template,
            });
        }
        else
        {
            query.Put(new ElasticsearchQueryMetadata
            {
                Index = indexProfile.Name,
                Template = template,
            });
        }

        IQueryResults results;

        try
        {
            results = await queryManager.ExecuteQueryAsync(query, parameters);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The "Run query" page shows the same message to the same caller.
            return $"The query failed: {exception.Message}";
        }

        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);
        var rows = QueryResultRows.CreatePage(results?.Items, page, pageSize);

        return AgentToolResults.Json(new
        {
            index = indexProfile.Name,
            provider = indexProfile.ProviderName,
            matches = SearchQueryProviders.GetMatchCount(results),
            items = rows.Items,
            total = rows.Total,
            page,
            pageSize,
            truncated = rows.Truncated
                ? true
                : (bool?)null,
            notes = rows.GetNotes(),
        });
    }

    // A model sends the query as JSON text or as a JSON object; both are accepted.
    private static bool TryReadQuery(AIFunctionArguments arguments, out string template)
    {
        template = null;

        if (!arguments.TryGetFirst("query", out var value))
        {
            return false;
        }

        template = value switch
        {
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            JsonElement { ValueKind: JsonValueKind.Object } element => element.GetRawText(),
            JsonObject jsonObject => jsonObject.ToJsonString(),
            _ => null,
        };

        return !string.IsNullOrWhiteSpace(template);
    }
}
