using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContentTransfer.Indexes;
using Dapper;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContentTransfer.Migrations;

public sealed class ContentTransferMigrations : DataMigration
{
    private readonly IStore _store;
    private readonly IDbConnectionAccessor _dbConnectionAccessor;

    public ContentTransferMigrations(
        IStore store,
        IDbConnectionAccessor dbConnectionAccessor)
    {
        _store = store;
        _dbConnectionAccessor = dbConnectionAccessor;
    }

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ContentTransferEntryIndex>(table => table
            .Column<string>("EntryId", column => column.WithLength(26))
            .Column<string>("Status", column => column.NotNull().WithLength(25))
            .Column<ContentTransferDirection>("Direction")
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<string>("ContentType", column => column.WithLength(255))
            .Column<string>("Owner", column => column.WithLength(26))
        );

        await SchemaBuilder.AlterIndexTableAsync<ContentTransferEntryIndex>(table => table
            .CreateIndex("IDX_ContentTransferEntryIndex_DocumentId",
                "DocumentId",
                "EntryId",
                "Status",
                "CreatedUtc",
                "ContentType",
                "Owner")
        );

        await SchemaBuilder.AlterIndexTableAsync<ContentTransferEntryIndex>(table => table
            .CreateIndex("IDX_ContentTransferEntryIndex_Status",
                "Status",
                "CreatedUtc",
                "DocumentId",
                "ContentType",
                "Owner")
        );

        await SchemaBuilder.AlterIndexTableAsync<ContentTransferEntryIndex>(table => table
            .CreateIndex("IDX_ContentTransferEntryIndex_Direction",
                "Direction",
                "Status",
                "CreatedUtc",
                "DocumentId")
        );

        return 2;
    }

    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<ContentTransferEntryIndex>(table =>
            table.AddColumn<ContentTransferDirection>("Direction"));

        await SchemaBuilder.AlterIndexTableAsync<ContentTransferEntryIndex>(table => table
            .CreateIndex("IDX_ContentTransferEntryIndex_Direction",
                "Direction",
                "Status",
                "CreatedUtc",
                "DocumentId")
        );

        await MigrateCanceledImportStatusesAsync();

        return 2;
    }

    private async Task MigrateCanceledImportStatusesAsync()
    {
        var dialect = _store.Configuration.SqlDialect;
        var documentTableName = _store.Configuration.TableNameConvention.GetDocumentTable(string.Empty);
        var documentTable = $"{_store.Configuration.TablePrefix}{documentTableName}";
        var quotedDocumentTableName = dialect.QuoteForTableName(documentTable, _store.Configuration.Schema);
        var quotedIdColumnName = dialect.QuoteForColumnName(nameof(Document.Id));
        var quotedContentColumnName = dialect.QuoteForColumnName(nameof(Document.Content));

        var indexTable = $"{_store.Configuration.TablePrefix}{nameof(ContentTransferEntryIndex)}";
        var quotedIndexTableName = dialect.QuoteForTableName(indexTable, _store.Configuration.Schema);
        var quotedDirectionColumnName = dialect.QuoteForColumnName(nameof(ContentTransferEntryIndex.Direction));
        var quotedStatusColumnName = dialect.QuoteForColumnName(nameof(ContentTransferEntryIndex.Status));

        await using var connection = _dbConnectionAccessor.CreateConnection();
        await connection.OpenAsync();

        await connection.ExecuteAsync(
            $"""
            UPDATE {quotedIndexTableName}
            SET {quotedStatusColumnName} = @Paused
            WHERE {quotedDirectionColumnName} = @ImportDirection
              AND {quotedStatusColumnName} IN @LegacyStatuses
            """,
            new
            {
                Paused = nameof(ContentTransferEntryStatus.Paused),
                ImportDirection = ContentTransferDirection.Import,
                LegacyStatuses = new[]
                {
                    "Canceled",
                    "CanceledWithImportedRecords",
                },
            });

        var documents = (await connection.QueryAsync<Document>(
            $"""
            SELECT {quotedIdColumnName} AS Id, {quotedContentColumnName} AS Content
            FROM {quotedDocumentTableName}
            WHERE {quotedContentColumnName} LIKE '%"Status"%'
              AND {quotedContentColumnName} LIKE '%"Direction"%'
              AND {quotedContentColumnName} LIKE '%"StoredFileName"%'
            """)).ToList();

        foreach (var document in documents)
        {
            if (JsonNode.Parse(document.Content) is not JsonObject contentObject)
            {
                continue;
            }

            if (!TryGetDirection(contentObject, out var direction) || direction != ContentTransferDirection.Import)
            {
                continue;
            }

            if (!TryUpdateLegacyStatus(contentObject))
            {
                continue;
            }

            await connection.ExecuteAsync(
                $"UPDATE {quotedDocumentTableName} SET {quotedContentColumnName} = @Content WHERE {quotedIdColumnName} = @Id",
                new
                {
                    document.Id,
                    Content = contentObject.ToJsonString(),
                });
        }
    }

    private static bool TryGetDirection(JsonObject contentObject, out ContentTransferDirection direction)
    {
        direction = default;

        if (contentObject[nameof(ContentTransferEntry.Direction)] is not JsonNode directionNode)
        {
            return false;
        }

        if (directionNode is JsonValue jsonValue)
        {
            if (jsonValue.TryGetValue<int>(out var numericDirection))
            {
                direction = (ContentTransferDirection)numericDirection;
                return true;
            }

            if (jsonValue.TryGetValue<string>(out var stringDirection)
                && Enum.TryParse(stringDirection, true, out direction))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryUpdateLegacyStatus(JsonObject contentObject)
    {
        if (contentObject[nameof(ContentTransferEntry.Status)] is not JsonNode statusNode)
        {
            return false;
        }

        if (statusNode is JsonValue statusValue)
        {
            if (statusValue.TryGetValue<int>(out var numericStatus)
                && (numericStatus == 4 || numericStatus == 5))
            {
                contentObject[nameof(ContentTransferEntry.Status)] = (int)ContentTransferEntryStatus.Paused;
                return true;
            }

            if (statusValue.TryGetValue<string>(out var stringStatus)
                && (string.Equals(stringStatus, "Canceled", StringComparison.Ordinal)
                    || string.Equals(stringStatus, "CanceledWithImportedRecords", StringComparison.Ordinal)))
            {
                contentObject[nameof(ContentTransferEntry.Status)] = nameof(ContentTransferEntryStatus.Paused);
                return true;
            }
        }

        return false;
    }
}
