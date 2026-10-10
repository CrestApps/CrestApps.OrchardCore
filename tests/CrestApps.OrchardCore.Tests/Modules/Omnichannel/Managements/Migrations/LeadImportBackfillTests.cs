using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Migrations;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore;
using OrchardCore.ContentManagement;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Migrations;

/// <summary>
/// Leads imported before leads recorded their imports are attributed to the import that wrote them, so the files
/// already imported can be picked in an inventory load.
/// </summary>
public sealed class LeadImportBackfillTests
{
    private const string Importer = "importer-user";

    private static readonly DateTime _started = new(2020, 7, 3, 14, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _completed = _started.AddMinutes(10);

    [Fact]
    public async Task RunAsync_RecordsTheImportOnTheLeadsItWroteOnly()
    {
        // Arrange
        var databasePath = LeadTestStore.DatabasePath("import-backfill");
        var store = await CreateStoreAsync($"Data Source={databasePath};Pooling=False");

        try
        {
            string created, updated;

            await using (var seedSession = store.CreateSession())
            {
                await seedSession.SaveAsync(Entry("july", ContentTransferDirection.Import), cancellationToken: TestContext.Current.CancellationToken);
                await seedSession.SaveAsync(Entry("export", ContentTransferDirection.Export), cancellationToken: TestContext.Current.CancellationToken);

                // A lead the import created, and a lead it updated.
                created = await SaveLeadAsync(seedSession, Importer, createdUtc: _started.AddMinutes(1));
                updated = await SaveLeadAsync(seedSession, Importer, createdUtc: _started.AddDays(-30), modifiedUtc: _started.AddMinutes(3));

                // A lead somebody else created while the import ran, and one the importer created after it finished.
                await SaveLeadAsync(seedSession, "someone-else", createdUtc: _started.AddMinutes(2));
                await SaveLeadAsync(seedSession, Importer, createdUtc: _completed.AddMinutes(10));

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            var recorded = await LeadImportBackfill.RunAsync(store, [LeadTestStore.LeadContentType], NullLogger.Instance);
            var recordedAgain = await LeadImportBackfill.RunAsync(store, [LeadTestStore.LeadContentType], NullLogger.Instance);

            // Assert
            Assert.Equal(2, recorded);
            Assert.Equal(0, recordedAgain);

            await using var session = store.CreateSession();
            var rows = await session.QueryIndex<LeadImportIndex>(index => index.Latest).ListAsync(TestContext.Current.CancellationToken);

            Assert.Equal([created, updated], rows.Select(row => row.ContentItemId).Order(StringComparer.Ordinal), StringComparer.Ordinal);
            Assert.All(rows, row =>
            {
                Assert.Equal("july", row.EntryId);
                Assert.Equal("July2020.csv", row.FileName);
            });
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static async Task<IStore> CreateStoreAsync(string connectionString)
    {
        var store = await LeadTestStore.CreateAsync(connectionString);

        store.RegisterIndexes([new ContentTransferEntryIndexProvider()]);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<ContentTransferEntryIndex>(table => table
            .Column<string>("EntryId", column => column.WithLength(26))
            .Column<string>("Status", column => column.NotNull().WithLength(25))
            .Column<ContentTransferDirection>("Direction")
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<string>("ContentType", column => column.WithLength(255))
            .Column<string>("Owner", column => column.WithLength(26)));

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    private static ContentTransferEntry Entry(string entryId, ContentTransferDirection direction)
        => new()
        {
            EntryId = entryId,
            ContentType = LeadTestStore.LeadContentType,
            Owner = Importer,
            UploadedFileName = direction == ContentTransferDirection.Import ? "July2020.csv" : "Leads_Export.csv",
            CreatedUtc = _started,
            CompletedUtc = _completed,
            Status = ContentTransferEntryStatus.Completed,
            Direction = direction,
        };

    private static async Task<string> SaveLeadAsync(ISession session, string owner, DateTime createdUtc, DateTime? modifiedUtc = null)
    {
        var lead = new ContentItem
        {
            ContentItemId = IdGenerator.GenerateId(),
            ContentItemVersionId = IdGenerator.GenerateId(),
            ContentType = LeadTestStore.LeadContentType,
            DisplayText = "Lead",
            Owner = owner,
            CreatedUtc = createdUtc,
            ModifiedUtc = modifiedUtc ?? createdUtc,
            Published = true,
            Latest = true,
        };

        lead.Weld<LeadPart>();

        await session.SaveAsync(lead, cancellationToken: TestContext.Current.CancellationToken);

        return lead.ContentItemId;
    }
}
