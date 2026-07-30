using YesSql;
using YesSql.Sql;
using YesSql.Utils;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Provides shared, dialect-portable SQL helpers used by Contact Center index migrations to preflight
/// legacy rows and create unique constraints.
/// </summary>
internal static class ContactCenterMigrationSql
{
    /// <summary>
    /// Gets the quoted, prefixed index table name for the specified index type.
    /// </summary>
    /// <param name="schemaBuilder">The active schema builder.</param>
    /// <param name="store">The YesSql store.</param>
    /// <param name="indexType">The index type.</param>
    /// <returns>The quoted, prefixed table name.</returns>
    public static string GetQuotedTableName(ISchemaBuilder schemaBuilder, IStore store, Type indexType)
    {
        var tableName = schemaBuilder.TablePrefix +
            schemaBuilder.TableNameConvention.GetIndexTable(indexType, ContactCenterConstants.CollectionName);

        return schemaBuilder.Dialect.QuoteForTableName(tableName, store.Configuration.Schema);
    }

    /// <summary>
    /// Adds a retention timestamp column to an existing index table and backfills it so rows that predate the
    /// column are not treated as infinitely old.
    /// </summary>
    /// <remarks>
    /// A column added without a backfill leaves every existing row at the default instant, which is older than
    /// any retention cutoff, so the first retention cycle after an upgrade would delete the entire table's
    /// history at once. Backfilling to the upgrade time instead gives those rows one full retention window,
    /// which matters most for the processed-event markers: purging one while its event can still be redelivered
    /// reintroduces the duplicate processing the marker exists to prevent. The backfill is a single set-based
    /// statement rather than a row-by-row loop, so it stays inside the tenant startup budget.
    /// </remarks>
    /// <param name="schemaBuilder">The active schema builder.</param>
    /// <param name="store">The YesSql store.</param>
    /// <param name="indexType">The index type whose table gains the column.</param>
    /// <param name="columnName">The name of the column to add.</param>
    /// <param name="backfillUtc">The instant existing rows adopt.</param>
    /// <param name="settledRowsFilter">
    /// An optional predicate restricting the backfill to rows that have already settled, so a record still in
    /// flight is not handed a false completion time. It is composed only from migration-owned constants and
    /// dialect-quoted column names; no caller-supplied or user-supplied text reaches it.
    /// </param>
    public static async Task AddRetentionColumnAsync(
        ISchemaBuilder schemaBuilder,
        IStore store,
        Type indexType,
        string columnName,
        DateTime backfillUtc,
        string settledRowsFilter = null)
    {
        var quotedTableName = GetQuotedTableName(schemaBuilder, store, indexType);
        var quotedColumnName = schemaBuilder.Dialect.QuoteForColumnName(columnName);
        var settledFilter = string.IsNullOrEmpty(settledRowsFilter)
            ? string.Empty
            : $" AND {settledRowsFilter}";

        await using var command = schemaBuilder.Connection.CreateCommand();

        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = $"UPDATE {quotedTableName} SET {quotedColumnName} = @backfillUtc WHERE {quotedColumnName} IS NULL{settledFilter}";

        var parameter = command.CreateParameter();

        parameter.ParameterName = "@backfillUtc";
        parameter.Value = backfillUtc;

        command.Parameters.Add(parameter);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Builds a dialect-portable string concatenation expression.
    /// </summary>
    /// <remarks>
    /// String concatenation is one of the few expressions with no shared syntax across the supported engines:
    /// SQLite and PostgreSQL use <c>||</c>, SQL Server uses <c>+</c>, and MySQL requires <c>concat()</c>. A
    /// migration that hard-codes any one of them silently produces a wrong value or a syntax error on the
    /// others, so the dialect is asked to render it.
    /// </remarks>
    /// <param name="dialect">The active SQL dialect.</param>
    /// <param name="fragments">The already-quoted operands, in order.</param>
    /// <returns>The concatenation expression in the dialect's own syntax.</returns>
    public static string BuildConcat(ISqlDialect dialect, params string[] fragments)
    {
        ArgumentNullException.ThrowIfNull(dialect);
        ArgumentNullException.ThrowIfNull(fragments);

        var builder = new RentedStringBuilder(256);

        dialect.Concat(builder, fragments.Select<string, Action<IStringBuilder>>(
            fragment => target => target.Append(fragment)).ToArray());

        var expression = builder.ToString();

        builder.Dispose();

        return expression;
    }

    /// <summary>
    /// Executes a set-based statement against the migration's connection and transaction.
    /// </summary>
    /// <remarks>
    /// Backfills run inside the transaction that gates tenant startup, so they are expressed as whole-table
    /// statements rather than a command per row: a per-row loop turns a one million row tenant into one
    /// million round trips and the tenant never finishes activating.
    /// </remarks>
    /// <param name="schemaBuilder">The active schema builder.</param>
    /// <param name="commandText">The statement text.</param>
    /// <param name="parameters">The optional named parameters.</param>
    public static async Task ExecuteAsync(
        ISchemaBuilder schemaBuilder,
        string commandText,
        params (string Name, object Value)[] parameters)
    {
        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = commandText;

        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Determines whether at least one row matches the specified query.
    /// </summary>
    /// <param name="schemaBuilder">The active schema builder.</param>
    /// <param name="commandText">The query text that returns at least one row when a match exists.</param>
    /// <param name="parameters">The optional named parameters.</param>
    /// <returns><see langword="true"/> when a matching row exists; otherwise <see langword="false"/>.</returns>
    public static async Task<bool> ExistsAsync(
        ISchemaBuilder schemaBuilder,
        string commandText,
        params (string Name, object Value)[] parameters)
    {
        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = commandText;

        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        return await command.ExecuteScalarAsync() is not null;
    }

    /// <summary>
    /// Builds a portable <c>CREATE UNIQUE INDEX</c> statement whose identifiers are quoted entirely through
    /// the supplied dialect, so the same migration code emits valid SQL on every supported database engine.
    /// </summary>
    /// <param name="dialect">The active SQL dialect.</param>
    /// <param name="tablePrefix">The configured table prefix, applied to the index name when the dialect requires globally-unique index names.</param>
    /// <param name="quotedTableName">The already dialect-quoted table name the index is created on.</param>
    /// <param name="indexName">The unqualified unique index name.</param>
    /// <param name="columnNames">The unquoted columns that participate in the unique constraint.</param>
    /// <returns>The dialect-quoted <c>CREATE UNIQUE INDEX</c> statement.</returns>
    public static string BuildCreateUniqueIndexStatement(
        ISqlDialect dialect,
        string tablePrefix,
        string quotedTableName,
        string indexName,
        params string[] columnNames)
    {
        ArgumentNullException.ThrowIfNull(dialect);

        if (dialect.PrefixIndex)
        {
            indexName = tablePrefix + indexName;
        }

        var quotedIndexName = dialect.QuoteForColumnName(dialect.FormatIndexName(indexName));
        var quotedColumns = string.Join(
            ", ",
            columnNames.Select(dialect.QuoteForColumnName));

        return $"CREATE UNIQUE INDEX {quotedIndexName} ON {quotedTableName} ({quotedColumns})";
    }

    /// <summary>
    /// Creates a unique index over the specified columns using dialect-aware quoting.
    /// </summary>
    /// <param name="schemaBuilder">The active schema builder.</param>
    /// <param name="store">The YesSql store.</param>
    /// <param name="indexType">The index type whose table receives the constraint.</param>
    /// <param name="indexName">The unqualified unique index name.</param>
    /// <param name="columnNames">The columns that participate in the unique constraint.</param>
    public static async Task CreateUniqueIndexAsync(
        ISchemaBuilder schemaBuilder,
        IStore store,
        Type indexType,
        string indexName,
        params string[] columnNames)
    {
        var quotedTableName = GetQuotedTableName(schemaBuilder, store, indexType);

        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = BuildCreateUniqueIndexStatement(
            schemaBuilder.Dialect,
            schemaBuilder.TablePrefix,
            quotedTableName,
            indexName,
            columnNames);
        await command.ExecuteNonQueryAsync();
    }
}
