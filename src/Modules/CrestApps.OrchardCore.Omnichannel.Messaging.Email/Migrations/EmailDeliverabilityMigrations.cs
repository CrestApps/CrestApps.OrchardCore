using System.Globalization;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Migrations;

/// <summary>
/// Creates the tables of the delivery log, the suppression list and each address's sending state.
/// </summary>
internal sealed class EmailDeliverabilityMigrations : DataMigration
{
    /// <summary>
    /// The unique index that keeps an address to one sending state, so two processes cannot each create one and a
    /// change written to one be lost when the other is read.
    /// </summary>
    internal const string SendingStateAddressUniqueIndexName = "UQ_EmailSendingState_Address";

    /// <summary>
    /// The unique index that keeps an address on the suppression list once, so two reports suppressing it at the same
    /// moment cannot each add a record. The delivery log's event id gets no such index: its send entries carry no event
    /// id, and SQL Server lets a unique index hold only one empty value, so the processor's check stays the guard there.
    /// </summary>
    internal const string SuppressionAddressUniqueIndexName = "UQ_EmailSuppression_Address";

    private const string Collection = EmailChannelConstants.DeliverabilityCollectionName;

    private readonly IStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailDeliverabilityMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public EmailDeliverabilityMigrations(IStore store)
    {
        _store = store;
    }

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<EmailDeliveryLogIndex>(table => table
            .Column<string>("AddressId", column => column.WithLength(EmailDeliveryLogIndex.AddressIdLength))
            .Column<int>("Kind")
            .Column<string>("Recipient", column => column.WithLength(EmailDeliveryLogIndex.RecipientLength))
            .Column<string>("RecipientDomain", column => column.WithLength(EmailDeliveryLogIndex.RecipientLength))
            .Column<string>("MessageId", column => column.WithLength(EmailDeliveryLogIndex.MessageIdLength))
            .Column<string>("EventId", column => column.WithLength(EmailDeliveryLogIndex.EventIdLength))
            .Column<bool>("IsBulk")
            .Column<bool>("IsUnconfirmed", column => column.NotNull().WithDefault(false))
            .Column<DateTime>("OccurredUtc", column => column.NotNull()),
            collection: Collection);

        await SchemaBuilder.AlterIndexTableAsync<EmailDeliveryLogIndex>(table =>
        {
            // Sending limits and health count one address's entries of one kind in a time window.
            table.CreateIndex("IDX_EmailDeliveryLog_Address", "AddressId", "Kind", "OccurredUtc", "DocumentId");

            // Per-domain limits and deferrals narrow that to one receiving domain.
            table.CreateIndex("IDX_EmailDeliveryLog_Domain", "AddressId", "RecipientDomain", "Kind", "OccurredUtc");

            // Soft bounces are counted per recipient, and an event is traced to its sender by recipient.
            table.CreateIndex("IDX_EmailDeliveryLog_Recipient", "Recipient", "Kind", "OccurredUtc");

            table.CreateIndex("IDX_EmailDeliveryLog_Message", "MessageId", "Kind");
            table.CreateIndex("IDX_EmailDeliveryLog_Event", "EventId");
            table.CreateIndex("IDX_EmailDeliveryLog_Retention", "OccurredUtc", "DocumentId");
        },
            collection: Collection);

        await SchemaBuilder.CreateMapIndexTableAsync<EmailSuppressionIndex>(table => table
            .Column<string>("Address", column => column.WithLength(EmailDeliveryLogIndex.RecipientLength))
            .Column<int>("Reason")
            .Column<DateTime>("CreatedUtc", column => column.NotNull()),
            collection: Collection);

        await SchemaBuilder.AlterIndexTableAsync<EmailSuppressionIndex>(table =>
        {
            table.CreateIndex("IDX_EmailSuppression_Address", "Address", "DocumentId");
            table.CreateIndex("IDX_EmailSuppression_Created", "CreatedUtc", "DocumentId");
        },
            collection: Collection);

