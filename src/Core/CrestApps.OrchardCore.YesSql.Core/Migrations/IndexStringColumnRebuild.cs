using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.YesSql.Core.Migrations;

/// <summary>
/// Widens the declared length of a text index column, preserving the values already stored in it.
/// </summary>
/// <remarks>
/// Widening a column is a schema change, not an additive one: it alters an existing column so that a value that
/// outgrew the declared length is admitted rather than refused. An engine that enforces the length rejects an
/// over-length write outright (PostgreSQL <c>22001</c>, SQL Server <c>8152</c>, MySQL <c>1406</c> under the
/// default strict mode); only non-strict MySQL truncates. Widening is forward-only and repairs no row already
/// rejected or truncated under the narrow length. SQLite has no <c>ALTER COLUMN</c>
/// and stores every text column as unbounded <c>TEXT</c>, so a length is neither enforced nor changeable in
/// place there; the engines production uses (SQL Server, PostgreSQL, MySQL) do enforce it. Add, copy, drop and
/// rename is therefore used rather than an alter, because it is the one sequence available on every supported
/// engine and produces the same end state everywhere: on SQLite it re-creates the same unbounded column, and on
/// an enforcing engine it re-creates the column at the wider declared length.
/// <para>
/// The rebuild is written to resume rather than restart, because MySQL commits each schema change on its own, so
/// an attempt that stopped part-way can leave the replacement column behind. Re-running then finds the
/// replacement already present and finishes the sequence instead of adding it a second time and failing every
/// activation from there on. The value copy is a straight assignment, so re-running it over the same rows is
/// harmless.
/// </para>
/// </remarks>
public static class IndexStringColumnRebuild
{
    private const string TemporarySuffix = "__widen";

    // Only SQL Server materializes a column's declared default as a separately named constraint. On PostgreSQL
    // and MySQL the default is part of the column definition and is removed with the column; SQLite enforces no
    // length and rebuilds the whole table for any column change. So the pre-drop of the default constraint is
    // needed on SQL Server alone.
    private const string SqlServerDialectName = "SqlServer";

    /// <summary>
    /// Rebuilds a text column at a wider declared length, carrying its current values across unchanged.
    /// </summary>
    /// <typeparam name="TIndex">The index whose table carries the column.</typeparam>
    /// <param name="schemaBuilder">The active schema builder.</param>
    /// <param name="store">The YesSql store.</param>
    /// <param name="columnName">The name of the column to widen.</param>
    /// <param name="length">The wider declared length the column must carry after the rebuild.</param>
    /// <param name="isNotNull">Whether the rebuilt column refuses null, matching the fresh-install declaration.</param>
    /// <param name="defaultValue">The default the rebuilt column declares, or <see langword="null"/> when it declares none. A not-null column added to a table that already has rows needs one so the add succeeds.</param>
    /// <param name="collection">The collection the index table belongs to.</param>
    public static async Task WidenAsync<TIndex>(
        ISchemaBuilder schemaBuilder,
        IStore store,
        string columnName,
        int length,
        bool isNotNull,
        object defaultValue,
        string collection)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrEmpty(columnName);

        var tableName = schemaBuilder.TablePrefix +
            schemaBuilder.TableNameConvention.GetIndexTable(typeof(TIndex), collection);
        var quotedTableName = schemaBuilder.Dialect.QuoteForTableName(tableName, store.Configuration.Schema);

        var temporaryColumnName = columnName + TemporarySuffix;
        var columnNames = await ReadColumnNamesAsync(schemaBuilder, quotedTableName);
        var hasOriginal = columnNames.Contains(columnName);
        var hasReplacement = columnNames.Contains(temporaryColumnName);

