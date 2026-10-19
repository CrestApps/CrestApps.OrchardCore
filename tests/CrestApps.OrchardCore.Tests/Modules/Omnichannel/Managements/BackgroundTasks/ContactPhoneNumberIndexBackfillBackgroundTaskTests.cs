using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.BackgroundTasks;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.BackgroundTasks;

public sealed class ContactPhoneNumberIndexBackfillBackgroundTaskTests
{
    [Fact]
    public async Task RunStepAsync_IndexesEveryNumberOfTheRecordsSavedBeforeTheIndexExisted()
    {
        var (connectionString, path) = ContactPhoneTestStore.CreateDatabase("phone-backfill");
        var legacyStore = await ContactPhoneTestStore.CreateAsync(connectionString, withPhoneNumberIndex: false);

        var published = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Work", "+17025550104"));
        published.Latest = false;

        var draft = ContactPhoneTestStore.CreateRecord(
            ContactPhoneTestStore.ContactContentType,
            ("Work", "+17025550104"),
            ("Other", "+17025550106"));
        draft.ContentItemId = published.ContentItemId;
        draft.Published = false;

        await ContactPhoneTestStore.SaveAsync(legacyStore, published, draft);
        legacyStore.Dispose();

        var store = await ContactPhoneTestStore.OpenAsync(connectionString);

        try
        {
            // Act
            var step = await ContactPhoneNumberIndexBackfillBackgroundTask.RunStepAsync(
                store,
                TestContactMethods.Accessor,
                cursor: 0,
                pages: 10,
                NullLogger.Instance,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.True(step.Completed);

            await using var session = store.CreateSession();
            var rows = await session.QueryIndex<OmnichannelContactPhoneNumberIndex>().ListAsync(TestContext.Current.CancellationToken);

            // Both versions are indexed, each with the numbers it holds.
            Assert.Equal(
                [
                    (true, false, "+17025550104"),
                    (false, true, "+17025550104"),
                    (false, true, "+17025550106"),
                ],
                rows
                    .Select(row => (row.Published, row.Latest, row.NormalizedPhoneNumber))
                    .OrderByDescending(row => row.Published)
                    .ThenBy(row => row.NormalizedPhoneNumber, StringComparer.Ordinal));
            Assert.All(rows, row => Assert.Equal(published.ContentItemId, row.ContentItemId));

            var lookup = ContactPhoneTestStore.CreateLookup(session);
            var byWork = await lookup.FindByNumbersAsync(["+17025550104"], OmnichannelContactVersion.Published, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(published.ContentItemId, Assert.Single(byWork).ContentItemId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    [Fact]
    public async Task RunStepAsync_ResumesFromItsCursorAndLeavesRecordsAlreadyIndexedAlone()
    {
        var (connectionString, path) = ContactPhoneTestStore.CreateDatabase("phone-backfill-resume");
        var legacyStore = await ContactPhoneTestStore.CreateAsync(connectionString, withPhoneNumberIndex: false);
        var legacy = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Work", "+17025550104"));
        await ContactPhoneTestStore.SaveAsync(legacyStore, legacy);
        legacyStore.Dispose();

        var store = await ContactPhoneTestStore.OpenAsync(connectionString);

        try
        {
            // Arrange
            // A record saved after the upgrade already has its rows, so the backfill must not save it again.
            var current = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Office", "+17025550105"));
            await ContactPhoneTestStore.SaveAsync(store, current);
            var currentVersion = await ReadVersionAsync(connectionString, current.Id);

            // Act
            // One page holds both records, so the first step reads them and the second finds nothing after its cursor.
            var first = await ContactPhoneNumberIndexBackfillBackgroundTask.RunStepAsync(store, TestContactMethods.Accessor, 0, pages: 1, NullLogger.Instance, TestContext.Current.CancellationToken);
            var second = await ContactPhoneNumberIndexBackfillBackgroundTask.RunStepAsync(store, TestContactMethods.Accessor, first.Cursor, pages: 1, NullLogger.Instance, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(first.Completed);
            Assert.Equal(current.Id, first.Cursor);
            Assert.True(second.Completed);
            Assert.Equal(currentVersion, await ReadVersionAsync(connectionString, current.Id));

            await using var session = store.CreateSession();
            var lookup = ContactPhoneTestStore.CreateLookup(session);
            var byWork = await lookup.FindByNumbersAsync(["+17025550104"], OmnichannelContactVersion.Published, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(legacy.ContentItemId, Assert.Single(byWork).ContentItemId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    private static async Task<long> ReadVersionAsync(string connectionString, long documentId)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Version FROM Document WHERE Id = $id";
        command.Parameters.AddWithValue("$id", documentId);

        return Convert.ToInt64(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }
}