        await SchemaBuilder.CreateMapIndexTableAsync<EmailSendingStateIndex>(table => table
            .Column<string>("AddressId", column => column.WithLength(EmailDeliveryLogIndex.AddressIdLength)),
            collection: Collection);

        await SchemaBuilder.AlterIndexTableAsync<EmailSendingStateIndex>(table => table
            .CreateIndex("IDX_EmailSendingState_Address", "AddressId", "DocumentId"),
            collection: Collection);

        await CreateSendingStateUniqueIndexAsync();
        await CreateSuppressionUniqueIndexAsync();

        return 4;
    }

    /// <summary>
    /// Keeps each address to one sending state: deletes every record of an address but its oldest, the one the store
    /// reads, then makes the address unique. Two processes could each create a record for an address, and a change
    /// written to the one not read was lost.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await RemoveDuplicateSendingStatesAsync();
        await CreateSendingStateUniqueIndexAsync();

        return 2;
    }

    /// <summary>
    /// Keeps each address on the suppression list once: deletes every record of an address but its oldest, which holds
    /// the first reason, then makes the address unique. Two reports suppressing an address at the same moment could each
    /// add a record.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await RemoveDuplicateSuppressionsAsync();
        await CreateSuppressionUniqueIndexAsync();

        return 3;
    }

    /// <summary>
    /// Marks the delivery log entries that record a report tied to its email only by the recipient, so they count toward
    /// no suppression or domain backoff. Entries logged before are taken as confirmed.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom3Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<EmailDeliveryLogIndex>(table => table
            .AddColumn<bool>("IsUnconfirmed", column => column.NotNull().WithDefault(false)),
            collection: Collection);

        return 4;
    }

    private async Task CreateSuppressionUniqueIndexAsync()
    {
        // A second app instance activating the tenant at the same time may have created it already.
        if (await IndexTableIndexCreation.ExistsAsync(SchemaBuilder, typeof(EmailSuppressionIndex), SuppressionAddressUniqueIndexName, Collection))
        {
            return;
        }

        await IndexMigrationSql.CreateUniqueIndexAsync(
            SchemaBuilder,
            _store,
            typeof(EmailSuppressionIndex),
            Collection,
            SuppressionAddressUniqueIndexName,
            "Address");
    }

    private async Task RemoveDuplicateSuppressionsAsync()
    {
        var dialect = SchemaBuilder.Dialect;
        var indexTable = IndexMigrationSql.GetQuotedTableName(SchemaBuilder, _store, typeof(EmailSuppressionIndex), Collection);
        var documentTable = dialect.QuoteForTableName(
            SchemaBuilder.TablePrefix + SchemaBuilder.TableNameConvention.GetDocumentTable(Collection),
            _store.Configuration.Schema);
        var idColumn = dialect.QuoteForColumnName("Id");
        var documentIdColumn = dialect.QuoteForColumnName("DocumentId");
        var addressColumn = dialect.QuoteForColumnName("Address");

        // The list can be long, so only the records of addresses listed more than once (and any that name no address)
        // are read; the database groups them by its own collation, the one the unique index compares with. Ids are read
        // as whatever integer the column holds, since older tenants have 32-bit identity columns.
        var rows = new List<(long Id, long DocumentId, string Address)>();

        await using (var command = SchemaBuilder.Connection.CreateCommand())
        {
            command.Transaction = SchemaBuilder.Transaction;
            command.CommandText =
                $"SELECT {idColumn}, {documentIdColumn}, {addressColumn} FROM {indexTable} " +
                $"WHERE {addressColumn} IS NULL OR {addressColumn} IN (SELECT {addressColumn} FROM {indexTable} GROUP BY {addressColumn} HAVING COUNT(*) > 1)";

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                rows.Add((
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
                    reader.IsDBNull(2) ? null : Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture)));
            }
        }

        // The oldest record of each address stays. A record that names no address is never found, and two of them would
        // break the unique index on the databases that treat nulls as equal, so it goes too.
        var kept = rows
            .Where(row => !string.IsNullOrEmpty(row.Address))
            .GroupBy(row => row.Address, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.MinBy(row => row.Id).Id)
            .ToHashSet();

        foreach (var row in rows.Where(row => !kept.Contains(row.Id)))
        {
            // The index row goes first: on SQL Server it has a foreign key to its document.
            await DeleteIndexRowAsync(indexTable, idColumn, row.Id);
            await DeleteDocumentAsync(documentTable, idColumn, row.DocumentId);
        }
    }

    private async Task CreateSendingStateUniqueIndexAsync()
    {
        // A second app instance activating the tenant at the same time may have created it already.
        if (await IndexTableIndexCreation.ExistsAsync(SchemaBuilder, typeof(EmailSendingStateIndex), SendingStateAddressUniqueIndexName, Collection))
        {
            return;
        }

        await IndexMigrationSql.CreateUniqueIndexAsync(
            SchemaBuilder,
            _store,
            typeof(EmailSendingStateIndex),
            Collection,
            SendingStateAddressUniqueIndexName,
            "AddressId");
    }

    private async Task RemoveDuplicateSendingStatesAsync()
    {
        var dialect = SchemaBuilder.Dialect;
        var indexTable = IndexMigrationSql.GetQuotedTableName(SchemaBuilder, _store, typeof(EmailSendingStateIndex), Collection);
        var documentTable = dialect.QuoteForTableName(
            SchemaBuilder.TablePrefix + SchemaBuilder.TableNameConvention.GetDocumentTable(Collection),
            _store.Configuration.Schema);
        var idColumn = dialect.QuoteForColumnName("Id");
        var documentIdColumn = dialect.QuoteForColumnName("DocumentId");
        var addressIdColumn = dialect.QuoteForColumnName("AddressId");

        // An address has one record, so the table is small: it is read whole and the records to delete are picked here,
        // the same way on every database. Ids are read as whatever integer the column holds, since older tenants have
        // 32-bit identity columns.
        var rows = new List<(long Id, long DocumentId, string AddressId)>();

        await using (var command = SchemaBuilder.Connection.CreateCommand())
        {
            command.Transaction = SchemaBuilder.Transaction;
            command.CommandText = $"SELECT {idColumn}, {documentIdColumn}, {addressIdColumn} FROM {indexTable}";

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                rows.Add((
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
                    reader.IsDBNull(2) ? null : Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture)));
            }
        }

        // The oldest record of each address stays. A record that names no address is never read, and two of them would
        // break the unique index on the databases that treat nulls as equal, so it goes too. Addresses are compared
        // without case, as a case-insensitive collation compares them for the unique index.
        var kept = rows
            .Where(row => !string.IsNullOrEmpty(row.AddressId))
            .GroupBy(row => row.AddressId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.MinBy(row => row.Id).Id)
            .ToHashSet();

        foreach (var row in rows.Where(row => !kept.Contains(row.Id)))
        {
            // The index row goes first: on SQL Server it has a foreign key to its document.
            await DeleteIndexRowAsync(indexTable, idColumn, row.Id);
            await DeleteDocumentAsync(documentTable, idColumn, row.DocumentId);
        }
    }

    // Removes the index row of a duplicate record. Its document goes next; on SQL Server the index row has a foreign key
    // to it, so the row goes first.
    private Task DeleteIndexRowAsync(string indexTable, string idColumn, long id)
        => IndexMigrationSql.ExecuteAsync(SchemaBuilder, $"DELETE FROM {indexTable} WHERE {idColumn} = @Id", ("@Id", id));

    // Removes the document of a duplicate record, once its index row is gone.
    private Task DeleteDocumentAsync(string documentTable, string idColumn, long documentId)
        => IndexMigrationSql.ExecuteAsync(SchemaBuilder, $"DELETE FROM {documentTable} WHERE {idColumn} = @DocumentId", ("@DocumentId", documentId));
}
