using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.BackgroundTasks;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Mvc.Rendering;
using Moq;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.Documents;
using OrchardCore.Modules;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

/// <summary>
/// The List filter of an inventory load offered the free-text list names the leads carried, which a file's own
/// "List" column could fill with anything, down to a person's name on every row. It now offers the files the leads
/// were imported from. These tests pin what that pick list offers.
/// </summary>
public sealed class LeadImportProviderTests
{
    private static readonly DateTime _july = new(2020, 7, 3, 14, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _august = new(2020, 8, 10, 9, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetImportsAsync_ListsEachImportOnceNewestFirstWithItsLeadCount()
    {
        // Arrange
        var databasePath = LeadTestStore.DatabasePath("import-summaries");
        var store = await LeadTestStore.CreateAsync($"Data Source={databasePath};Pooling=False");

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await LeadTestStore.SaveLeadAsync(seedSession, Import("july", "July2020.csv", _july));
                await LeadTestStore.SaveLeadAsync(seedSession, Import("july", "July2020.csv", _july));

                // A lead a later file updated belongs to both files.
                await LeadTestStore.SaveLeadAsync(seedSession, Import("july", "July2020.csv", _july), Import("august", "August2020.csv", _august));

                // A lead that was not imported adds no entry to pick.
                await LeadTestStore.SaveLeadAsync(seedSession);

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            await using var session = store.CreateSession();
            var imports = await CreateProvider(session).GetImportsAsync();

            // Assert
            Assert.Collection(
                imports,
                august =>
                {
                    Assert.Equal("august", august.EntryId);
                    Assert.Equal("August2020.csv", august.FileName);
                    Assert.Equal(_august, august.ImportedUtc);
                    Assert.Equal(1L, august.LeadCount);
                },
                july =>
                {
                    Assert.Equal("july", july.EntryId);
                    Assert.Equal("July2020.csv", july.FileName);
                    Assert.Equal(_july, july.ImportedUtc);
                    Assert.Equal(3L, july.LeadCount);
                });
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task GetOptionsAsync_NamesEachImportByItsFileAndMarksTheSavedOneSelected()
    {
        // Arrange
        var databasePath = LeadTestStore.DatabasePath("import-selected");
        var store = await LeadTestStore.CreateAsync($"Data Source={databasePath};Pooling=False");

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await LeadTestStore.SaveLeadAsync(seedSession, Import("july", "July2020.csv", _july));
                await LeadTestStore.SaveLeadAsync(seedSession, Import("august", "August2020.csv", _august));

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            await using var session = store.CreateSession();
            var options = await CreateProvider(session).GetOptionsAsync("july");

            // Assert
            Assert.Equal(["august", "july"], options.Select(option => option.Value));
            Assert.StartsWith("July2020.csv", Assert.Single(options, option => option.Selected).Text);
            Assert.Equal("july", Assert.Single(options, option => option.Selected).Value);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task GetOptionsAsync_KeepsASavedImportThatNoLeadRecordsAnyLonger()
    {
        // Arrange
        // A load saved with an import whose leads were since deleted must still show that it loads that import.
        // Dropping it would make the editor read "Any imported file" while the stored filter still loads nothing.
        var databasePath = LeadTestStore.DatabasePath("import-retired");
        var store = await LeadTestStore.CreateAsync($"Data Source={databasePath};Pooling=False");

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await LeadTestStore.SaveLeadAsync(seedSession, Import("august", "August2020.csv", _august));

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            await using var session = store.CreateSession();
            var options = await CreateProvider(session).GetOptionsAsync("deleted-import");

            // Assert
            Assert.Equal(["deleted-import", "august"], options.Select(option => option.Value));
            Assert.Equal("deleted-import", Assert.Single(options, option => option.Selected).Value);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task GetOptionsAsync_ListsImportsThePastImportBackfillHasNotReached_UntilItFinishes()
    {
        // Arrange
        // The past imports are recorded on their leads a few thousand leads a minute, in document order, so after an
        // upgrade the pick list showed only some of the files while the rest waited for the backfill to reach them.
        var databasePath = LeadTestStore.DatabasePath("import-indexing");
        var store = await LeadTestStore.CreateAsync($"Data Source={databasePath};Pooling=False");

        try
        {
            store.RegisterIndexes([new ContentTransferEntryIndexProvider()]);

            await using (var schemaSession = store.CreateSession())
            {
                var transaction = await schemaSession.BeginTransactionAsync(TestContext.Current.CancellationToken);

                await new SchemaBuilder(store.Configuration, transaction).CreateMapIndexTableAsync<ContentTransferEntryIndex>(table => table
                    .Column<string>("EntryId", column => column.WithLength(26))
                    .Column<string>("Status", column => column.NotNull().WithLength(25))
                    .Column<ContentTransferDirection>("Direction")
                    .Column<DateTime>("CreatedUtc", column => column.NotNull())
                    .Column<string>("ContentType", column => column.WithLength(255))
                    .Column<string>("Owner", column => column.WithLength(26)));

                await transaction.CommitAsync(TestContext.Current.CancellationToken);
            }

            await using (var seedSession = store.CreateSession())
            {
                await LeadTestStore.SaveLeadAsync(seedSession, Import("august", "August2020.csv", _august));
                await seedSession.SaveAsync(Entry("august", "August2020.csv", _august, ContentTransferDirection.Import), cancellationToken: TestContext.Current.CancellationToken);
                await seedSession.SaveAsync(Entry("july", "July2020.csv", _july, ContentTransferDirection.Import), cancellationToken: TestContext.Current.CancellationToken);
                await seedSession.SaveAsync(Entry("export", "Leads.csv", _july, ContentTransferDirection.Export), cancellationToken: TestContext.Current.CancellationToken);

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var backfillState = new OmnichannelBackfillState();

            // Act
            IList<SelectListItem> whileIndexing, afterIndexing;

            await using (var session = store.CreateSession())
            {
                whileIndexing = await CreateProvider(session, backfillState).GetOptionsAsync(null);
            }

            backfillState.Completed.Add(LeadImportBackfillBackgroundTask.BackfillName);

            await using (var session = store.CreateSession())
            {
                afterIndexing = await CreateProvider(session, backfillState).GetOptionsAsync(null);
            }

            // Assert
            Assert.Equal(["august", "july"], whileIndexing.Select(option => option.Value));
            Assert.Contains("1 lead", whileIndexing[0].Text);
            Assert.StartsWith("July2020.csv", whileIndexing[1].Text);
            Assert.Contains("still being indexed", whileIndexing[1].Text);
            Assert.Equal(["august"], afterIndexing.Select(option => option.Value));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task GetImportsAsync_KeepsTheImportsBetweenRequestsUntilTheImportHistoryChanges()
    {
        // Arrange
        // Counting the leads of every import read the whole lead import index on each render of the inventory load
        // editor, which saturated a small production database's reads and timed out the queries around it.
        var databasePath = LeadTestStore.DatabasePath("import-cache");
        var store = await LeadTestStore.CreateAsync($"Data Source={databasePath};Pooling=False");
        var importsCache = new LeadImportListCache(CreateClock(DateTime.UtcNow).Object);

        try
        {
            await CreateImportHistoryTableAsync(store);

            await using (var seedSession = store.CreateSession())
            {
                await LeadTestStore.SaveLeadAsync(seedSession, Import("july", "July2020.csv", _july));
                await seedSession.SaveAsync(Entry("july", "July2020.csv", _july, ContentTransferDirection.Import), cancellationToken: TestContext.Current.CancellationToken);

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var backfillState = new OmnichannelBackfillState();
            backfillState.Completed.Add(LeadImportBackfillBackgroundTask.BackfillName);

            await using (var session = store.CreateSession())
            {
                await CreateProvider(session, backfillState, importsCache).GetImportsAsync();
            }

            // A lead written while the history is unchanged, as by an import still running, is not counted yet.
            await using (var seedSession = store.CreateSession())
            {
                await LeadTestStore.SaveLeadAsync(seedSession, Import("july", "July2020.csv", _july));

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            IReadOnlyList<LeadImportSummary> kept, refreshed;

            await using (var session = store.CreateSession())
            {
                kept = await CreateProvider(session, backfillState, importsCache).GetImportsAsync();
            }

            await using (var seedSession = store.CreateSession())
            {
                await LeadTestStore.SaveLeadAsync(seedSession, Import("august", "August2020.csv", _august));
                await seedSession.SaveAsync(Entry("august", "August2020.csv", _august, ContentTransferDirection.Import), cancellationToken: TestContext.Current.CancellationToken);

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using (var session = store.CreateSession())
            {
                refreshed = await CreateProvider(session, backfillState, importsCache).GetImportsAsync();
            }

            // Assert
            Assert.Equal(1L, Assert.Single(kept).LeadCount);
            Assert.Equal(["august", "july"], refreshed.Select(import => import.EntryId));
            Assert.Equal(2L, refreshed[1].LeadCount);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task GetImportsAsync_ReadsTheImportsAgainOnceTheirLifetimeIsOver()
    {
        // Arrange
        // With the import history unchanged, a lead deleted or re-imported since is reflected once the kept imports
        // have outlived their lifetime.
        var databasePath = LeadTestStore.DatabasePath("import-cache-lifetime");
        var store = await LeadTestStore.CreateAsync($"Data Source={databasePath};Pooling=False");
        var now = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
        var clock = CreateClock(now);
        var importsCache = new LeadImportListCache(clock.Object);

        try
        {
            await CreateImportHistoryTableAsync(store);

            await using (var seedSession = store.CreateSession())
            {
                await LeadTestStore.SaveLeadAsync(seedSession, Import("july", "July2020.csv", _july));
                await seedSession.SaveAsync(Entry("july", "July2020.csv", _july, ContentTransferDirection.Import), cancellationToken: TestContext.Current.CancellationToken);

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var backfillState = new OmnichannelBackfillState();
            backfillState.Completed.Add(LeadImportBackfillBackgroundTask.BackfillName);

            await using (var session = store.CreateSession())
            {
                await CreateProvider(session, backfillState, importsCache).GetImportsAsync();
            }

            await using (var seedSession = store.CreateSession())
            {
                await LeadTestStore.SaveLeadAsync(seedSession, Import("july", "July2020.csv", _july));

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            IReadOnlyList<LeadImportSummary> withinLifetime, afterLifetime;

            clock.Setup(c => c.UtcNow).Returns(now.Add(LeadImportListCache.Lifetime).AddSeconds(-1));

            await using (var session = store.CreateSession())
            {
                withinLifetime = await CreateProvider(session, backfillState, importsCache).GetImportsAsync();
            }

            clock.Setup(c => c.UtcNow).Returns(now.Add(LeadImportListCache.Lifetime));

            await using (var session = store.CreateSession())
            {
                afterLifetime = await CreateProvider(session, backfillState, importsCache).GetImportsAsync();
            }

            // Assert
            Assert.Equal(1L, Assert.Single(withinLifetime).LeadCount);
            Assert.Equal(2L, Assert.Single(afterLifetime).LeadCount);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task GetSelectedOptionsAsync_OffersOnlyTheSavedImportNamedByItsFile()
    {
        // Arrange
        // The editor renders just the saved import, so opening it never counts the leads of every import.
        var databasePath = LeadTestStore.DatabasePath("import-selected");
        var store = await LeadTestStore.CreateAsync($"Data Source={databasePath};Pooling=False");

        try
        {
            await CreateImportHistoryTableAsync(store);

            await using (var seedSession = store.CreateSession())
            {
                await LeadTestStore.SaveLeadAsync(seedSession, Import("july", "July2020.csv", _july));
                await seedSession.SaveAsync(Entry("july", "July2020.csv", _july, ContentTransferDirection.Import), cancellationToken: TestContext.Current.CancellationToken);
                await seedSession.SaveAsync(Entry("august", "August2020.csv", _august, ContentTransferDirection.Import), cancellationToken: TestContext.Current.CancellationToken);

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            await using var session = store.CreateSession();
            var provider = CreateProvider(session, new OmnichannelBackfillState());
            var options = await provider.GetSelectedOptionsAsync("august");
            var none = await provider.GetSelectedOptionsAsync(" ");

            // Assert
            var option = Assert.Single(options);
            Assert.Equal("august", option.Value);
            Assert.True(option.Selected);
            Assert.Contains("August2020.csv", option.Text);
            Assert.Empty(none);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task GetSelectedOptionsAsync_KeepsTheSavedImportWhenThereIsNoImportHistoryToNameItFrom()
    {
        // Arrange
        var databasePath = LeadTestStore.DatabasePath("import-selected-no-history");
        var store = await LeadTestStore.CreateAsync($"Data Source={databasePath};Pooling=False");

        try
        {
            // Act
            await using var session = store.CreateSession();
            var options = await CreateProvider(session).GetSelectedOptionsAsync("gone");

            // Assert
            var option = Assert.Single(options);
            Assert.Equal("gone", option.Value);
            Assert.True(option.Selected);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static Mock<IClock> CreateClock(DateTime utcNow)
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(utcNow);

        return clock;
    }

    private static async Task CreateImportHistoryTableAsync(IStore store)
    {
        store.RegisterIndexes([new ContentTransferEntryIndexProvider()]);

        await using var schemaSession = store.CreateSession();
        var transaction = await schemaSession.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await new SchemaBuilder(store.Configuration, transaction).CreateMapIndexTableAsync<ContentTransferEntryIndex>(table => table
            .Column<string>("EntryId", column => column.WithLength(26))
            .Column<string>("Status", column => column.NotNull().WithLength(25))
            .Column<ContentTransferDirection>("Direction")
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<string>("ContentType", column => column.WithLength(255))
            .Column<string>("Owner", column => column.WithLength(26)));

        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private static ContentTransferEntry Entry(string entryId, string fileName, DateTime createdUtc, ContentTransferDirection direction)
        => new()
        {
            EntryId = entryId,
            ContentType = LeadTestStore.LeadContentType,
            UploadedFileName = fileName,
            CreatedUtc = createdUtc,
            ProcessSaveUtc = createdUtc.AddMinutes(5),
            Status = ContentTransferEntryStatus.Completed,
            Direction = direction,
        };

    private static LeadImportProvider CreateProvider(ISession session, OmnichannelBackfillState backfillState, LeadImportListCache importsCache = null)
    {
        var localClock = new Mock<ILocalClock>();
        localClock
            .Setup(clock => clock.ConvertToLocalAsync(It.IsAny<DateTimeOffset>()))
            .ReturnsAsync((DateTimeOffset value) => value);

        var state = new Mock<IDocumentManager<OmnichannelBackfillState>>();
        state.Setup(manager => manager.GetOrCreateImmutableAsync(It.IsAny<Func<Task<OmnichannelBackfillState>>>()))
            .ReturnsAsync(backfillState);

        var definitions = new Mock<IContentDefinitionManager>();
        definitions.Setup(manager => manager.ListTypeDefinitionsAsync())
            .ReturnsAsync([new ContentTypeDefinitionBuilder()
                .WithName(LeadTestStore.LeadContentType)
                .WithPart(OmnichannelConstants.ContentParts.Lead)
                .Build()]);

        return new LeadImportProvider(session, localClock.Object, new PassThroughStringLocalizer<LeadImportProvider>(), state.Object, definitions.Object, importsCache);
    }

    private static LeadImport Import(string entryId, string fileName, DateTime importedUtc)
        => new()
        {
            EntryId = entryId,
            FileName = fileName,
            ImportedUtc = importedUtc,
        };

    private static LeadImportProvider CreateProvider(ISession session)
    {
        var localClock = new Mock<ILocalClock>();
        localClock
            .Setup(clock => clock.ConvertToLocalAsync(It.IsAny<DateTimeOffset>()))
            .ReturnsAsync((DateTimeOffset value) => value);

        return new LeadImportProvider(session, localClock.Object, new PassThroughStringLocalizer<LeadImportProvider>());
    }
}
