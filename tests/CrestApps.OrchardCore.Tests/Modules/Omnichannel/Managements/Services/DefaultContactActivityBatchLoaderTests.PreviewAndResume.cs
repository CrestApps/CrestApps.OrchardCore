using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Tests.Utilities;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

public sealed partial class DefaultContactActivityBatchLoaderTests
{
    // Counting before loading is only worth anything if it counts exactly what a load would do and changes nothing.
    [Fact]
    public async Task LoadAsync_WhenPreviewing_CountsWhatALoadWouldDoAndCreatesNothing()
    {
        // Arrange
        var databasePath = DatabasePath("preview");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveContactAsync(seedSession, cellPhoneNumber: "+15553330001", cellNationalNumber: "5553330001");
                await SaveContactAsync(seedSession, cellPhoneNumber: "+15553330002", cellNationalNumber: "5553330002");
                var busyId = await SaveContactAsync(seedSession, cellPhoneNumber: "+15553330003", cellNationalNumber: "5553330003");

                await SaveExistingActivityAsync(seedSession, busyId, ActivityStatus.NotStated);
                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewBatch(ActivitySources.Manual, OmnichannelConstants.Channels.Phone);
            batch.PreventDuplicates = true;
            batch.Status = OmnichannelActivityBatchStatus.New;

            // Act
            ActivityBatchLoadContext context;

            await using (var session = store.CreateSession())
            {
                var loader = CreateLoader(session, store, connectionString, batch.Source);
                context = new ActivityBatchLoadContext(batch, LoaderId, LoaderUserName) { IsPreview = true };

                await loader.LoadAsync(context, TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Assert
            Assert.Null(context.StopReason);
            Assert.Equal(3, batch.TotalMatched);
            Assert.Equal(2, batch.TotalLoaded);
            Assert.Equal(1, batch.TotalSkippedAsDuplicate);
            Assert.Equal(OmnichannelActivityBatchStatus.New, batch.Status);
            Assert.Null(batch.LoadCursor);
            Assert.Empty(await ListLoadedActivitiesAsync(store));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    // The count shown before a load must leave out a contact who asked not to be called, as the load does, and count
    // them in once "Include do-not-call" is ticked: a count that disagrees with the load is worse than none.
    [Theory]
    [InlineData(false, 1, 1)]
    [InlineData(true, 2, 0)]
    public async Task LoadAsync_WhenPreviewing_CountsDoNotCallContactsOnlyWhenIncluded(bool includeDoNotCall, int expectedLoaded, int expectedOptedOut)
    {
        // Arrange
        var databasePath = DatabasePath($"preview-do-not-call-{includeDoNotCall}");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveContactAsync(seedSession, cellPhoneNumber: "+15553330101", cellNationalNumber: "5553330101", doNotCall: true);
                await SaveContactAsync(seedSession, cellPhoneNumber: "+15553330102", cellNationalNumber: "5553330102");
                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewBatch(ActivitySources.Automatic, OmnichannelConstants.Channels.Phone);
            batch.IncludeDoNoCalls = includeDoNotCall;
            batch.Status = OmnichannelActivityBatchStatus.New;

            // Act
            await using (var session = store.CreateSession())
            {
                var loader = CreateLoader(session, store, connectionString, batch.Source);
                var context = new ActivityBatchLoadContext(batch, LoaderId, LoaderUserName) { IsPreview = true };

                await loader.LoadAsync(context, TestContext.Current.CancellationToken);
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Assert
            Assert.Equal(2, batch.TotalMatched);
            Assert.Equal(expectedLoaded, batch.TotalLoaded);
            Assert.Equal(expectedOptedOut, batch.TotalSkippedAsOptedOut);
            Assert.Empty(await ListLoadedActivitiesAsync(store));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    // A load cut off by a restart used to stay "Loading" for good. Resumed, it carries on after the last record it
    // committed and keeps its counts, so nobody it already loaded is loaded twice.
    [Fact]
    public async Task LoadAsync_WhenResuming_CarriesOnAfterTheCursorWithTheCountsItHad()
    {
        // Arrange
        var databasePath = DatabasePath("resume");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            var contactIds = new List<string>();

            await using (var seedSession = store.CreateSession())
            {
                for (var i = 0; i < 3; i++)
                {
                    contactIds.Add(await SaveContactAsync(seedSession, cellPhoneNumber: $"+1555444000{i}", cellNationalNumber: $"555444000{i}"));
                }

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            long cursor;

            await using (var readSession = store.CreateSession())
            {
                var secondContact = await readSession
                    .Query<ContentItem, ContentItemIndex>(index => index.ContentItemId == contactIds[1] && index.Latest)
                    .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

                cursor = secondContact.Id;
            }

            var batch = NewBatch(ActivitySources.Manual, OmnichannelConstants.Channels.Phone);
            batch.LoadCursor = cursor;
            batch.TotalLoaded = 2;
            batch.TotalMatched = 2;

            // Act
            await using (var session = store.CreateSession())
            {
                var loader = CreateLoader(session, store, connectionString, batch.Source);
                var context = new ActivityBatchLoadContext(batch, LoaderId, LoaderUserName) { IsResume = true };

                await loader.LoadAsync(context, TestContext.Current.CancellationToken);
            }

            // Assert
            var activity = Assert.Single(await ListLoadedActivitiesAsync(store));

            Assert.Equal(contactIds[2], activity.ContactContentItemId);
            Assert.Equal(3, batch.TotalLoaded);
            Assert.Equal(3, batch.TotalMatched);
            Assert.Equal(OmnichannelActivityBatchStatus.Loaded, batch.Status);
            Assert.Null(batch.LoadCursor);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    // The attempt a contact's last activity was on narrows the load the way an operator writes it: "4" exactly, "4+"
    // for four or more, "4-" for four or fewer, "3..4" for a range, with or without a subject.
    [Theory]
    [InlineData("4", null, new[] { 1 })]
    [InlineData("4+", null, new[] { 1, 2 })]
    [InlineData("4-", null, new[] { 0, 1 })]
    [InlineData("3..4", null, new[] { 1 })]
    [InlineData("4+", PriorSubjectContentType, new[] { 1 })]
    public async Task LoadAsync_WhenALastActivityAttemptsFilterIsSet_LoadsOnlyContactsWhoseLastActivityWasOnThoseAttempts(
        string attempts,
        string lastActivitySubject,
        int[] expectedContacts)
    {
        // Arrange
        var databasePath = DatabasePath("last-attempts");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            var contactIds = new List<string>();

            await using (var seedSession = store.CreateSession())
            {
                // The last completed activities were on attempts 2, 4 and 5; the third was for another subject.
                var lastAttempts = new[] { (2, PriorSubjectContentType), (4, PriorSubjectContentType), (5, "SomeOtherOutreach") };

                for (var i = 0; i < lastAttempts.Length; i++)
                {
                    var contactId = await SaveContactAsync(seedSession, cellPhoneNumber: $"+1555666000{i}", cellNationalNumber: $"555666000{i}");
                    contactIds.Add(contactId);

                    // An earlier activity on another attempt, which is not the last one and must not count.
                    await SaveCompletedActivityAsync(seedSession, contactId, PriorSubjectContentType, attempts: 9, _now.AddDays(-5));
                    await SaveCompletedActivityAsync(seedSession, contactId, lastAttempts[i].Item2, lastAttempts[i].Item1, _now.AddDays(-1));
                }

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewBatch(ActivitySources.Manual, OmnichannelConstants.Channels.Phone);
            batch.LastActivityAttempts = attempts;
            batch.LastActivitySubjectContentType = lastActivitySubject;

            // Act
            await LoadAsync(store, connectionString, batch);

            // Assert
            var loaded = (await ListLoadedActivitiesAsync(store))
                .Select(activity => activity.ContactContentItemId)
                .Order(StringComparer.Ordinal);

            Assert.Equal(expectedContacts.Select(index => contactIds[index]).Order(StringComparer.Ordinal), loaded);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static Task SaveCompletedActivityAsync(
        ISession session,
        string contactContentItemId,
        string subjectContentType,
        int attempts,
        DateTime completedUtc)
    {
        return session.SaveAsync(
            new OmnichannelActivity
            {
                ItemId = IdGenerator.GenerateId(),
                Kind = ActivityKind.Call,
                Source = ActivitySources.Manual,
                Channel = OmnichannelConstants.Channels.Phone,
                ContactContentItemId = contactContentItemId,
                ContactContentType = ContactContentType,
                SubjectContentType = subjectContentType,
                ScheduledUtc = completedUtc.AddHours(-1),
                CreatedUtc = completedUtc.AddHours(-1),
                CompletedUtc = completedUtc,
                InteractionType = ActivityInteractionType.Manual,
                Attempts = attempts,
                Status = ActivityStatus.Completed,
            },
            collection: OmnichannelConstants.CollectionName,
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
