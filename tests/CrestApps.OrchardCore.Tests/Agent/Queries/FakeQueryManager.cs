using System.Text.Json.Nodes;
using OrchardCore.Queries;

namespace CrestApps.OrchardCore.Tests.Agent.Queries;

/// <summary>
/// Stands in for the query manager in a tool test: it keeps the saved queries in memory, fills and updates a query
/// from its data the way the query manager and the query handlers of Orchard Core do, runs a query with the test's
/// function, and records every call that changes or runs something, in order.
/// </summary>
internal sealed class FakeQueryManager : IQueryManager
{
    /// <summary>
    /// Gets the saved queries, by name. As in Orchard Core, the case of a name is ignored.
    /// </summary>
    public Dictionary<string, Query> Queries { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the names of the query sources that are enabled.
    /// </summary>
    public List<string> Sources { get; } = ["Lucene", "Elasticsearch", "Sql"];

    /// <summary>
    /// Gets the calls that changed or ran something, in order, for example <c>Save:Recent</c>.
    /// </summary>
    public List<string> Calls { get; } = [];

    /// <summary>
    /// Gets or sets what running a query returns.
    /// </summary>
    public Func<Query, IDictionary<string, object>, IQueryResults> Execute { get; set; } = (_, _) => new Results();

    /// <summary>
    /// Gets the query that was run last.
    /// </summary>
    public Query Executed { get; private set; }

    /// <summary>
    /// Gets the parameters of the query that was run last.
    /// </summary>
    public IDictionary<string, object> ExecutedParameters { get; private set; }

    /// <summary>
    /// Adds a saved query with the metadata its source stores.
    /// </summary>
    /// <param name="name">The name of the query.</param>
    /// <param name="source">The name of the source.</param>
    /// <param name="template">The template.</param>
    /// <param name="index">The index, for a search source.</param>
    /// <returns>The query.</returns>
    public Query Add(string name, string source, string template, string index = null)
    {
        var query = new Query
        {
            Name = name,
            Source = source,
        };

        SetMetadata(query, template, index);
        Queries[name] = query;

        return query;
    }

    /// <summary>
    /// Reads the template a source stored on a query.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <returns>The template, or <see langword="null"/> when there is none.</returns>
    public static string GetTemplate(Query query)
        => query.Properties[$"{query.Source}QueryMetadata"]?["Template"]?.GetValue<string>();

    /// <summary>
    /// Reads the index a source stored on a query.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <returns>The index, or <see langword="null"/> when there is none.</returns>
    public static string GetIndex(Query query)
        => query.Properties[$"{query.Source}QueryMetadata"]?["Index"]?.GetValue<string>();

    public Task<Query> NewAsync(string source, JsonNode data = null)
    {
        if (!Sources.Contains(source))
        {
            return Task.FromResult<Query>(null);
        }

        var query = new Query
        {
            Source = source,
        };

        if (data is not null)
        {
            SetMetadata(query, data["Template"]?.GetValue<string>(), data["Index"]?.GetValue<string>());

            query.Name = data[nameof(Query.Name)]?.GetValue<string>();

            UpdateBaseProperties(data, query);
        }

        return Task.FromResult(query);
    }

    public Task UpdateAsync(Query query, JsonNode data = null)
    {
        Calls.Add($"Update:{query.Name}");

        if (data is not null)
        {
            UpdateBaseProperties(data, query);
            SetMetadata(query, data["Template"]?.GetValue<string>(), data["Index"]?.GetValue<string>());
        }

        Queries[query.Name] = query;

        return Task.CompletedTask;
    }

    public Task<bool> DeleteQueryAsync(params string[] name)
    {
        Calls.Add($"Delete:{string.Join(",", name)}");

        var deleted = false;

        foreach (var queryName in name)
        {
            deleted |= Queries.Remove(queryName);
        }

        return Task.FromResult(deleted);
    }

    public Task<Query> GetQueryAsync(string name)
        => Task.FromResult(Queries.GetValueOrDefault(name));

    public Task<IQueryResults> ExecuteQueryAsync(Query query, IDictionary<string, object> parameters)
    {
        Calls.Add($"Execute:{query.Name}");
        Executed = query;
        ExecutedParameters = parameters;

        return Task.FromResult(Execute(query, parameters));
    }

    public Task<string> GetIdentifierAsync()
        => Task.FromResult("identifier");

    public Task<IEnumerable<Query>> ListQueriesAsync(QueryContext context = null)
        => Task.FromResult(Locate(context));

    public Task<ListQueryResult> PageQueriesAsync(int page, int pageSize, QueryContext context = null)
    {
        var matches = Locate(context).ToList();

        return Task.FromResult(new ListQueryResult
        {
            Count = matches.Count,
            Records = matches.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
        });
    }

    public Task SaveAsync(params Query[] queries)
    {
        foreach (var query in queries)
        {
            Calls.Add($"Save:{query.Name}");
            Queries[query.Name] = query;
        }

        return Task.CompletedTask;
    }

    private IEnumerable<Query> Locate(QueryContext context)
    {
        var queries = Queries.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(context?.Name))
        {
            queries = queries.Where(query => query.Name.Contains(context.Name, StringComparison.OrdinalIgnoreCase));
        }

        return context?.Sorted == true
            ? queries.OrderBy(query => query.Name, StringComparer.Ordinal)
            : queries;
    }

    // What the query handler of a source does: the template and the index are kept in the metadata of the source,
    // and a value that is not given leaves the stored one.
    private static void SetMetadata(Query query, string template, string index)
    {
        var key = $"{query.Source}QueryMetadata";
        var stored = query.Properties[key] as JsonObject;
        var metadata = stored ?? [];

        if (!string.IsNullOrEmpty(template))
        {
            metadata["Template"] = template;
        }

        if (!string.IsNullOrEmpty(index))
        {
            metadata["Index"] = index;
        }

        if (stored is null && metadata.Count > 0)
        {
            query.Properties[key] = metadata;
        }
    }

    // What the query manager does on every create and update: the schema is taken from the data, given or not.
    private static void UpdateBaseProperties(JsonNode data, Query query)
    {
        query.Schema = data[nameof(Query.Schema)]?.GetValue<string>();

        var returnContentItems = data[nameof(Query.ReturnContentItems)];

        if (returnContentItems is not null)
        {
            query.ReturnContentItems = returnContentItems.GetValue<bool>();
        }
    }

    /// <summary>
    /// The results of a query in a test.
    /// </summary>
    public sealed class Results : IQueryResults
    {
        public IEnumerable<object> Items { get; set; } = [];
    }
}
