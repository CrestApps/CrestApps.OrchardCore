using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Tests.Utilities;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

public sealed partial class DefaultContactActivityBatchLoaderTests
{
    [Fact]
    public async Task LoadAsync_WhenALeadsNumberIsAContactsWorkNumber_SkipsTheLeadAsAlreadyAContact()
    {
        // Arrange
        // A lead is already a contact when a contact holds one of its numbers, whatever type the contact saved it
        // as. Comparing only the first cell and home numbers dialed a lead whose number was a customer's work line.
        var databasePath = DatabasePath("leads-contact-work-number");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            string newLeadId;

            await using (var seedSession = store.CreateSession())
            {
                await SaveLeadAsync(seedSession, "5556660001", OpenStatusId);
                newLeadId = await SaveLeadAsync(seedSession, "5556660002", OpenStatusId);

                var customer = ContactPhoneTestStore.CreateRecord(
                    "Customer",
                    ("Cell", "+15556669999"),
                    ("Work", "+15556660001"));
                await seedSession.SaveAsync(customer, cancellationToken: TestContext.Current.CancellationToken);

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewLeadBatch(new LeadBatchFilter { SkipLeadsThatAreContacts = true });

            // Act
            await LoadAsync(store, connectionString, batch, [ContactContentType]);

            // Assert
            var activity = Assert.Single(await ListLoadedActivitiesAsync(store));

            Assert.Equal(newLeadId, activity.ContactContentItemId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }
}
