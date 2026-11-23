using System.Reflection;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Payroll.Migrations;

/// <summary>
/// Copies the rows of an index table Payroll kept under its old, generic name (such as <c>Payroll_PayRunIndex</c>) into
/// the table of the same index under its module-specific name (such as <c>Payroll_PayrollPayRunIndex</c>), so a site
/// keeps its index rows without saving every document again. An application module that keeps its own index of the same
/// generic name in a collection named Payroll no longer shares Payroll's table.
/// </summary>
/// <remarks>
/// The new table is made first by the index's schema, and the old one is dropped by the migration step afterwards. The
/// copy is one set-based statement on the migration's own connection and transaction, so it runs on every database
/// YesSql supports. The columns are the index type's properties, which are the columns YesSql writes, and every one of
/// them was in the old table.
/// </remarks>
internal static class PayrollIndexTableMove
{
    /// <summary>
    /// Makes the index table of <paramref name="indexType"/> when it is missing and copies the rows of the old table into
    /// it when the old table is there. The caller drops the old table when this returns <see langword="true"/>.
    /// </summary>
    /// <param name="schemaBuilder">The migration's schema builder.</param>
    /// <param name="store">The store, for the database schema.</param>
    /// <param name="legacyTable">The old table's name in the Payroll collection, without the table prefix.</param>
    /// <param name="indexType">The index type whose table receives the rows.</param>
    /// <param name="createTables">Makes the new table, with the other tables of its schema that are missing too.</param>
    /// <returns><see langword="true"/> when the old table was there and its rows were copied.</returns>
    public static async Task<bool> MoveAsync(ISchemaBuilder schemaBuilder, IStore store, string legacyTable, Type indexType, Func<ISchemaBuilder, Task> createTables)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentNullException.ThrowIfNull(createTables);

        // A site that came through an older step after the rename already has the new table: an older step makes its
        // tables with the schema of today.
        if (!await TableExistence.ExistsAsync(schemaBuilder, schemaBuilder.TableNameConvention.GetIndexTable(indexType, PayrollConstants.CollectionName)))
        {
            await createTables(schemaBuilder);
        }

        if (!await TableExistence.ExistsAsync(schemaBuilder, legacyTable))
        {
            return false;
        }

        await CopyRowsAsync(schemaBuilder, store, legacyTable, indexType);

        return true;
    }

    /// <summary>
    /// Copies every row of the old table into the index table of <paramref name="indexType"/>.
    /// </summary>
    /// <param name="schemaBuilder">The migration's schema builder.</param>
    /// <param name="store">The store, for the database schema.</param>
    /// <param name="legacyTable">The old table's name in the Payroll collection, without the table prefix.</param>
    /// <param name="indexType">The index type whose table receives the rows.</param>
    /// <returns>A task.</returns>
    public static Task CopyRowsAsync(ISchemaBuilder schemaBuilder, IStore store, string legacyTable, Type indexType)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrEmpty(legacyTable);
        ArgumentNullException.ThrowIfNull(indexType);

        var dialect = schemaBuilder.Dialect;
        var target = IndexMigrationSql.GetQuotedTableName(schemaBuilder, store, indexType, PayrollConstants.CollectionName);
        var source = dialect.QuoteForTableName(schemaBuilder.TablePrefix + legacyTable, store.Configuration.Schema);
        var columns = string.Join(", ", ColumnsOf(indexType).Select(dialect.QuoteForColumnName));

        return IndexMigrationSql.ExecuteAsync(schemaBuilder, $"INSERT INTO {target} ({columns}) SELECT {columns} FROM {source}");
    }

    /// <summary>
    /// Gets the columns of an index table YesSql writes: the document id and each property of the index, except its own id.
    /// </summary>
    /// <param name="indexType">The index type.</param>
    /// <returns>The column names.</returns>
    public static IReadOnlyList<string> ColumnsOf(Type indexType)
    {
        ArgumentNullException.ThrowIfNull(indexType);

        return
        [
            "DocumentId",
            .. indexType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.CanRead && property.CanWrite && property.Name is not "Id" and not "DocumentId")
                .Select(property => property.Name),
        ];
    }
}
