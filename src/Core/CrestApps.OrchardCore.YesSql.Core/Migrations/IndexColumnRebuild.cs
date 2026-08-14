using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.YesSql.Core.Migrations;

/// <summary>
/// Converts an index column that was declared as text into the integer column an enum index property actually
/// writes, preserving the values already stored in it.
/// </summary>
/// <remarks>
/// YesSql persists an enum index property as its underlying integer whatever the column was declared as, so a
/// column declared as text holds integers written through a text column. SQLite reconciles the two through
/// column affinity and behaves correctly, which is why such a column can survive a long time unnoticed; an
/// engine that does not coerce rejects the write outright, so the same tenant that works on SQLite cannot run at
/// all on the engine production uses. Correcting the declaration is therefore a data-correctness fix rather than
/// a tidiness one.
/// <para>
/// The conversion is a rebuild rather than an <c>ALTER COLUMN</c> because SQLite has no <c>ALTER COLUMN</c> at
/// all, so an alter-based fix would be unavailable on the engine most tenants develop against. Add, copy, drop
/// and rename are supported everywhere and produce the same end state.
/// </para>
/// </remarks>
public static class IndexColumnRebuild
{
    private const string TemporarySuffix = "__rebuild";

    /// <summary>
    /// Rebuilds a text column as the integer column the specified enum requires, translating the values it holds.
    /// </summary>
    /// <remarks>
    /// A value that is neither a member's number nor a member's name becomes <see langword="null"/> rather than
    /// the enum's first member, because writing a real member over an unreadable value would turn a visible data
    /// problem into an invisible one.
    /// </remarks>
    /// <typeparam name="TIndex">The index whose table carries the column.</typeparam>
    /// <typeparam name="TEnum">The enum the column stores.</typeparam>
    /// <param name="schemaBuilder">The active schema builder.</param>
    /// <param name="store">The YesSql store.</param>
    /// <param name="columnName">The name of the column to rebuild.</param>
    /// <param name="collection">The collection the index table belongs to.</param>
    public static async Task RebuildAsEnumColumnAsync<TIndex, TEnum>(
        ISchemaBuilder schemaBuilder,
        IStore store,
        string columnName,
        string collection)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrEmpty(columnName);

        var tableName = schemaBuilder.TablePrefix +
            schemaBuilder.TableNameConvention.GetIndexTable(typeof(TIndex), collection);
        var quotedTableName = schemaBuilder.Dialect.QuoteForTableName(tableName, store.Configuration.Schema);

        var temporaryColumnName = columnName + TemporarySuffix;
        var declaredTypes = await ReadDeclaredColumnTypesAsync(schemaBuilder, quotedTableName);
        var hasReplacement = declaredTypes.ContainsKey(temporaryColumnName);

        if (declaredTypes.TryGetValue(columnName, out var declaredType))
        {
            if (declaredType != typeof(string))
            {
                return;
            }

            // A replacement that is already present was left behind by an attempt that stopped part-way, and
            // adding it again would fail every activation from here on.
            if (!hasReplacement)
            {
                await schemaBuilder.AlterIndexTableAsync(
                    typeof(TIndex),
                    table => table.AddColumn<TEnum>(temporaryColumnName),
                    collection);
            }

            await CopyValuesAsync<TEnum>(schemaBuilder, quotedTableName, columnName, temporaryColumnName);

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
        // the translated values and repeating the earlier steps would destroy them.
        await schemaBuilder.AlterIndexTableAsync(
            typeof(TIndex),
            table => table.RenameColumn(temporaryColumnName, columnName),
            collection);
    }

    /// <summary>
    /// Reads the declared type of every column in the table.
    /// </summary>
    /// <remarks>
    /// Two questions are answered by the same read. The rebuild translates text into numbers, so it is only
    /// correct against a text column: a column already declared as the enum's storage type needs nothing done to
    /// it, and running the translation over it anyway would compare a number against a string, which SQLite
    /// coerces and appears to work while an engine that resolves operators by type rejects the statement and the
    /// tenant fails to activate — the very failure this rebuild exists to prevent. The replacement column is
    /// looked for in the same read because MySQL commits each schema change on its own, so an interrupted attempt
    /// can leave the replacement behind, and adding it a second time would fail every subsequent activation.
    /// Types are read through the data reader rather than from an engine-specific catalog view, so the same probe
    /// works on every supported engine, and the query is written to match no rows because only declarations are
    /// wanted.
    /// </remarks>
    private static async Task<Dictionary<string, Type>> ReadDeclaredColumnTypesAsync(
        ISchemaBuilder schemaBuilder,
        string quotedTableName)
    {
        var declaredTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = $"SELECT * FROM {quotedTableName} WHERE 1 = 0";

        await using var reader = await command.ExecuteReaderAsync();

        for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
        {
            declaredTypes[reader.GetName(ordinal)] = reader.GetFieldType(ordinal);
        }

        return declaredTypes;
    }

    private static async Task CopyValuesAsync<TEnum>(
        ISchemaBuilder schemaBuilder,
        string quotedTableName,
        string columnName,
        string temporaryColumnName)
        where TEnum : struct, Enum
    {
        var quotedColumnName = schemaBuilder.Dialect.QuoteForColumnName(columnName);
        var quotedTemporaryColumnName = schemaBuilder.Dialect.QuoteForColumnName(temporaryColumnName);

        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;

        var branches = new List<string>();
        var parameterIndex = 0;

        foreach (var value in Enum.GetValues<TEnum>())
        {
            var number = Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);

            // The stored text is whatever the engine made of an integer written through a text column, which is
            // the member's number. A member's name is matched as well so a legacy row seeded by hand, or by an
            // engine that formatted the value differently, is carried across rather than discarded.
            foreach (var candidate in new[] { number.ToString(System.Globalization.CultureInfo.InvariantCulture), value.ToString() })
            {
                var parameterName = $"@RebuildValue{parameterIndex++}";
                var parameter = command.CreateParameter();
                parameter.ParameterName = parameterName;
                parameter.Value = candidate;
                command.Parameters.Add(parameter);

                branches.Add($"WHEN {quotedColumnName} = {parameterName} THEN {number}");
            }
        }

        command.CommandText =
            $"""
            UPDATE {quotedTableName}
            SET {quotedTemporaryColumnName} = CASE
                {string.Join(Environment.NewLine + "    ", branches)}
                ELSE NULL
            END
            """;

        await command.ExecuteNonQueryAsync();
    }
}
