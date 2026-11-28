using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Tests.PersonalData;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.BackgroundTasks;

/// <summary>
/// The resumable step that indexes every email address of the contacts saved before the email address index existed,
/// and fills their canonical primary email from the contact's own address rather than by an SQL approximation.
/// </summary>
public sealed class ContactEmailAddressIndexStepTests
{
    [Fact]
    public async Task RunAsync_IndexesEveryAddressOfTheRecordsSavedBefore_AndFillsTheCanonicalPrimaryEmail()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (connectionString, path) = ContactPhoneTestStore.CreateDatabase("email-backfill");
        var legacyStore = await ContactPhoneTestStore.CreateAsync(connectionString, withEmailAddressIndex: false);
        var ann = CrmPersonalDataTestContacts.Create("Ann Lee", ["\"Ann Lee\" <Ann@Example.com>", "ann.work@contoso.com"], []);
        var bob = CrmPersonalDataTestContacts.Create("Bob Ray", [], ["+17025550104"]);
        await ContactPhoneTestStore.SaveAsync(legacyStore, ann, bob);
        legacyStore.Dispose();

        // A row written before the canonical column existed has no value in it.
        await ExecuteAsync(connectionString, "UPDATE OmnichannelContactIndex SET NormalizedPrimaryEmailAddress = NULL");
        var bobVersion = await ReadVersionAsync(connectionString, bob.Id);

        var store = await ContactPhoneTestStore.OpenAsync(connectionString);

        try
        {
            var step = new ContactEmailAddressIndexStep(store, TestContactMethods.Accessor, NullLogger<ContactEmailAddressIndexStep>.Instance);
            var context = new ResumableStepContext();

            // Act
            var done = await step.RunAsync(context, cancellationToken);

            // Assert
            Assert.True(done);
            Assert.False(string.IsNullOrEmpty(context.Cursor));

            await using var session = store.CreateSession();
            var rows = await session.QueryIndex<OmnichannelContactEmailAddressIndex>().ListAsync(cancellationToken);

            Assert.Equal(
                ["ann.work@contoso.com", "ann@example.com"],
                rows.Select(row => row.NormalizedEmailAddress).Order(StringComparer.Ordinal));
            Assert.All(rows, row => Assert.Equal(ann.ContentItemId, row.ContentItemId));

            var contactRow = Assert.Single(await session
                .QueryIndex<OmnichannelContactIndex>(index => index.ContentItemId == ann.ContentItemId)
                .ListAsync(cancellationToken));
            Assert.Equal("ann@example.com", contactRow.NormalizedPrimaryEmailAddress);

            // A record without an address is not saved again.
            Assert.Equal(bobVersion, await ReadVersionAsync(connectionString, bob.Id));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    [Fact]
    public async Task RunStepAsync_LeavesARecordSavedAfterTheUpgradeAlone()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (connectionString, path) = ContactPhoneTestStore.CreateDatabase("email-backfill-current");
        var store = await ContactPhoneTestStore.CreateAsync(connectionString);

        try
        {
            var current = CrmPersonalDataTestContacts.Create("Ann Lee", ["ann@example.com"], []);
            await ContactPhoneTestStore.SaveAsync(store, current);
            var version = await ReadVersionAsync(connectionString, current.Id);

            // Act
            var step = await ContactEmailAddressIndexStep.RunStepAsync(store, TestContactMethods.Accessor, 0, pages: 10, NullLogger.Instance, cancellationToken);

            // Assert
            Assert.True(step.Completed);
            Assert.Equal(0, step.Reindexed);
            Assert.Equal(version, await ReadVersionAsync(connectionString, current.Id));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
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
