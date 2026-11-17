using System.Text.Json.Nodes;
using OrchardCore.Queries;

namespace CrestApps.OrchardCore.AI.Agent.Queries;

/// <summary>
/// Describes a saved query with an explicit list of fields.
/// </summary>
internal static class QueryContent
{
    /// <summary>
    /// The name of the SQL query source. Orchard Core declares it in the Queries module, which this module cannot
    /// reference.
    /// </summary>
    public const string SqlSourceName = "Sql";

    /// <summary>
    /// The most queries one call of a tool deletes.
    /// </summary>
    public const int MaxQueries = 50;

    /// <summary>
    /// What a tool adds to its result when a SQL query returned no rows. The SQL query source logs a query its parser
    /// refuses and returns no rows, so the two cases look the same to the tool.
    /// </summary>
    public const string NoSqlRowsNote = "The query returned no rows. Orchard Core also returns no rows, without an error, when its SQL parser refuses the query (a syntax it does not support, or a statement that is not a SELECT); the site's log then has the parse error.";

    private const int MaxTemplateLength = 4000;

    private const string TemplateKey = "Template";

    private const string IndexKey = "Index";

    /// <summary>
    /// Whether a query runs SQL.
    /// </summary>
    /// <param name="source">The name of the source of the query.</param>
    /// <returns><see langword="true"/> for the SQL query source.</returns>
    public static bool IsSql(string source)
        => string.Equals(source, SqlSourceName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Describes a saved query.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <returns>The fields of the query.</returns>
    public static object Describe(Query query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (template, index) = ReadTemplateAndIndex(query);

        return new
        {
            name = query.Name,
            source = query.Source,
            index,
            template = QueryResultRows.Cut(template, MaxTemplateLength),
            templateTruncated = template?.Length > MaxTemplateLength
                ? true
                : (bool?)null,
            schema = string.IsNullOrEmpty(query.Schema)
                ? null
                : query.Schema,
            returnContentItems = query.ReturnContentItems,
        };
    }

    // Each query source keeps its template, and the index it runs on, in its own metadata under the properties of the
    // query, and the metadata type of the SQL source is inside the Queries module. The two values are read by the
    // names the query manager takes them by, whatever the source.
    private static (string Template, string Index) ReadTemplateAndIndex(Query query)
    {
        if (query.Properties is null)
        {
            return (null, null);
        }

        foreach (var (_, value) in query.Properties)
        {
            if (value is not JsonObject metadata)
            {
                continue;
            }

            var template = ReadText(metadata, TemplateKey);

            if (template is not null)
            {
                return (template, ReadText(metadata, IndexKey));
            }
        }

        return (null, null);
    }

    private static string ReadText(JsonObject metadata, string name)
    {
        foreach (var (key, value) in metadata)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase) &&
                value is JsonValue jsonValue &&
                jsonValue.TryGetValue<string>(out var text))
            {
                return text;
            }
        }

        return null;
    }
}
