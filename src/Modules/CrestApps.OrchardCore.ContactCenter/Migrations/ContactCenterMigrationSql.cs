using CrestApps.OrchardCore.YesSql.Core.Migrations;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// The shared index migration SQL helpers (<see cref="IndexMigrationSql"/>), bound to the Contact Center collection.
/// </summary>
internal static class ContactCenterMigrationSql
{
    /// <inheritdoc cref="IndexMigrationSql.GetQuotedTableName"/>
    public static string GetQuotedTableName(ISchemaBuilder schemaBuilder, IStore store, Type indexType)
        => IndexMigrationSql.GetQuotedTableName(schemaBuilder, store, indexType, ContactCenterStorage.CollectionName);

    /// <inheritdoc cref="IndexMigrationSql.AddRetentionColumnAsync"/>
    public static Task AddRetentionColumnAsync(
        ISchemaBuilder schemaBuilder,
        IStore store,
        Type indexType,
        string columnName,
        DateTime backfillUtc,
        string settledRowsFilter = null)
        => IndexMigrationSql.AddRetentionColumnAsync(schemaBuilder, store, indexType, ContactCenterStorage.CollectionName, columnName, backfillUtc, settledRowsFilter);

    /// <inheritdoc cref="IndexMigrationSql.BuildConcat"/>
    public static string BuildConcat(ISqlDialect dialect, params string[] fragments)
        => IndexMigrationSql.BuildConcat(dialect, fragments);

    /// <inheritdoc cref="IndexMigrationSql.ExecuteAsync"/>
    public static Task ExecuteAsync(ISchemaBuilder schemaBuilder, string commandText, params (string Name, object Value)[] parameters)
        => IndexMigrationSql.ExecuteAsync(schemaBuilder, commandText, parameters);

    /// <inheritdoc cref="IndexMigrationSql.ExistsAsync"/>
    public static Task<bool> ExistsAsync(ISchemaBuilder schemaBuilder, string commandText, params (string Name, object Value)[] parameters)
        => IndexMigrationSql.ExistsAsync(schemaBuilder, commandText, parameters);

    /// <inheritdoc cref="IndexMigrationSql.BuildCreateUniqueIndexStatement"/>
    public static string BuildCreateUniqueIndexStatement(ISqlDialect dialect, string tablePrefix, string quotedTableName, string indexName, params string[] columnNames)
        => IndexMigrationSql.BuildCreateUniqueIndexStatement(dialect, tablePrefix, quotedTableName, indexName, columnNames);

    /// <inheritdoc cref="IndexMigrationSql.CreateUniqueIndexAsync"/>
    public static Task CreateUniqueIndexAsync(ISchemaBuilder schemaBuilder, IStore store, Type indexType, string indexName, params string[] columnNames)
        => IndexMigrationSql.CreateUniqueIndexAsync(schemaBuilder, store, indexType, ContactCenterStorage.CollectionName, indexName, columnNames);
}
