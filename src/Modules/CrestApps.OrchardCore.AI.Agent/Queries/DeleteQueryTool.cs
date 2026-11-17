using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Queries;

namespace CrestApps.OrchardCore.AI.Agent.Queries;

/// <summary>
/// Deletes saved queries, as the Delete action of the Queries admin page does.
/// </summary>
public sealed class DeleteQueryTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteQuery";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "names": {
          "type": "array",
          "description": "The names of the saved queries to delete, from listQueries. At most 50.",
          "items": {
            "type": "string"
          },
          "minItems": 1,
          "maxItems": 50
        }
      },
      "required": [
        "names"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes one or more saved queries by name. Templates, GraphQL clients and API consumers that call them will stop getting results.";

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

        if (!arguments.TryGetFirst<string[]>("names", out var givenNames) || givenNames is not { Length: > 0 })
        {
            return "Nothing was deleted: names must be a list of names of saved queries. Use listQueries to get them.";
        }

        if (givenNames.Length > QueryContent.MaxQueries)
        {
            return $"Nothing was deleted: one call deletes at most {QueryContent.MaxQueries} queries. Delete the queries in several calls.";
        }

        var queryManager = services.GetRequiredService<IQueryManager>();
        var names = new List<string>();
        var notFound = new List<string>();

        foreach (var givenName in givenNames)
        {
            var query = string.IsNullOrWhiteSpace(givenName)
                ? null
                : await queryManager.GetQueryAsync(givenName.Trim());

            if (query is null)
            {
                notFound.Add(givenName);
            }
            else if (!names.Contains(query.Name, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(query.Name);
            }
        }

        if (notFound.Count > 0)
        {
            return $"Nothing was deleted: there is no saved query named {string.Join(", ", notFound.Select(name => $"'{name}'"))}. Use listQueries to get the names.";
        }

        if (!await queryManager.DeleteQueryAsync([.. names]))
        {
            return "Nothing was deleted: the queries were not found any more.";
        }

        return AgentToolResults.Json(new
        {
            deleted = names,
            deletedCount = names.Count,
            note = "The queries cannot be restored. Liquid templates, GraphQL clients and API consumers that called them get no results.",
        });
    }
}
