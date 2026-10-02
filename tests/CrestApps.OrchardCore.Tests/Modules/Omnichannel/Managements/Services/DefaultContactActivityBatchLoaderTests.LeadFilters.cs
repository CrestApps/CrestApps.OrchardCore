using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Tests.Utilities;
using OrchardCore.ContentFields.Fields;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

/// <summary>
/// The lead filters of an inventory load. The load used to carry an "Include closed leads" box beside the status
/// boxes, and both had to agree: ticking a closed status without the box loaded nothing from it. The box is gone, so
/// the status boxes alone decide which statuses load.
/// </summary>
public sealed partial class DefaultContactActivityBatchLoaderTests
{
    private const string OpenStatusId = "open-status";
    private const string ClosedStatusId = "closed-status";

    [Fact]
    public async Task LoadAsync_WhenNoLeadStatusIsPicked_LeavesLeadsInAClosedStatusOut()
    {
        // Arrange
        var databasePath = DatabasePath("leads-no-status");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            string openLeadId;

            await using (var seedSession = store.CreateSession())
            {
                openLeadId = await SaveLeadAsync(seedSession, "5556660001", OpenStatusId);
                await SaveLeadAsync(seedSession, "5556660002", ClosedStatusId, isClosed: true);

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewLeadBatch(new LeadBatchFilter());

            // Act
            await LoadAsync(store, connectionString, batch, [ContactContentType]);

            // Assert
            var activity = Assert.Single(await ListLoadedActivitiesAsync(store));

            Assert.Equal(openLeadId, activity.ContactContentItemId);
            Assert.Equal(1L, batch.TotalMatched.GetValueOrDefault());
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenAClosedStatusIsPicked_LoadsItsLeads()
    {
        // Arrange
        // Bug: picking a closed status loaded nothing unless "Include closed leads" was ticked as well, because the
        // two filters were combined. The pick alone has to be enough now that the box is gone.
        var databasePath = DatabasePath("leads-closed-status");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            string closedLeadId;

            await using (var seedSession = store.CreateSession())
            {
                await SaveLeadAsync(seedSession, "5556670001", OpenStatusId);
                closedLeadId = await SaveLeadAsync(seedSession, "5556670002", ClosedStatusId, isClosed: true);

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewLeadBatch(new LeadBatchFilter { StatusIds = [ClosedStatusId] });

            // Act
            await LoadAsync(store, connectionString, batch, [ContactContentType]);

            // Assert
            var activity = Assert.Single(await ListLoadedActivitiesAsync(store));

            Assert.Equal(closedLeadId, activity.ContactContentItemId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenAListAndAnOwnerArePicked_LoadsOnlyLeadsMatchingBoth()
    {
        // Arrange
        // Every lead filter narrows the load. A lead on the right list but owned by somebody else, or owned by the
        // right person but on another list, is not part of "list X owned by Z".
        var databasePath = DatabasePath("leads-list-owner");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            string matchingLeadId;

            await using (var seedSession = store.CreateSession())
            {
                matchingLeadId = await SaveLeadAsync(seedSession, "5556680001", OpenStatusId, listName: "Spring Import", ownerId: "owner-z");
                await SaveLeadAsync(seedSession, "5556680002", OpenStatusId, listName: "Spring Import", ownerId: "owner-y");
                await SaveLeadAsync(seedSession, "5556680003", OpenStatusId, listName: "Trade Show Scans", ownerId: "owner-z");

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewLeadBatch(new LeadBatchFilter
            {
                ListName = "Spring Import",
                OwnerId = "owner-z",
            });

            // Act
            await LoadAsync(store, connectionString, batch, [ContactContentType]);

            // Assert
            var activity = Assert.Single(await ListLoadedActivitiesAsync(store));

            Assert.Equal(matchingLeadId, activity.ContactContentItemId);
            Assert.Equal(1L, batch.TotalMatched.GetValueOrDefault());
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static OmnichannelActivityBatch NewLeadBatch(LeadBatchFilter filter)
    {
        var batch = NewBatch(ActivitySources.Manual, OmnichannelConstants.Channels.Phone);

        batch.Put(filter);

        return batch;
    }

    private static Task<string> SaveLeadAsync(
        ISession session,
        string nationalNumber,
        string statusId,
        bool isClosed = false,
        string listName = null,
        string ownerId = null)
    {
        return SaveContactAsync(
            session,
            cellPhoneNumber: $"+1{nationalNumber}",
            cellNationalNumber: nationalNumber,
            lead: part =>
            {
                part.StatusId = statusId;
                part.IsClosed = isClosed;
                part.ListName = new TextField { Text = listName };
                part.Owner = new UserPickerField { UserIds = ownerId is null ? [] : [ownerId] };
            });
    }
}
