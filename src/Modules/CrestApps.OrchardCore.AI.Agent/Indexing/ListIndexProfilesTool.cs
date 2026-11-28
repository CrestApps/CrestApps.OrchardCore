using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Indexing.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.Indexing;

/// <summary>
/// Lists the search index profiles of the site, as the Indexes admin page does, and the provider and type
/// combinations a new index profile can be created for.
/// </summary>
public sealed class ListIndexProfilesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listIndexProfiles";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "Text the name of the index profile must contain. Leave it out to list every index profile."
        },
        "provider": {
          "type": "string",
          "description": "The technical name of a search provider, for example Lucene or Elasticsearch, to list only its index profiles. The sources of the result give the names."
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
          "description": "The number of index profiles on a page. The default is 20 and the most is 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the search index profiles (name, provider, type, index name, indexed content types, created date) with an optional name or provider filter, and the provider and type combinations available to create a new one. Use it to find an index before searching, querying or rebuilding it.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, IndexingPermissions.ManageIndexes))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var indexProfileManager = services.GetRequiredService<IIndexProfileManager>();
        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);

        arguments.TryGetFirstString("search", out var search);
        search = search?.Trim();

        IEnumerable<IndexProfile> indexProfiles;
        int total;

        if (arguments.TryGetFirstString("provider", out var provider))
        {
            // The Indexes page has no provider filter; the index profiles of one provider are few, so they are
            // filtered and paged here.
            var matches = (await indexProfileManager.GetByProviderAsync(provider.Trim()))
                .Where(indexProfile => string.IsNullOrEmpty(search) || indexProfile.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
                .OrderBy(indexProfile => indexProfile.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            total = matches.Count;
            indexProfiles = matches.Skip((page - 1) * pageSize).Take(pageSize);
        }
        else
        {
            var result = await indexProfileManager.PageAsync(page, pageSize, new QueryContext
            {
                Sorted = true,
                Name = search,
            });

            total = result.Count;
            indexProfiles = result.Models;
        }

        var localClock = services.GetRequiredService<ILocalClock>();
        var items = new List<object>();

        foreach (var indexProfile in indexProfiles)
        {
            items.Add(await IndexProfileContent.DescribeSummaryAsync(indexProfile, localClock));
        }

        var indexingOptions = services.GetRequiredService<IOptions<IndexingOptions>>().Value;

        return AgentToolResults.Json(new
        {
            items,
            total,
            page,
            pageSize,
            sources = indexingOptions.Sources.Values
                .OrderBy(source => source.ProviderName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(source => source.Type, StringComparer.OrdinalIgnoreCase)
                .Select(source => new
                {
                    provider = source.ProviderName,
                    providerDisplayName = indexingOptions.Providers.TryGetValue(source.ProviderName, out var providerEntry)
                        ? providerEntry.DisplayName?.Value
                        : null,
                    type = source.Type,
                    displayName = source.DisplayName?.Value,
                    description = source.Description?.Value,
                }),
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(localClock),
        });
    }
}
