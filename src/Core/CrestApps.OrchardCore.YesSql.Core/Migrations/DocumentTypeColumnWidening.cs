using System.Data.Common;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.YesSql.Core.Migrations;

/// <summary>
/// Widens the <c>Type</c> column of a collection's document table so a long document type name is stored rather
/// than refused.
/// </summary>
/// <remarks>
/// The data layer declares <c>Type</c> as a 255-character column and stores in it the full name of the document
/// type. A catalog persists its records as one <c>DictionaryDocument&lt;T&gt;</c>, whose name carries the record
/// type's assembly-qualified name, version and all: for a channel endpoint it is 256 characters, so SQL Server
/// refused every channel endpoint (error 2628) while SQLite, which enforces no length, stored it. Widening is a
/// schema change, not an additive one, but it only ever admits more: the values already stored are kept as they
/// are.
/// <para>
/// The column is altered in place rather than rebuilt, because the document table holds every document the tenant
/// has and copying it would rewrite the whole table. Widening a text column is an in-place change on every engine
/// that enforces the length. SQL Server refuses to alter a column an index refers to, so there the indexes over
/// <c>Type</c> come down first and are recreated as they were. PostgreSQL and MySQL keep their indexes through
/// the alter, and SQLite stores every text column as unbounded <c>TEXT</c>, so it has nothing to widen.
/// </para>
/// <para>
/// The current length is read first and a column already at least as wide is left alone, so the step is safe to
/// run from more than one feature and to repeat after an attempt that stopped part-way.
/// </para>
/// </remarks>
public static class DocumentTypeColumnWidening
{
    /// <summary>
    /// The length the <c>Type</c> column is widened to.
    /// </summary>
    public const int TypeLength = 512;

    private const string TypeColumnName = "Type";

    /// <summary>
    /// Widens the <c>Type</c> column of the collection's document table to <see cref="TypeLength"/>.
    /// </summary>
    /// <param name="schemaBuilder">The active schema builder.</param>
    /// <param name="store">The YesSql store.</param>
    /// <param name="collection">The collection whose document table is widened; empty for the default one.</param>
    /// <returns><see langword="true"/> when the column was widened; <see langword="false"/> when there was nothing to do.</returns>
    public static async Task<bool> WidenAsync(ISchemaBuilder schemaBuilder, IStore store, string collection = "")
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentNullException.ThrowIfNull(store);

        var schema = store.Configuration.Schema;
        var tableName = schemaBuilder.TablePrefix + schemaBuilder.TableNameConvention.GetDocumentTable(collection ?? string.Empty);
        var quotedTableName = schemaBuilder.Dialect.QuoteForTableName(tableName, schema);
        var quotedColumnName = schemaBuilder.Dialect.QuoteForColumnName(TypeColumnName);

