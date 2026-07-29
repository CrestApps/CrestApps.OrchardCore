using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.YesSql.Core.Migrations;

/// <summary>
/// Decides whether dropping an index needs a schema-qualified name because the engine resolves an index by name
/// rather than through its table.
/// </summary>
/// <remarks>
/// PostgreSQL and SQLite name only the index in a drop, so the name is resolved against the connection's search
/// path instead of the table's schema. A tenant whose tables live in a named schema therefore drops nothing, and
/// because the statement is written with <c>IF EXISTS</c> the miss is silent: the failure only surfaces later,
/// when recreating the index reports that it already exists and the tenant cannot activate. SQL Server and MySQL
/// name the table in the drop, so they resolve the index correctly and need no help.
/// </remarks>
public static class SchemaQualifiedIndexDrop
{
    /// <summary>
    /// Gets the quoted, schema-qualified index name a drop must use, or <see langword="null"/> when the
    /// statement the data layer emits already finds the index.
    /// </summary>
    /// <param name="schemaBuilder">The active schema builder.</param>
    /// <param name="store">The YesSql store.</param>
    /// <param name="indexType">The index whose table carries the database index.</param>
    /// <param name="indexName">The name of the database index to drop.</param>
    /// <param name="collection">The collection the index table belongs to.</param>
    /// <returns>The quoted, schema-qualified index name, or <see langword="null"/> when none is needed.</returns>
    public static string TryGetQualifiedIndexName(
        ISchemaBuilder schemaBuilder,
        IStore store,
        Type indexType,
        string indexName,
        string collection)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(indexType);
        ArgumentException.ThrowIfNullOrEmpty(indexName);

        var schema = store.Configuration.Schema;

        if (string.IsNullOrEmpty(schema))
        {
            return null;
        }

        var tableName = schemaBuilder.TablePrefix +
            schemaBuilder.TableNameConvention.GetIndexTable(indexType, collection);
        var quotedTableName = schemaBuilder.Dialect.QuoteForTableName(tableName, schema);
        var emitted = schemaBuilder.Dialect.GetDropIndexString(indexName, tableName, schema);

        if (emitted.Contains(quotedTableName, StringComparison.Ordinal))
        {
            return null;
        }

        // The name the index was created under is not the name the caller passes: the data layer prefixes it on
        // the engines that share one index namespace across tables, and shortens it where the engine has a name
        // length limit. Qualifying the caller's name would name an index that does not exist, which is the same
        // silent miss this helper exists to remove.
        var effectiveIndexName = schemaBuilder.Dialect.PrefixIndex
            ? schemaBuilder.TablePrefix + indexName
            : indexName;

        return schemaBuilder.Dialect.QuoteForTableName(
            schemaBuilder.Dialect.FormatIndexName(effectiveIndexName),
            schema);
    }
}
