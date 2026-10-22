using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Data.Sqlite;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

/// <summary>
/// Live, activity loads read every lead of a large list as a whole document, a hundred at a time, and held a small
/// production database's reads at their limit for hours, timing out every other request of the tenant. A load now
/// tells the records it passes over from the indexes and reads the document of a record only when it may become an
/// activity.
/// </summary>
public sealed partial class DefaultContactActivityBatchLoaderTests
{
    [Fact]
    public async Task LoadAsync_PassesOverConvertedLeadsAndLeadsThatAreContacts_WithoutReadingTheirDocuments()
    {
        // Arrange
        // The documents of the leads the load passes over are made unreadable, so reading any of them fails the load.
        var databasePath = DatabasePath("document-reads");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            string openLeadId, convertedLeadId, contactLeadId;

            await using (var seedSession = store.CreateSession())
            {
                openLeadId = await SaveLeadAsync(seedSession, "5556690001", OpenStatusId);
                convertedLeadId = await SaveContactAsync(
                    seedSession,
                    cellPhoneNumber: "+15556690002",
                    cellNationalNumber: "5556690002",
                    lead: part =>
                    {
                        part.StatusId = OpenStatusId;
                        part.IsConverted = true;
                    });
                contactLeadId = await SaveLeadAsync(seedSession, "5556690003", OpenStatusId);

                var customer = ContactPhoneTestStore.CreateRecord("Customer", ("Cell", "+15556690003"));
                await seedSession.SaveAsync(customer, cancellationToken: TestContext.Current.CancellationToken);

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await MakeDocumentsUnreadableAsync(store, connectionString, convertedLeadId, contactLeadId);

            var batch = NewLeadBatch(new LeadBatchFilter { SkipLeadsThatAreContacts = true });

            // Act
            await LoadAsync(store, connectionString, batch, [ContactContentType]);

            // Assert
            var activity = Assert.Single(await ListLoadedActivitiesAsync(store));

            Assert.Equal(openLeadId, activity.ContactContentItemId);
            Assert.Equal(3L, batch.TotalMatched.GetValueOrDefault());
            Assert.Equal(1L, batch.TotalLoaded.GetValueOrDefault());
            Assert.Equal(1L, batch.TotalSkippedAsConverted);
            Assert.Equal(1L, batch.TotalSkippedAsExistingContact);
            Assert.Equal(OmnichannelActivityBatchStatus.Loaded, batch.Status);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static async Task MakeDocumentsUnreadableAsync(IStore store, string connectionString, params string[] contentItemIds)
    {
        List<long> documentIds;

        await using (var session = store.CreateSession())
        {
            documentIds = (await session.QueryIndex<ContentItemIndex>(index => index.ContentItemId.IsIn(contentItemIds))
                .ListAsync(TestContext.Current.CancellationToken))
                .Select(index => index.DocumentId)
                .ToList();
        }

        Assert.Equal(contentItemIds.Length, documentIds.Count);

        var table = store.Configuration.SqlDialect.QuoteForTableName($"{store.Configuration.TablePrefix}Document", store.Configuration.Schema);

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"UPDATE {table} SET Content = 'not a document' WHERE Id IN ({string.Join(", ", documentIds)})";

        Assert.Equal(documentIds.Count, await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
    }
}
