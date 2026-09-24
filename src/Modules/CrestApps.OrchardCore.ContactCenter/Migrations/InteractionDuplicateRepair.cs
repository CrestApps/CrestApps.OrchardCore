using System.Data.Common;
using System.Reflection;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Merges the interactions an earlier defect stored as more than one document into one document each.
/// </summary>
/// <remarks>
/// Accepting an offer committed the unit of work part-way through, and the interaction saved after that commit was
/// inserted as a second document with the same id. The repair runs on the migration's own connection and
/// transaction: a second connection opened inside a migration step waits on the lock the step's transaction already
/// holds, which on SQLite never resolves. Each copy's document is read, the copies are merged into the newest one
/// (<see cref="InteractionDuplicateMerge"/>), the kept document and its index row are rewritten, and the other copies'
/// documents and index rows are deleted. Only interactions that have copies are touched, so a tenant without any
/// pays one grouped query.
/// </remarks>
internal static class InteractionDuplicateRepair
{
    private static readonly PropertyInfo[] _indexColumns = typeof(InteractionIndex)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(property =>
            property.CanRead &&
            property.Name is not nameof(InteractionIndex.Id) and not nameof(InteractionIndex.DocumentId))
        .ToArray();

    /// <summary>
    /// Merges every interaction stored as more than one document.
    /// </summary>
    /// <param name="schemaBuilder">The migration's schema builder, whose connection and transaction the repair uses.</param>
    /// <param name="store">The store, for its table naming and content serializer.</param>
    /// <returns>The number of interactions that were merged.</returns>
    public static async Task<int> MergeDuplicatesAsync(ISchemaBuilder schemaBuilder, IStore store)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentNullException.ThrowIfNull(store);

        var dialect = schemaBuilder.Dialect;
        var indexTable = ContactCenterMigrationSql.GetQuotedTableName(schemaBuilder, store, typeof(InteractionIndex));
        var documentTable = dialect.QuoteForTableName(
            schemaBuilder.TablePrefix + schemaBuilder.TableNameConvention.GetDocumentTable(ContactCenterStorage.CollectionName),
            store.Configuration.Schema);
        var itemIdColumn = dialect.QuoteForColumnName("ItemId");
        var documentIdColumn = dialect.QuoteForColumnName("DocumentId");
        var idColumn = dialect.QuoteForColumnName("Id");
        var contentColumn = dialect.QuoteForColumnName("Content");
        var versionColumn = dialect.QuoteForColumnName("Version");

        var copiesByItemId = new Dictionary<string, List<long>>(StringComparer.Ordinal);

        await using (var command = CreateCommand(
            schemaBuilder,
            $"""
            SELECT {itemIdColumn}, {documentIdColumn}
            FROM {indexTable}
            WHERE {itemIdColumn} IN (
                SELECT {itemIdColumn}
                FROM {indexTable}
                WHERE {itemIdColumn} IS NOT NULL AND {itemIdColumn} <> ''
                GROUP BY {itemIdColumn}
                HAVING COUNT(DISTINCT {documentIdColumn}) > 1)
            """))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var itemId = reader.GetString(0);
                var documentId = Convert.ToInt64(reader.GetValue(1));

                if (!copiesByItemId.TryGetValue(itemId, out var documentIds))
                {
                    copiesByItemId[itemId] = documentIds = [];
                }

                if (!documentIds.Contains(documentId))
                {
                    documentIds.Add(documentId);
                }
            }
        }

        var serializer = store.Configuration.ContentSerializer;

        foreach (var documentIds in copiesByItemId.Values)
        {
            var copies = new List<(long DocumentId, Interaction Interaction)>();

            foreach (var documentId in documentIds)
            {
                await using var command = CreateCommand(
                    schemaBuilder,
                    $"SELECT {contentColumn} FROM {documentTable} WHERE {idColumn} = @id",
                    ("@id", documentId));

                if (await command.ExecuteScalarAsync() is string content &&
                    serializer.Deserialize(content, typeof(Interaction)) is Interaction interaction)
                {
                    copies.Add((documentId, interaction));
                }
            }

            if (copies.Count < 2)
            {
                continue;
            }

            // The copy written last carries what happened to the call most recently; a tie keeps the later document.
            var ordered = copies
                .OrderByDescending(copy => copy.Interaction.ModifiedUtc ?? copy.Interaction.CreatedUtc)
                .ThenByDescending(copy => copy.DocumentId)
                .ToArray();
            var kept = ordered[0];
            var merged = InteractionDuplicateMerge.Merge([.. ordered.Select(copy => copy.Interaction)]);

            await ExecuteAsync(
                schemaBuilder,
                $"UPDATE {documentTable} SET {contentColumn} = @content, {versionColumn} = {versionColumn} + 1 WHERE {idColumn} = @id",
                ("@content", serializer.Serialize(merged)),
                ("@id", kept.DocumentId));

            await UpdateIndexRowAsync(schemaBuilder, indexTable, documentIdColumn, kept.DocumentId, merged);

            foreach (var removed in ordered.Skip(1))
            {
                await DeleteIndexRowAsync(schemaBuilder, indexTable, documentIdColumn, removed.DocumentId);
                await DeleteDocumentAsync(schemaBuilder, documentTable, idColumn, removed.DocumentId);
            }
        }

        return copiesByItemId.Count;
    }

    // The copy's content is already in the kept document; only its index row goes.
    private static Task DeleteIndexRowAsync(ISchemaBuilder schemaBuilder, string indexTable, string documentIdColumn, long documentId)
        => ExecuteAsync(
            schemaBuilder,
            $"DELETE FROM {indexTable} WHERE {documentIdColumn} = @id",
            ("@id", documentId));

    // The copy's content is already in the kept document; only the copy itself goes.
    private static Task DeleteDocumentAsync(ISchemaBuilder schemaBuilder, string documentTable, string idColumn, long documentId)
        => ExecuteAsync(
            schemaBuilder,
            $"DELETE FROM {documentTable} WHERE {idColumn} = @id",
            ("@id", documentId));

    private static async Task UpdateIndexRowAsync(
        ISchemaBuilder schemaBuilder,
        string indexTable,
        string documentIdColumn,
        long documentId,
        Interaction merged)
    {
        // The row is rebuilt from the same mapping the index provider writes, so the kept row answers queries for
        // the merged record rather than for the copy it was before.
        var index = InteractionIndexProvider.ToIndex(merged);
        var assignments = _indexColumns
            .Select((property, position) => $"{schemaBuilder.Dialect.QuoteForColumnName(property.Name)} = @p{position}");
        var parameters = _indexColumns
            .Select((property, position) => ($"@p{position}", ToParameterValue(property.GetValue(index))))
            .Append(("@documentId", (object)documentId))
            .ToArray();

        await ExecuteAsync(
            schemaBuilder,
            $"UPDATE {indexTable} SET {string.Join(", ", assignments)} WHERE {documentIdColumn} = @documentId",
            parameters);
    }

    private static object ToParameterValue(object value)
        => value switch
        {
            null => DBNull.Value,
            Enum enumValue => Convert.ToInt32(enumValue, System.Globalization.CultureInfo.InvariantCulture),
            _ => value,
        };

    private static async Task ExecuteAsync(ISchemaBuilder schemaBuilder, string commandText, params (string Name, object Value)[] parameters)
    {
        await using var command = CreateCommand(schemaBuilder, commandText, parameters);
        await command.ExecuteNonQueryAsync();
    }

    private static DbCommand CreateCommand(ISchemaBuilder schemaBuilder, string commandText, params (string Name, object Value)[] parameters)
    {
        var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = commandText;

        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        return command;
    }
}
