using System.Data.Common;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Models;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tickets.Migrations;

/// <summary>
/// Moves the service desk's own records from the default collection to the service desk's
/// (<see cref="TicketsConstants.CollectionName"/>).
/// </summary>
/// <remarks>
/// The protected information, the requester e-mail outbox, the backlog snapshots and the time-elapsed markers were
/// documents of the default collection; each is read as JSON and saved again in the service desk's collection, in the
/// order it was stored, and the old document is deleted. Nothing refers to these documents by their document id, so a new
/// one is fine. The desks, statuses, priorities, saved views and boards were kept as one dictionary document per kind;
/// each record is saved as a document of its own, with the same item id, unless the collection already has one, and the
/// dictionary document is deleted. Running the move again finds nothing left to move. Everything runs on the migration's
/// own connection and transaction: a second connection opened inside a migration step waits on the lock the step already
/// holds, which on SQLite never resolves.
/// </remarks>
internal static class TicketServiceDeskMove
{
    /// <summary>
    /// Moves the records.
    /// </summary>
    /// <param name="schemaBuilder">The migration's schema builder, whose connection and transaction the move uses.</param>
    /// <param name="store">The store, for its table naming, type names and content serializer.</param>
    /// <param name="session">The migration's session, which saves the moved records.</param>
    /// <returns>The number of records moved.</returns>
    public static async Task<int> MoveAsync(ISchemaBuilder schemaBuilder, IStore store, ISession session)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(session);

        var move = new Mover(schemaBuilder, store, session);

        return await move.DocumentsAsync<TicketProtectedItemDocument>()
            + await move.DocumentsAsync<TicketEmailOutboxItem>()
            + await move.DocumentsAsync<TicketBacklogDay>()
            + await move.DocumentsAsync<TicketTimeElapsedMark>()
            + await move.RecordsAsync<TicketDesk, TicketDeskIndex>()
            + await move.RecordsAsync<TicketStatus, TicketStatusIndex>()
            + await move.RecordsAsync<TicketPriority, TicketPriorityIndex>()
            + await move.RecordsAsync<TicketView, TicketViewIndex>()
            + await move.RecordsAsync<TicketBoard, TicketBoardIndex>();
    }

    private sealed class Mover
    {
        private readonly ISchemaBuilder _schemaBuilder;
        private readonly IStore _store;
        private readonly ISession _session;
        private readonly string _documentTable;
        private readonly string _idColumn;
        private readonly string _typeColumn;
        private readonly string _contentColumn;

        public Mover(ISchemaBuilder schemaBuilder, IStore store, ISession session)
        {
            _schemaBuilder = schemaBuilder;
            _store = store;
            _session = session;

            var dialect = schemaBuilder.Dialect;

            _documentTable = dialect.QuoteForTableName(
                schemaBuilder.TablePrefix + schemaBuilder.TableNameConvention.GetDocumentTable(string.Empty),
                store.Configuration.Schema);
            _idColumn = dialect.QuoteForColumnName("Id");
            _typeColumn = dialect.QuoteForColumnName("Type");
            _contentColumn = dialect.QuoteForColumnName("Content");
        }

        // A document of the default collection becomes one of the service desk's collection.
        public async Task<int> DocumentsAsync<T>()
            where T : class
        {
            var rows = await ReadAsync(_store.TypeNames[typeof(T)]);

            foreach (var (_, content) in rows)
            {
                if (_store.Configuration.ContentSerializer.Deserialize(content, typeof(T)) is T document)
                {
                    // The old document id came with the content; the document gets a new one in the collection.
                    ClearDocumentId(document);
                    await _session.SaveAsync(document, false, TicketsConstants.CollectionName);
                }
            }

            await DeleteAsync(rows);

            return rows.Count;
        }

        // A dictionary document of records becomes a document per record; one the collection already has is kept.
        public async Task<int> RecordsAsync<T, TIndex>()
            where T : CatalogItem
            where TIndex : TicketSettingsRecordIndex
        {
            var rows = await ReadAsync(_store.TypeNames[typeof(DictionaryDocument<T>)]);

            if (rows.Count == 0)
            {
                return 0;
            }

            var existing = (await _session.QueryIndex<TIndex>(TicketsConstants.CollectionName).ListAsync())
                .Select(index => index.ItemId)
                .ToHashSet(StringComparer.Ordinal);
            var moved = 0;

            foreach (var (_, content) in rows)
            {
                if (JsonNode.Parse(content)?["Records"] is not JsonObject records)
                {
                    continue;
                }

                foreach (var (key, value) in records)
                {
                    if (value is not JsonObject json ||
                        _store.Configuration.ContentSerializer.Deserialize(json.ToJsonString(), typeof(T)) is not T record)
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(record.ItemId))
                    {
                        record.ItemId = key;
                    }

                    if (!existing.Add(record.ItemId))
                    {
                        continue;
                    }

                    await _session.SaveAsync(record, false, TicketsConstants.CollectionName);
                    moved++;
                }
            }

            await DeleteAsync(rows);

            return moved;
        }

        private static void ClearDocumentId(object document)
        {
            if (document.GetType().GetProperty("Id") is { CanWrite: true } property && property.PropertyType.IsValueType)
            {
                property.SetValue(document, Activator.CreateInstance(property.PropertyType));
            }
        }

        private async Task<List<(long Id, string Content)>> ReadAsync(string typeName)
        {
            var rows = new List<(long, string)>();

            await using var command = _schemaBuilder.Connection.CreateCommand();

            command.Transaction = _schemaBuilder.Transaction;
            command.CommandText = $"SELECT {_idColumn}, {_contentColumn} FROM {_documentTable} WHERE {_typeColumn} = @type ORDER BY {_idColumn}";
            AddParameter(command, "@type", typeName);

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                rows.Add((Convert.ToInt64(reader.GetValue(0)), reader.GetString(1)));
            }

            return rows;
        }

        // Only the rows that were read go, so a document saved meanwhile in the default collection isn't lost.
        private async Task DeleteAsync(List<(long Id, string Content)> rows)
        {
            foreach (var (id, _) in rows)
            {
                await using var command = _schemaBuilder.Connection.CreateCommand();

                command.Transaction = _schemaBuilder.Transaction;
                command.CommandText = $"DELETE FROM {_documentTable} WHERE {_idColumn} = @id";
                AddParameter(command, "@id", id);

                await command.ExecuteNonQueryAsync();
            }
        }

        private static void AddParameter(DbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();

            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
    }
}
