using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Queries;

namespace CrestApps.OrchardCore.AI.Agent.Queries;

/// <summary>
/// Lists the saved queries of the site, as the Queries admin page does, and the query sources that are enabled.
/// </summary>
public sealed class ListQueriesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listQueries";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "Text the name of the query must contain. Leave it out to list every saved query."
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
          "description": "The number of queries on a page. The default is 20 and the most is 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the saved queries with name, source (Sql, Lucene, Elasticsearch), template, target index, result schema and whether they return content items, plus the query sources enabled on the site; supports a name filter and paging. Use it to find a query to run or edit.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, QueryPermissions.ManageQueries))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);

        arguments.TryGetFirstString("search", out var search);

        var result = await services.GetRequiredService<IQueryManager>().PageQueriesAsync(page, pageSize, new QueryContext
        {
            Name = search?.Trim(),
            Sorted = true,
        });

        return AgentToolResults.Json(new
        {
            items = result.Records.Select(QueryContent.Describe),
            total = result.Count,
            page,
            pageSize,
            sources = services.GetServices<IQuerySource>()
                .Select(source => source.Name)
                .Order(StringComparer.OrdinalIgnoreCase),
        });
    }
}
