using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Agent.Search;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Queries;

namespace CrestApps.OrchardCore.AI.Agent.Queries;

/// <summary>
/// Executes a saved query by name through the query manager, with the permission check of the query API.
/// </summary>
public sealed class RunQueryTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "runQuery";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "The name of the saved query, from listQueries."
        },
        "parameters": {
          "type": "object",
          "description": "The values the template of the query reads, by name, for example { \"size\": 5 }. Leave it out when the query takes none."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page of the returned rows to show, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of rows on a page. The default is 20 and the most is 50."
        }
      },
      "required": [
        "name"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Executes a saved query by name with optional parameters and returns its rows or content items. Use it to answer data questions with queries an administrator already defined. Rows are returned a page at a time, long values are shortened, and a content item is returned as its ID, type and title.";

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

        // The permission is the one of the query, so the query is found first. Nothing is looked up for a caller who
        // is not signed in.
        if (AgentToolAuthorization.GetCaller(services) is null)
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("name", out var name) || string.IsNullOrWhiteSpace(name))
        {
            return "The name argument is required: the name of a saved query.";
        }

        name = name.Trim();

        var queryManager = services.GetRequiredService<IQueryManager>();
        var query = await queryManager.GetQueryAsync(name);

        // The check of the query API. It is made for a name that matches no query too, so a caller who may not run
        // queries does not learn which names exist.
        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, QueryPermissions.CreatePermissionForQuery(query?.Name ?? name)))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (query is null)
        {
            return $"The query was not run: there is no saved query named '{name}'. Use listQueries to get the names.";
        }

        if (!QueryResultRows.TryReadParameters(arguments, out var parameters))
        {
            return "The query was not run: parameters must be a JSON object.";
        }

        IQueryResults results;

        try
        {
            results = await queryManager.ExecuteQueryAsync(query, parameters);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The caller may run the query but not necessarily read or change it, so the error stays in the log.
            services.GetService<ILogger<RunQueryTool>>()?.LogError(exception, "The saved query '{QueryName}' failed when the runQuery AI tool ran it.", query.Name);

            return $"The query '{query.Name}' failed with an error. The site's log has the details. Check the parameters, or the template of the query on the Queries admin page.";
        }

        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);
        var rows = QueryResultRows.CreatePage(results?.Items, page, pageSize);
        var notes = rows.GetNotes() ?? [];

        if (rows.Total == 0 && QueryContent.IsSql(query.Source))
        {
            notes.Add(QueryContent.NoSqlRowsNote);
        }

        return AgentToolResults.Json(new
        {
            name = query.Name,
            source = query.Source,
            returnContentItems = query.ReturnContentItems,
            matches = SearchQueryProviders.GetMatchCount(results),
            items = rows.Items,
            total = rows.Total,
            page,
            pageSize,
            truncated = rows.Truncated
                ? true
                : (bool?)null,
            notes = notes.Count > 0
                ? notes
                : null,
        });
    }
}