        switch (schemaBuilder.Dialect.Name)
        {
            case "SqlServer":
                return await WidenOnSqlServerAsync(schemaBuilder, quotedTableName, quotedColumnName);

            case "PostgreSql":
                return await WidenOnPostgreSqlAsync(schemaBuilder, tableName, schema, quotedTableName, quotedColumnName);

            case "MySql":
                return await WidenOnMySqlAsync(schemaBuilder, tableName, schema, quotedTableName, quotedColumnName);

            default:
                // SQLite stores every text column as unbounded TEXT, so a long type name was never refused there.
                return false;
        }
    }

    private static async Task<bool> WidenOnPostgreSqlAsync(ISchemaBuilder schemaBuilder, string tableName, string schema, string quotedTableName, string quotedColumnName)
    {
        if (await ReadInformationSchemaLengthAsync(schemaBuilder, tableName, schema, "current_schema()") >= TypeLength)
        {
            return false;
        }

        await using var command = NewCommand(schemaBuilder);
        command.CommandText = $"ALTER TABLE {quotedTableName} ALTER COLUMN {quotedColumnName} TYPE varchar({TypeLength})";
        await command.ExecuteNonQueryAsync();

        return true;
    }

    private static async Task<bool> WidenOnMySqlAsync(ISchemaBuilder schemaBuilder, string tableName, string schema, string quotedTableName, string quotedColumnName)
    {
        if (await ReadInformationSchemaLengthAsync(schemaBuilder, tableName, schema, "DATABASE()") >= TypeLength)
        {
            return false;
        }

        await using var command = NewCommand(schemaBuilder);
        command.CommandText = $"ALTER TABLE {quotedTableName} MODIFY COLUMN {quotedColumnName} varchar({TypeLength}) NOT NULL";
        await command.ExecuteNonQueryAsync();

        return true;
    }

    private static async Task<bool> WidenOnSqlServerAsync(ISchemaBuilder schemaBuilder, string quotedTableName, string quotedColumnName)
    {
        // max_length is in bytes, two per character for NVARCHAR, and -1 for NVARCHAR(MAX).
        object maxLength;

        await using (var command = NewCommand(schemaBuilder, quotedTableName))
        {
            command.CommandText = "SELECT c.max_length FROM sys.columns c WHERE c.object_id = OBJECT_ID(@table) AND c.name = @column";
            maxLength = await command.ExecuteScalarAsync();
        }

        if (maxLength is null or DBNull)
        {
            return false;
        }

        var bytes = Convert.ToInt32(maxLength, System.Globalization.CultureInfo.InvariantCulture);

        if (bytes == -1 || bytes >= TypeLength * 2)
        {
            return false;
        }

        var indexes = await ReadSqlServerIndexesOnTypeAsync(schemaBuilder, quotedTableName);

        foreach (var index in indexes)
        {
            await using var command = NewCommand(schemaBuilder);
            command.CommandText = $"DROP INDEX {schemaBuilder.Dialect.QuoteForColumnName(index.Name)} ON {quotedTableName}";
            await command.ExecuteNonQueryAsync();
        }

        await using (var command = NewCommand(schemaBuilder))
        {
            command.CommandText = $"ALTER TABLE {quotedTableName} ALTER COLUMN {quotedColumnName} NVARCHAR({TypeLength}) NOT NULL";
            await command.ExecuteNonQueryAsync();
        }

        foreach (var index in indexes)
        {
            var columns = string.Join(", ", index.Columns.Select(schemaBuilder.Dialect.QuoteForColumnName));

            await using var command = NewCommand(schemaBuilder);
            command.CommandText = $"CREATE {(index.IsUnique ? "UNIQUE " : string.Empty)}INDEX {schemaBuilder.Dialect.QuoteForColumnName(index.Name)} ON {quotedTableName} ({columns})";
            await command.ExecuteNonQueryAsync();
        }

        return true;
    }

    // Every index whose key names the Type column, with its key columns in order, so each can be put back exactly.
    private static async Task<List<SqlServerIndex>> ReadSqlServerIndexesOnTypeAsync(ISchemaBuilder schemaBuilder, string quotedTableName)
    {
        var rows = new List<(string Name, bool IsUnique, string Column)>();

        await using (var command = NewCommand(schemaBuilder, quotedTableName))
        {
            command.CommandText = """
            SELECT i.name, i.is_unique, c.name
            FROM sys.indexes i
            INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id = OBJECT_ID(@table)
                AND i.is_primary_key = 0
                AND ic.is_included_column = 0
                AND i.index_id IN (
                    SELECT ic2.index_id FROM sys.index_columns ic2
                    INNER JOIN sys.columns c2 ON c2.object_id = ic2.object_id AND c2.column_id = ic2.column_id
                    WHERE ic2.object_id = i.object_id AND c2.name = @column)
            ORDER BY i.name, ic.key_ordinal
            """;

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                rows.Add((reader.GetString(0), reader.GetBoolean(1), reader.GetString(2)));
            }
        }

        return rows
            .GroupBy(row => row.Name, StringComparer.Ordinal)
            .Select(group => new SqlServerIndex(group.Key, group.First().IsUnique, group.Select(row => row.Column).ToArray()))
            .ToList();
    }

    private static async Task<long> ReadInformationSchemaLengthAsync(ISchemaBuilder schemaBuilder, string tableName, string schema, string currentSchemaExpression)
    {
        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText =
            "SELECT character_maximum_length FROM information_schema.columns " +
            "WHERE table_name = @table AND column_name = @column AND table_schema = " +
            (string.IsNullOrEmpty(schema) ? currentSchemaExpression : "@schema");

        AddParameter(command, "@table", tableName);
        AddParameter(command, "@column", TypeColumnName);

        if (!string.IsNullOrEmpty(schema))
        {
            AddParameter(command, "@schema", schema);
        }

        var result = await command.ExecuteScalarAsync();

        // A column that is not found, or has no declared length, is left alone.
        return result is null or DBNull
            ? long.MaxValue
            : Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    // A command on the migration's own connection and transaction; each caller sets its statement where it is read.
    private static DbCommand NewCommand(ISchemaBuilder schemaBuilder, string quotedTableName = null)
    {
        var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;

        if (quotedTableName is not null)
        {
            AddParameter(command, "@table", quotedTableName);
            AddParameter(command, "@column", TypeColumnName);
        }

        return command;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record SqlServerIndex(string Name, bool IsUnique, IReadOnlyList<string> Columns);
}
