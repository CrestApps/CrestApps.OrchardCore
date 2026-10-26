using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

/// <summary>
/// Live, a dialer load filtered down to about twenty leads sat on "Loading" with nothing loaded for over twenty minutes.
/// The loader read every lead of the list, a hundred whole documents, a round of lookups and a commit at a time, and
/// passed over all but the twenty. These tests hold a filtered load to reading the records its filters picked and
/// nothing else, from the start and when it is resumed.
/// </summary>
public sealed partial class DefaultContactActivityBatchLoaderTests
{
    [Fact]
    public async Task FindCandidateDocumentIdsAsync_WhenEveryPickedRecordHasItsDocumentId_ReadsNothingFromTheDatabase()
    {
        // Arrange
        // A null session fails the test if the lookup reaches for it: a record the filters read from an index already has
        // its document id, and looking it up again cost a query per five hundred records on every load and preview.
        var batch = new OmnichannelActivityBatch { ContactContentType = "Lead" };
        var eligible = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a", "b", "c", "d" };
        var known = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = 40,
            ["b"] = 10,
            ["c"] = 25,
            ["d"] = 5,
        };

        // Act
        var documentIds = await DefaultContactActivityBatchLoader.FindCandidateDocumentIdsAsync(
            readonlySession: null,
            batch,
            eligible,
            known,
            afterDocumentId: 5,
            leadCreatedFrom: null,
            leadCreatedTo: null,
            TestContext.Current.CancellationToken);

        // Assert
        // In order, and past the cursor only.
        Assert.Equal([10L, 25L, 40L], documentIds);
    }

    [Fact]
    public async Task LoadAsync_WhenAFilterPicksAFewRecordsOfALongList_ReadsOnlyThoseRecords()
    {
        // Arrange
        // The two picked records sit at either end of a list longer than two pages, so a load that pages over the whole
        // list commits once per page of it before it reaches the second one.
        var databasePath = DatabasePath("filtered-long-list");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            var pickedIds = new List<string>();

            await using (var seedSession = store.CreateSession())
            {
                pickedIds.Add(await SaveContactAsync(seedSession, cellPhoneNumber: "+15558880001", cellNationalNumber: "5558880001"));

                for (var i = 0; i < 250; i++)
                {
                    await SaveContactAsync(seedSession, cellPhoneNumber: $"+1555777{i:D4}", cellNationalNumber: $"555777{i:D4}");
                }

                pickedIds.Add(await SaveContactAsync(seedSession, cellPhoneNumber: "+15558880002", cellNationalNumber: "5558880002"));

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewBatch(ActivitySources.Manual, OmnichannelConstants.Channels.Phone);
            batch.PhoneNumber = "555888";
            batch.PhoneNumberMatchType = PhoneNumberMatchType.BeginsWith;

            var catalog = CreateCountingBatchCatalog();

            // Act
            await using (var session = store.CreateSession())
            {
                var loader = CreateLoader(session, store, connectionString, batch.Source, batchCatalog: catalog.Object);

                await loader.LoadAsync(new ActivityBatchLoadContext(batch, LoaderId, LoaderUserName), TestContext.Current.CancellationToken);
            }

            // Assert
            var activities = await ListLoadedActivitiesAsync(store);

            Assert.Equal(
                pickedIds.OrderBy(id => id, StringComparer.Ordinal),
                activities.Select(activity => activity.ContactContentItemId).OrderBy(id => id, StringComparer.Ordinal));
            Assert.Equal(2L, batch.TotalMatched);
            Assert.Equal(2L, batch.TotalLoaded);
            Assert.Equal(OmnichannelActivityBatchStatus.Loaded, batch.Status);

            // One page holding the two picked records, then the finished load. Paging over the whole list committed four
            // times.
            catalog.Verify(x => x.UpdateAsync(It.IsAny<OmnichannelActivityBatch>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenAFilteredLoadIsResumed_CarriesOnAfterTheCursorWithThePickedRecordsOnly()
    {
        // Arrange
        // A resumed load reads only the picked records after the last one its earlier run committed, so nobody is
        // loaded twice and nobody the filter left out is loaded at all.
        var databasePath = DatabasePath("filtered-resume");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            var pickedIds = new List<string>();

            await using (var seedSession = store.CreateSession())
            {
                for (var i = 0; i < 3; i++)
                {
                    pickedIds.Add(await SaveContactAsync(seedSession, cellPhoneNumber: $"+1555888100{i}", cellNationalNumber: $"555888100{i}"));
                    await SaveContactAsync(seedSession, cellPhoneNumber: $"+1555777100{i}", cellNationalNumber: $"555777100{i}");
                }

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            long cursor;

            await using (var readSession = store.CreateSession())
            {
                var firstPicked = await readSession
                    .Query<ContentItem, ContentItemIndex>(index => index.ContentItemId == pickedIds[0] && index.Latest)
                    .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

                cursor = firstPicked.Id;
            }

            var batch = NewBatch(ActivitySources.Manual, OmnichannelConstants.Channels.Phone);
            batch.PhoneNumber = "555888";
            batch.PhoneNumberMatchType = PhoneNumberMatchType.BeginsWith;
            batch.LoadCursor = cursor;
            batch.TotalLoaded = 1;
            batch.TotalMatched = 1;

            // Act
            await using (var session = store.CreateSession())
            {
                var loader = CreateLoader(session, store, connectionString, batch.Source);
                var context = new ActivityBatchLoadContext(batch, LoaderId, LoaderUserName) { IsResume = true };

                await loader.LoadAsync(context, TestContext.Current.CancellationToken);
            }

            // Assert
            var activities = await ListLoadedActivitiesAsync(store);

            Assert.Equal(
                pickedIds.Skip(1).OrderBy(id => id, StringComparer.Ordinal),
                activities.Select(activity => activity.ContactContentItemId).OrderBy(id => id, StringComparer.Ordinal));
            Assert.Equal(3L, batch.TotalLoaded);
            Assert.Equal(3L, batch.TotalMatched);
            Assert.Equal(OmnichannelActivityBatchStatus.Loaded, batch.Status);
            Assert.Null(batch.LoadCursor);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    /// <summary>
    /// A catalog that accepts the batch writes like <see cref="CreateBatchCatalog"/>, and can be asked how many there
    /// were: a load writes the batch once per page it commits and once when it finishes.
    /// </summary>
    private static Mock<ICatalog<OmnichannelActivityBatch>> CreateCountingBatchCatalog()
    {
        var catalog = new Mock<ICatalog<OmnichannelActivityBatch>>();

        catalog
            .Setup(x => x.UpdateAsync(It.IsAny<OmnichannelActivityBatch>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        return catalog;
    }
}
