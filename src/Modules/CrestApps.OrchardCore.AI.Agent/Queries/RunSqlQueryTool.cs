using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Queries;

namespace CrestApps.OrchardCore.AI.Agent.Queries;

/// <summary>
/// Runs an ad-hoc SQL query through the SQL query source of Orchard Core, which parses it with the parser the
/// "Run SQL Query" admin page uses: only a SELECT is run, and the table prefix of the tenant is applied.
/// </summary>
public sealed class RunSqlQueryTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "runSqlQuery";

    private const int ManyRows = 200;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "query": {
          "type": "string",
          "description": "One SQL SELECT statement in the Orchard SQL dialect, with table names without the tenant prefix and a LIMIT, for example SELECT ContentType, COUNT(*) AS Total FROM ContentItemIndex GROUP BY ContentType LIMIT 50. Use @name for a parameter. Liquid is allowed."
        },
        "parameters": {
          "type": "object",
          "description": "The values of the @name parameters of the query, by name. Leave it out when the query has none."
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
        "query"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Runs an ad-hoc read-only SQL SELECT (Orchard SQL dialect, table prefix applied automatically, optional parameters) against the site's database and returns the rows. It can read any table, including user and settings records, so use it only for an explicit reporting or diagnostics request and always limit the rows. Rows are returned a page at a time, long values are shortened, and values named like a credential are left out.";

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

        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, OrchardCorePermissions.ManageSqlQueries))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("query", out var sql) || string.IsNullOrWhiteSpace(sql))
        {
            return "The query argument is required: one SQL SELECT statement.";
        }

        if (!QueryResultRows.TryReadParameters(arguments, out var parameters))
        {
            return "The query was not run: parameters must be a JSON object.";
        }

        // A query that is not saved, run by the SQL query source. The source parses the SQL with Orchard Core's SQL
        // parser, which accepts a SELECT only and applies the table prefix; this tool never opens a connection.
        var queryManager = services.GetRequiredService<IQueryManager>();
        var query = await queryManager.NewAsync(QueryContent.SqlSourceName, new JsonObject
        {
            ["Template"] = sql,
        });

        if (query is null)
        {
            return "The query was not run: the SQL query source is not available on this site.";
        }

        query.ReturnContentItems = false;

        IQueryResults results;

        try
        {
            results = await queryManager.ExecuteQueryAsync(query, parameters);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The "Run SQL Query" page shows the same message to the same caller.
            return $"The query failed: {exception.Message}";
        }

        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);
        var rows = QueryResultRows.CreatePage(results?.Items, page, pageSize);
        var notes = rows.GetNotes() ?? [];

        if (rows.Total == 0)
        {
            notes.Add(QueryContent.NoSqlRowsNote);
        }
        else if (rows.Total > ManyRows)
        {
            notes.Add($"The query returned {rows.Total} rows, all read from the database. Add a LIMIT to the query.");
        }

        return AgentToolResults.Json(new
        {
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
