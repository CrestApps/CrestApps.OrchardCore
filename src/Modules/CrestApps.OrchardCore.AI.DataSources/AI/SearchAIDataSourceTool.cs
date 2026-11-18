using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.AI.Tooling.Instances.DataSources;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.DataSources.AI;

/// <summary>
/// Runs a test search of one data source with the retrieval a data source search tool instance uses, and returns what
/// an assistant would be given for the query.
/// </summary>
public sealed class SearchAIDataSourceTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "searchAIDataSource";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the data source to search, from listAIDataSources."
        },
        "query": {
          "type": "string",
          "description": "A short search phrase, such as 'refund policy'."
        },
        "topN": {
          "type": "integer",
          "minimum": 1,
          "maximum": 20,
          "description": "How many chunks to return. Leave it out for the site default."
        }
      },
      "required": ["id", "query"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Runs a test vector search of one data source for a query and returns the matching content as an assistant using that data source would receive it, with titles and references. Use it to check what a knowledge base returns before or after a change; it makes one embedding call.";

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
        var query = AIModuleTools.ReadString(arguments, "query");

        if (id is null || query is null)
        {
            return "Nothing was searched: give the data source's ID in id and a search phrase in query.";
        }

        var dataSource = await services.GetRequiredService<ISourceCatalogManager<AIDataSource>>().FindByIdAsync(id, cancellationToken);

        if (dataSource is null)
        {
            return $"Nothing was searched: no data source has the ID '{id}'. Call listAIDataSources to find it.";
        }

        int? topN = null;

        if (arguments.TryGetValue("topN", out var rawTopN) && rawTopN is not null)
        {
            topN = arguments.GetFirstValueOrDefault<int?>("topN", null);

            if (topN is null or < 1 or > 20)
            {
                return "Nothing was searched: topN must be a whole number from 1 to 20.";
            }
        }

        // The retrieval behind the data source search tool instances, run for this data source with its defaults.
        var search = new DataSourceSearchToolFunction(TheName, Description, new DataSourceSearchToolSettings
        {
            DataSourceId = dataSource.ItemId,
            TopNDocuments = topN,
        });

        var result = await search.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object>
        {
            ["queries"] = JsonSerializer.SerializeToElement(new[] { query }),
        })
        {
            Services = services,
        }, cancellationToken);

        var retrieval = result as DataSourceRetrievalResult;

        return AIModuleTools.Json(new
        {
            dataSource = new
            {
                id = dataSource.ItemId,
                displayText = dataSource.DisplayText,
                aiKnowledgeBaseIndexProfileName = dataSource.AIKnowledgeBaseIndexProfileName,
            },
            query,
            results = retrieval?.Text ?? result?.ToString(),
            figures = retrieval?.Figures.Count ?? 0,
            tables = retrieval?.Tables.Count ?? 0,
        });
    }
}
