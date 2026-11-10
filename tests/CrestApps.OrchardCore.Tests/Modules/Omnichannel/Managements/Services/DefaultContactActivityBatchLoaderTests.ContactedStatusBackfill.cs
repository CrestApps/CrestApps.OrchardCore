using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

/// <summary>
/// The backfill that moves leads called before a completed activity moved a lead out of the default status. Those leads
/// stayed "Open - Not Contacted", so a load that picked that status loaded them again. It runs on the same lead and
/// activity store the loader reads, so it shares this class's SQLite store.
/// </summary>
public sealed partial class DefaultContactActivityBatchLoaderTests
{
    private const string ContactedStatusId = "contacted-status";

    [Fact]
    public async Task ContactedStatusBackfill_MovesOnlyCalledLeadsInTheDefaultStatus()
    {
        // Arrange
        var databasePath = DatabasePath("contacted-backfill");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            string calledLeadId;
            string notCalledLeadId;
            string openActivityLeadId;
            string nurturingLeadId;

            await using (var seedSession = store.CreateSession())
            {
                calledLeadId = await SaveLeadAsync(seedSession, "5556680001", OpenStatusId);
                notCalledLeadId = await SaveLeadAsync(seedSession, "5556680002", OpenStatusId);
                openActivityLeadId = await SaveLeadAsync(seedSession, "5556680003", OpenStatusId);
                nurturingLeadId = await SaveLeadAsync(seedSession, "5556680004", "nurturing-status");

                await SaveExistingActivityAsync(seedSession, calledLeadId, ActivityStatus.Completed, completedUtc: _now.AddDays(-5));
                await SaveExistingActivityAsync(seedSession, openActivityLeadId, ActivityStatus.NotStated);
                await SaveExistingActivityAsync(seedSession, nurturingLeadId, ActivityStatus.Completed, completedUtc: _now.AddDays(-5));

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var backfill = new LeadContactedStatusBackfill(store, CreateBackfillStatuses(), NullLogger<LeadContactedStatusBackfill>.Instance);

            // Act
            var moved = await backfill.RunAsync(TestContext.Current.CancellationToken);
            var movedAgain = await backfill.RunAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, moved);
            Assert.Equal(0, movedAgain);
            Assert.Equal(ContactedStatusId, await GetIndexedStatusIdAsync(store, calledLeadId));
            Assert.Equal(OpenStatusId, await GetIndexedStatusIdAsync(store, notCalledLeadId));
            Assert.Equal(OpenStatusId, await GetIndexedStatusIdAsync(store, openActivityLeadId));
            Assert.Equal("nurturing-status", await GetIndexedStatusIdAsync(store, nurturingLeadId));

            await using var session = store.CreateSession();
            var lead = await session.Query<ContentItem, ContentItemIndex>(index => index.ContentItemId == calledLeadId).FirstOrDefaultAsync(TestContext.Current.CancellationToken);

            Assert.True(lead.TryGet<LeadPart>(out var part));
            Assert.Equal(ContactedStatusId, part.StatusId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task ContactedStatusBackfill_PagesThroughMoreLeadsThanOnePage()
    {
        // Arrange
        var databasePath = DatabasePath("contacted-backfill-pages");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);
        var leadCount = LeadContactedStatusBackfill.PageSize + 5;

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                for (var i = 0; i < leadCount; i++)
                {
                    var leadId = await SaveLeadAsync(seedSession, $"555669{i:D4}", OpenStatusId);
                    await SaveExistingActivityAsync(seedSession, leadId, ActivityStatus.Completed, completedUtc: _now.AddDays(-1));
                }

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var backfill = new LeadContactedStatusBackfill(store, CreateBackfillStatuses(), NullLogger<LeadContactedStatusBackfill>.Instance);

            // Act
            var moved = await backfill.RunAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(leadCount, moved);

            await using var session = store.CreateSession();
            Assert.Equal(0, await session.QueryIndex<LeadIndex>(index => index.StatusId == OpenStatusId).CountAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task ContactedStatusBackfill_WithoutAContactedStatus_MovesNothing()
    {
        // Arrange
        var databasePath = DatabasePath("contacted-backfill-none");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            string calledLeadId;

            await using (var seedSession = store.CreateSession())
            {
                calledLeadId = await SaveLeadAsync(seedSession, "5556700001", OpenStatusId);
                await SaveExistingActivityAsync(seedSession, calledLeadId, ActivityStatus.Completed, completedUtc: _now.AddDays(-1));

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var statuses = new Mock<INamedCatalog<LeadStatus>>();
            statuses
                .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([new LeadStatus { ItemId = OpenStatusId, Name = "Open - Not Contacted", IsDefault = true }]);

            var backfill = new LeadContactedStatusBackfill(store, statuses.Object, NullLogger<LeadContactedStatusBackfill>.Instance);

            // Act
            var moved = await backfill.RunAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(0, moved);
            Assert.Equal(OpenStatusId, await GetIndexedStatusIdAsync(store, calledLeadId));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static INamedCatalog<LeadStatus> CreateBackfillStatuses()
    {
        var statuses = new Mock<INamedCatalog<LeadStatus>>();
        statuses
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new LeadStatus { ItemId = OpenStatusId, Name = "Open - Not Contacted", Order = 10, IsDefault = true },
                new LeadStatus { ItemId = ContactedStatusId, Name = "Working - Contacted", Order = 20, IsContacted = true },
                new LeadStatus { ItemId = "nurturing-status", Name = "Nurturing", Order = 30 },
                new LeadStatus { ItemId = ClosedStatusId, Name = "Closed - Not Converted", Order = 40, IsClosed = true },
            ]);

        return statuses.Object;
    }

    private static async Task<string> GetIndexedStatusIdAsync(IStore store, string leadId)
    {
        await using var session = store.CreateSession();

        var row = await session.QueryIndex<LeadIndex>(index => index.ContentItemId == leadId).FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        return row?.StatusId;
    }
}