        if (hasOriginal)
        {
            // A replacement that is already present was left behind by an attempt that stopped part-way, and
            // adding it again would fail every activation from here on.
            if (!hasReplacement)
            {
                await schemaBuilder.AlterIndexTableAsync(
                    typeof(TIndex),
                    table => table.AddColumn<string>(temporaryColumnName, column =>
                    {
                        column.WithLength(length);

                        if (isNotNull)
                        {
                            column.NotNull();
                        }

                        if (defaultValue is not null)
                        {
                            column.WithDefault(defaultValue);
                        }
                    }),
                    collection);
            }

            // Re-read what physically exists before copying into the replacement. In normal execution the add
            // above has just created it, so the copy runs. Under a pass that records schema declarations without
            // executing them the replacement is not there; dropping the original now would destroy it and its
            // data with no replacement to rename into its place, so the table is left exactly as it is. That a
            // recording pass declares nothing further here is harmless: it captures the finished column identity
            // from the create step and never observes this rebuild at all.
            var physicalColumnNames = await ReadColumnNamesAsync(schemaBuilder, quotedTableName);

            if (!physicalColumnNames.Contains(temporaryColumnName))
            {
                return;
            }

            await CopyValuesAsync(schemaBuilder, quotedTableName, columnName, temporaryColumnName);

            // SQL Server refuses to drop a column while a default constraint still references it, and the
            // original column declared a default when it was added, so the auto-named constraint SQL Server
            // created for it comes down first. The replacement column re-declares the same default in the
            // AddColumn above and carries it through the rename below, so the finished column keeps the default
            // the fresh install declares. This is a no-op on every other engine and whenever the column never
            // declared a default. The drop is issued inline here, in the same method as the replacement that puts
            // the default back, so the additive-only guard reads the removal and its restoration together.
            if (string.Equals(schemaBuilder.Dialect.Name, SqlServerDialectName, StringComparison.OrdinalIgnoreCase))
            {
                var defaultConstraintName = await ReadSqlServerColumnDefaultNameAsync(schemaBuilder, quotedTableName, columnName);

                if (defaultConstraintName is not null)
                {
                    var quotedConstraintName = schemaBuilder.Dialect.QuoteForColumnName(defaultConstraintName);

                    await using var dropDefaultCommand = schemaBuilder.Connection.CreateCommand();
                    dropDefaultCommand.Transaction = schemaBuilder.Transaction;
                    dropDefaultCommand.CommandText = "alter table " + quotedTableName + " drop constraint " + quotedConstraintName;

                    await dropDefaultCommand.ExecuteNonQueryAsync();
                }
            }

            await schemaBuilder.AlterIndexTableAsync(
                typeof(TIndex),
                table => table.DropColumn(columnName),
                collection);
        }
        else if (!hasReplacement)
        {
            return;
        }

        // Reached either straight from the copy above, or by an attempt that stopped after the original column
        // was dropped and before the replacement took its name; in the second case the replacement already holds
        // the copied values and repeating the earlier steps would destroy them.
        await schemaBuilder.AlterIndexTableAsync(
            typeof(TIndex),
            table => table.RenameColumn(temporaryColumnName, columnName),
            collection);
    }

    /// <summary>
    /// Reads the name of every column in the table.
    /// </summary>
    /// <remarks>
    /// The replacement column is looked for so an interrupted attempt resumes rather than adding it twice, and
    /// the original is looked for so a run that already dropped it renames the replacement instead of failing.
    /// Names are read through the data reader rather than an engine-specific catalog view, so the same probe
    /// works on every supported engine, and the query is written to match no rows because only the shape is
    /// wanted.
    /// </remarks>
    private static async Task<HashSet<string>> ReadColumnNamesAsync(
        ISchemaBuilder schemaBuilder,
        string quotedTableName)
    {
        var columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = $"SELECT * FROM {quotedTableName} WHERE 1 = 0";

        await using var reader = await command.ExecuteReaderAsync();

        for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
        {
            columnNames.Add(reader.GetName(ordinal));
        }

        return columnNames;
    }

    private static async Task CopyValuesAsync(
        ISchemaBuilder schemaBuilder,
        string quotedTableName,
        string columnName,
        string temporaryColumnName)
    {
        var quotedColumnName = schemaBuilder.Dialect.QuoteForColumnName(columnName);
        var quotedTemporaryColumnName = schemaBuilder.Dialect.QuoteForColumnName(temporaryColumnName);

        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = $"UPDATE {quotedTableName} SET {quotedTemporaryColumnName} = {quotedColumnName}";

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Reads the name SQL Server gave the default constraint bound to a column, or <see langword="null"/> when
    /// the column carries none.
    /// </summary>
    /// <remarks>
    /// The name is auto-generated when a defaulted column is added, so it cannot be spelled ahead of time and is
    /// read from the catalog. The lookup is parameterized, and the table is resolved through <c>OBJECT_ID</c> so
    /// a tenant whose tables live in a named schema is matched by the same quoted name the rest of the rebuild
    /// uses.
    /// </remarks>
    /// <param name="schemaBuilder">The active schema builder.</param>
    /// <param name="quotedTableName">The quoted, schema-qualified name of the table the column belongs to.</param>
    /// <param name="columnName">The name of the column whose default constraint name is read.</param>
    private static async Task<string> ReadSqlServerColumnDefaultNameAsync(
        ISchemaBuilder schemaBuilder,
        string quotedTableName,
        string columnName)
    {
        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText =
            "SELECT dc.name FROM sys.default_constraints dc " +
            "INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id " +
            "WHERE dc.parent_object_id = OBJECT_ID(@table) AND c.name = @column";

        var tableParameter = command.CreateParameter();
        tableParameter.ParameterName = "@table";
        tableParameter.Value = quotedTableName;
        command.Parameters.Add(tableParameter);

        var columnParameter = command.CreateParameter();
        columnParameter.ParameterName = "@column";
        columnParameter.Value = columnName;
        command.Parameters.Add(columnParameter);

        var result = await command.ExecuteScalarAsync();

        return result as string;
    }
}
