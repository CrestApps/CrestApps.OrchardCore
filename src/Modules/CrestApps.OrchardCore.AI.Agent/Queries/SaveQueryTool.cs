using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Agent.Search;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Indexing;
using OrchardCore.Mvc.Utilities;
using OrchardCore.Queries;

namespace CrestApps.OrchardCore.AI.Agent.Queries;

/// <summary>
/// Creates or updates a saved Lucene or Elasticsearch query through the query manager the Queries admin page uses,
/// with the checks of that page. Everything is checked before anything is changed. A SQL query is not saved by this
/// tool: the page checks its SQL with a parser that only the Queries module can call.
/// </summary>
public sealed class SaveQueryTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveQuery";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "name": {
          "type": "string",
          "description": "The technical name of the query: letters and digits only, starting with a letter. It names the saved query to update, or the one to create when none has that name."
        },
        "newName": {
          "type": "string",
          "description": "To rename a saved query: its new technical name. Templates and API clients that call the old name stop getting results."
        },
        "source": {
          "type": "string",
          "enum": [
            "Lucene",
            "Elasticsearch"
          ],
          "description": "To create: the query source. It must be enabled on the site and cannot be changed afterwards."
        },
        "template": {
          "type": "string",
          "description": "The search query as JSON text in the Elasticsearch-style DSL; Liquid is allowed. Required to create. Test it first with runSearchQuery."
        },
        "index": {
          "type": "string",
          "description": "The name of the index profile the query runs on, of the same provider as the source. Required to create."
        },
        "schema": {
          "type": "string",
          "description": "The JSON schema of the documents the query returns, as JSON text; GraphQL uses it. An empty text removes it."
        },
        "returnContentItems": {
          "type": "boolean",
          "description": "True returns the content items of the matching documents; false returns the indexed documents."
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
    public override string Description => "Creates or updates a saved Lucene or Elasticsearch query: name, source, template (a JSON search query, Liquid allowed), index, optional result schema and the return-content-items flag. A saved query becomes callable from Liquid, GraphQL and `api/queries/{name}`, so test the template first with `runSearchQuery`. SQL queries are saved on the Queries admin page, not with this tool.";

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

        if (!arguments.TryGetFirstString("name", out var name) || string.IsNullOrWhiteSpace(name))
        {
            return "Nothing was saved: the name argument is required.";
        }

        name = name.Trim();

        var input = new QueryInput();

        if (arguments.TryGetFirstString("template", allowEmptyString: true, out var template))
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return "Nothing was saved: the template of a query cannot be empty.";
            }

            input.Template = template;
        }

        if (arguments.TryGetFirstString("schema", allowEmptyString: true, out var schema))
        {
            if (!string.IsNullOrEmpty(schema) && !schema.IsJson())
            {
                return "Nothing was saved: schema must be JSON text, or an empty text to remove the schema.";
            }

            input.Schema = schema;
        }

        if (arguments.TryGetFirst<bool>("returnContentItems", out var returnContentItems))
        {
            input.ReturnContentItems = returnContentItems;
        }

        arguments.TryGetFirstString("source", out var source);
        arguments.TryGetFirstString("index", out var index);
        arguments.TryGetFirstString("newName", out var newName);

        var queryManager = services.GetRequiredService<IQueryManager>();
        var query = await queryManager.GetQueryAsync(name);

        return query is null
            ? await CreateAsync(services, queryManager, name, newName, source, index, input)
            : await UpdateAsync(services, queryManager, query, newName, source, index, input);
    }

    // The Create action of the Queries page: a new query of the source, filled, checked and saved.
    private static async Task<object> CreateAsync(IServiceProvider services, IQueryManager queryManager, string name, string newName, string source, string index, QueryInput input)
    {
        if (newName is not null)
        {
            return $"Nothing was saved: there is no saved query named '{name}' to rename. Use listQueries to get the names.";
        }

        if (source is null)
        {
            return $"Nothing was saved: there is no saved query named '{name}'. To create it, give source, template and index.";
        }

        var sourceName = services.GetServices<IQuerySource>()
            .Select(querySource => querySource.Name)
            .FirstOrDefault(querySourceName => string.Equals(querySourceName, source.Trim(), StringComparison.OrdinalIgnoreCase));

        if (sourceName is null || !SearchQueryProviders.IsSupported(sourceName))
        {
            return DescribeUnsupportedSource(source.Trim(), sourceName is not null);
        }

        var problem = DescribeInvalidName(name);

        if (problem is not null)
        {
            return $"Nothing was saved: {problem}";
        }

        if (input.Template is null || index is null)
        {
            return "Nothing was saved: template and index are required to create a query.";
        }

        (index, problem) = await FindIndexAsync(services, index, sourceName);

        if (problem is not null)
        {
            return $"Nothing was saved: {problem}";
        }

        var data = new JsonObject
        {
            [nameof(Query.Name)] = name,
            ["Template"] = input.Template,
            ["Index"] = index,
        };

        if (!string.IsNullOrEmpty(input.Schema))
        {
            data[nameof(Query.Schema)] = input.Schema;
        }

        if (input.ReturnContentItems.HasValue)
        {
            data[nameof(Query.ReturnContentItems)] = input.ReturnContentItems.Value;
        }

        var query = await queryManager.NewAsync(sourceName, data);

        if (query is null)
        {
            return $"Nothing was saved: the query source '{sourceName}' is not available on this site.";
        }

        await queryManager.SaveAsync(query);

        return Describe(query, created: true);
    }

    // The Edit action of the Queries page: the stored query is removed under its old name and saved again with the
    // changes, which is also how that page renames a query.
    private static async Task<object> UpdateAsync(IServiceProvider services, IQueryManager queryManager, Query query, string newName, string source, string index, QueryInput input)
    {
        if (!SearchQueryProviders.IsSupported(query.Source))
        {
            return DescribeUnsupportedSource(query.Source, true);
        }

        if (source is not null && !string.Equals(source.Trim(), query.Source, StringComparison.OrdinalIgnoreCase))
        {
            return $"Nothing was saved: the source of the saved query '{query.Name}' is '{query.Source}' and cannot be changed. Save a query with another name for the source '{source.Trim()}'.";
        }

        newName = newName?.Trim();

        if (string.Equals(newName, query.Name, StringComparison.Ordinal))
        {
            newName = null;
        }

        if (newName is not null)
        {
            var problem = DescribeInvalidName(newName);

            if (problem is not null)
            {
                return $"Nothing was saved: {problem}";
            }

            var other = await queryManager.GetQueryAsync(newName);

            if (other is not null && !string.Equals(other.Name, query.Name, StringComparison.OrdinalIgnoreCase))
            {
                return $"Nothing was saved: a query named '{other.Name}' already exists.";
            }
        }

        if (index is not null)
        {
            (index, var problem) = await FindIndexAsync(services, index, query.Source);

            if (problem is not null)
            {
                return $"Nothing was saved: {problem}";
            }
        }

        if (newName is null && index is null && input.Template is null && input.Schema is null && !input.ReturnContentItems.HasValue)
        {
            return $"Nothing was saved: give newName, template, index, schema or returnContentItems to change the saved query '{query.Name}'.";
        }

        // The query manager sets the schema from the data on every update, so the stored one is sent when it stays.
        var data = new JsonObject
        {
            [nameof(Query.Schema)] = input.Schema ?? query.Schema,
        };

        if (input.Template is not null)
        {
            data["Template"] = input.Template;
        }

        if (index is not null)
        {
            data["Index"] = index;
        }

        if (input.ReturnContentItems.HasValue)
        {
            data[nameof(Query.ReturnContentItems)] = input.ReturnContentItems.Value;
        }

        await queryManager.DeleteQueryAsync(query.Name);

        if (newName is not null)
        {
            query.Name = newName;
        }

        await queryManager.UpdateAsync(query, data);

        return Describe(query, created: false);
    }

    private static string Describe(Query query, bool created)
    {
        return AgentToolResults.Json(new
        {
            saved = true,
            created,
            query = QueryContent.Describe(query),
            note = $"The query can be run with runQuery, and is callable by its name from Liquid templates, GraphQL and api/queries/{query.Name} by callers who may run it.",
        });
    }

    // The name check of the Queries page: required, and equal to its safe form.
    private static string DescribeInvalidName(string name)
    {
        var safeName = name.ToSafeName();

        if (!string.IsNullOrEmpty(safeName) && name == safeName)
        {
            return null;
        }

        return string.IsNullOrEmpty(safeName)
            ? $"the name '{name}' cannot be used. A query name holds letters and digits only and starts with a letter."
            : $"the name '{name}' cannot be used. A query name holds letters and digits only and starts with a letter, for example '{safeName}'.";
    }

    private static string DescribeUnsupportedSource(string source, bool enabled)
    {
        if (QueryContent.IsSql(source))
        {
            return "Nothing was saved: this tool does not save SQL queries. The Queries admin page checks the SQL of a query with a parser that this tool cannot call, so save a SQL query there.";
        }

        return enabled
            ? $"Nothing was saved: this tool saves Lucene and Elasticsearch queries. Save a query of the source '{source}' on the Queries admin page."
            : $"Nothing was saved: the query source '{source}' is not enabled on this site. listQueries gives the enabled sources.";
    }

    // The index list of the query editors: the index profiles of the provider of the source.
    private static async Task<(string Index, string Problem)> FindIndexAsync(IServiceProvider services, string index, string sourceName)
    {
        // The index profile manager is missing when the Indexing feature was turned off after the query was saved.
        var indexProfileManager = services.GetService<IIndexProfileManager>();
        var indexProfile = indexProfileManager is null
            ? null
            : await indexProfileManager.FindByNameAsync(index.Trim());

        if (indexProfile is null)
        {
            return (null, $"there is no index profile named '{index.Trim()}'. Use listIndexProfiles to get the names.");
        }

        if (!string.Equals(indexProfile.ProviderName, sourceName, StringComparison.OrdinalIgnoreCase))
        {
            return (null, $"the index '{indexProfile.Name}' belongs to the provider '{indexProfile.ProviderName}'. A {sourceName} query runs on a {sourceName} index.");
        }

        return (indexProfile.Name, null);
    }

    private sealed class QueryInput
    {
        public string Template { get; set; }

        public string Schema { get; set; }

        public bool? ReturnContentItems { get; set; }
    }
}
