using System.Data.Common;
using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Indexes;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Data;
using OrchardCore.Flows.Models;
using OrchardCore.Modules;
using YesSql;
using YesSql.Indexes;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

/// <summary>
/// This is the gate that decides who a campaign is allowed to contact. Everything downstream -- the dialer, the
/// automated SMS pass, the agent desktop -- just works through whatever it put in the inventory, so a mistake here
/// is a call placed to someone who asked us to stop, the same lead dialed twice, a run that ignores the size the
/// operator asked for, or a filtered batch that quietly loads the whole database. These tests hold the loader to
/// the promises the batch form makes to the person filling it in.
/// </summary>
public sealed class DefaultContactActivityBatchLoaderTests
{
    private static readonly DateTime _now = new(2026, 7, 16, 20, 0, 0, DateTimeKind.Utc);

    private const string ContactContentType = "Lead";
    private const string SubjectContentType = "LeadFollowUp";
    private const string PriorSubjectContentType = "PriorOutreach";
    private const string LoaderId = "loader-user-id";
    private const string LoaderUserName = "loader";

    [Fact]
    public async Task LoadAsync_WhenAnAutomatedPhoneContactHasAskedNotToBeCalled_CreatesNoActivityForThem()
    {
        // Arrange
        // The do-not-call preference is only worth anything if it is read where the inventory is built. Nothing
        // between this loader and the carrier asks the contact again, so a contact who is loaded here is a contact
        // who gets dialed.
        var databasePath = DatabasePath("do-not-call");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            string optedOutContactId;
            string reachableContactId;

            await using (var seedSession = store.CreateSession())
            {
                optedOutContactId = await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550101", cellNationalNumber: "5555550101", doNotCall: true);
                reachableContactId = await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550102", cellNationalNumber: "5555550102");

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewBatch(ActivitySources.Automatic, OmnichannelConstants.Channels.Phone);

            // Act
            await LoadAsync(store, connectionString, batch);

            // Assert
            var activities = await ListLoadedActivitiesAsync(store);
            var activity = Assert.Single(activities);

            Assert.Equal(reachableContactId, activity.ContactContentItemId);
            Assert.Equal("+15555550102", activity.PreferredDestination);
            Assert.DoesNotContain(activities, entry => entry.ContactContentItemId == optedOutContactId);
            Assert.Equal(1L, batch.TotalLoaded.GetValueOrDefault());
            Assert.Equal(OmnichannelActivityBatchStatus.Loaded, batch.Status);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenAnAutomatedSmsContactHasAskedNotToBeTexted_CreatesNoActivityForThem()
    {
        // Arrange
        // Same promise as the phone channel, and worth its own test: the two preferences are separate columns on
        // the contact, and a contact who blocked SMS may still be perfectly happy to take a call.
        var databasePath = DatabasePath("do-not-sms");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            string optedOutContactId;
            string reachableContactId;

            await using (var seedSession = store.CreateSession())
            {
                optedOutContactId = await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550201", cellNationalNumber: "5555550201", doNotSms: true);
                reachableContactId = await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550202", cellNationalNumber: "5555550202");

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewBatch(ActivitySources.Automatic, OmnichannelConstants.Channels.Sms);

            // Act
            await LoadAsync(store, connectionString, batch);

            // Assert
            var activities = await ListLoadedActivitiesAsync(store);
            var activity = Assert.Single(activities);

            Assert.Equal(reachableContactId, activity.ContactContentItemId);
            Assert.Equal(ActivityKind.Sms, activity.Kind);
            Assert.DoesNotContain(activities, entry => entry.ContactContentItemId == optedOutContactId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenAManualPhoneContactHasAskedNotToBeCalled_CreatesNoActivityForThem()
    {
        // Arrange
        // The skip that honours the preference is guarded by the interaction type, so it only runs for automated
        // batches. A manual batch -- the ordinary case, a list handed to agents to work -- loads the opted-out
        // contact anyway, with an empty destination where their number should be. An agent then sees the lead in
        // their queue and reaches them from the contact record. The preference has to hold whoever is dialing.
        var databasePath = DatabasePath("manual-do-not-call");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            string optedOutContactId;
            string reachableContactId;

            await using (var seedSession = store.CreateSession())
            {
                optedOutContactId = await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550301", cellNationalNumber: "5555550301", doNotCall: true);
                reachableContactId = await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550302", cellNationalNumber: "5555550302");

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewBatch(ActivitySources.Manual, OmnichannelConstants.Channels.Phone);

            // Act
            await LoadAsync(store, connectionString, batch);

            // Assert
            var activities = await ListLoadedActivitiesAsync(store);
            var activity = Assert.Single(activities);

            Assert.Equal(reachableContactId, activity.ContactContentItemId);
            Assert.DoesNotContain(activities, entry => entry.ContactContentItemId == optedOutContactId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenIncludeDoNoCallsIsTicked_LoadsContactsWhoAskedNotToBeCalled()
    {
        // Arrange
        // "Include do-not-call" is offered on the batch form and saved with the batch, and an operator who ticks it
        // -- for a recall notice, a service outage, anything the preference is not meant to block -- expects those
        // contacts in the inventory. Nothing reads the flag today, so ticking it changes nothing at all.
        var databasePath = DatabasePath("include-do-not-call");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            string optedOutContactId;

            await using (var seedSession = store.CreateSession())
            {
                optedOutContactId = await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550401", cellNationalNumber: "5555550401", doNotCall: true);
                await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550402", cellNationalNumber: "5555550402");

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewBatch(ActivitySources.Automatic, OmnichannelConstants.Channels.Phone);
            batch.IncludeDoNoCalls = true;

            // Act
            await LoadAsync(store, connectionString, batch);

            // Assert
            var activities = await ListLoadedActivitiesAsync(store);

            Assert.Equal(2, activities.Count);

            var optedOutActivity = Assert.Single(activities, entry => entry.ContactContentItemId == optedOutContactId);

            Assert.Equal("+15555550401", optedOutActivity.PreferredDestination);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenIncludeDoNoSmsIsUnticked_ExcludesContactsWhoAskedNotToBeTexted()
    {
        // Arrange
        // The mirror of the ticked case, on the channel where the consequence is a regulatory one. The flag is left
        // unticked, which is the form's way of saying "respect the preference", and the batch is manual so the
        // interaction-type guard cannot do the excluding for the wrong reason -- only the flag can.
        var databasePath = DatabasePath("exclude-do-not-sms");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            string optedOutContactId;
            string reachableContactId;

            await using (var seedSession = store.CreateSession())
            {
                optedOutContactId = await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550501", cellNationalNumber: "5555550501", doNotSms: true);
                reachableContactId = await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550502", cellNationalNumber: "5555550502");

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewBatch(ActivitySources.Manual, OmnichannelConstants.Channels.Sms);
            batch.IncludeDoNoSms = false;

            // Act
            await LoadAsync(store, connectionString, batch);

            // Assert
            var activities = await ListLoadedActivitiesAsync(store);
            var activity = Assert.Single(activities);

            Assert.Equal(reachableContactId, activity.ContactContentItemId);
            Assert.DoesNotContain(activities, entry => entry.ContactContentItemId == optedOutContactId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenIncludeDoNoEmailIsUnticked_ExcludesContactsWhoAskedNotToBeEmailed()
    {
        // Arrange
        // The third of the three flags, on the third channel. It is stored, it is editable, and like the other two
        // it is read by nothing, so an unticked box does not keep an unsubscribed contact out of the send list.
        var databasePath = DatabasePath("exclude-do-not-email");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            string optedOutContactId;
            string reachableContactId;

            await using (var seedSession = store.CreateSession())
            {
                optedOutContactId = await SaveContactAsync(seedSession, emailAddress: "opted-out@example.com", doNotEmail: true);
                reachableContactId = await SaveContactAsync(seedSession, emailAddress: "reachable@example.com");

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewBatch(ActivitySources.Manual, OmnichannelConstants.Channels.Email);
            batch.IncludeDoNoEmail = false;

            // Act
            await LoadAsync(store, connectionString, batch);

            // Assert
            var activities = await ListLoadedActivitiesAsync(store);
            var activity = Assert.Single(activities);

            Assert.Equal(reachableContactId, activity.ContactContentItemId);
            Assert.Equal("reachable@example.com", activity.PreferredDestination);
            Assert.DoesNotContain(activities, entry => entry.ContactContentItemId == optedOutContactId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenPreventDuplicatesIsSet_SkipsContactsHoldingAnOpenActivity()
    {
        // Arrange
        // Loading a second batch over the same list is routine. A contact who is already sitting in someone's queue
        // must not be handed to a second agent, or the lead gets called twice within the hour by two people who
        // each think they found them first.
        var databasePath = DatabasePath("prevent-duplicates-open");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            string busyContactId;
            string freeContactId;

            await using (var seedSession = store.CreateSession())
            {
                busyContactId = await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550601", cellNationalNumber: "5555550601");
                freeContactId = await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550602", cellNationalNumber: "5555550602");

                await SaveExistingActivityAsync(seedSession, busyContactId, ActivityStatus.InProgress);

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewBatch(ActivitySources.Manual, OmnichannelConstants.Channels.Phone);
            batch.PreventDuplicates = true;

            // Act
            await LoadAsync(store, connectionString, batch);

            // Assert
            var activities = await ListLoadedActivitiesAsync(store);
            var activity = Assert.Single(activities);

            Assert.Equal(freeContactId, activity.ContactContentItemId);
            Assert.DoesNotContain(activities, entry => entry.ContactContentItemId == busyContactId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenPreventDuplicatesIsSet_ReadmitsContactsWhoseOnlyActivityIsTerminal()
    {
        // Arrange
        // The other half of the duplicate rule, and the half that costs money when it is wrong. A single busy
        // signal or a no-answer leaves a Failed activity behind; if that counted as "already in the queue" the lead
        // would be barred from every future batch forever on the strength of one unanswered ring.
        var databasePath = DatabasePath("prevent-duplicates-terminal");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        var terminalStatuses = new[]
        {
            ActivityStatus.Completed,
            ActivityStatus.Purged,
            ActivityStatus.Failed,
            ActivityStatus.Cancelled,
        };

        try
        {
            var finishedContactIds = new List<string>();
            string busyContactId;

            await using (var seedSession = store.CreateSession())
            {
                for (var i = 0; i < terminalStatuses.Length; i++)
                {
                    var contactId = await SaveContactAsync(
                        seedSession,
                        cellPhoneNumber: $"+1555555071{i}",
                        cellNationalNumber: $"555555071{i}");

                    finishedContactIds.Add(contactId);

                    await SaveExistingActivityAsync(seedSession, contactId, terminalStatuses[i]);
                }

                busyContactId = await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550799", cellNationalNumber: "5555550799");

                await SaveExistingActivityAsync(seedSession, busyContactId, ActivityStatus.Scheduled);

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewBatch(ActivitySources.Manual, OmnichannelConstants.Channels.Phone);
            batch.PreventDuplicates = true;

            // Act
            await LoadAsync(store, connectionString, batch);

            // Assert
            var activities = await ListLoadedActivitiesAsync(store);

            Assert.Equal(
                finishedContactIds.OrderBy(id => id, StringComparer.Ordinal),
                activities.Select(entry => entry.ContactContentItemId).OrderBy(id => id, StringComparer.Ordinal));
            Assert.DoesNotContain(activities, entry => entry.ContactContentItemId == busyContactId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenALimitIsSet_StopsAtTheLimitPartWayThroughThePage()
    {
        // Arrange
        // The limit is how an operator tries a campaign out on a handful of leads before committing the list. If it
        // overshoots, the calls have already gone out by the time anyone notices. Five matching contacts sit well
        // inside one page, so the loader has to stop in the middle of a page rather than at a page boundary.
        var databasePath = DatabasePath("limit-exact");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                for (var i = 0; i < 5; i++)
                {
                    await SaveContactAsync(seedSession, cellPhoneNumber: $"+1555555080{i}", cellNationalNumber: $"555555080{i}");
                }

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewBatch(ActivitySources.Manual, OmnichannelConstants.Channels.Phone);
            batch.Limit = 2;

            // Act
            await LoadAsync(store, connectionString, batch);

            // Assert
            var activities = await ListLoadedActivitiesAsync(store);

            Assert.Equal(2, activities.Count);
            Assert.Equal(2L, batch.TotalLoaded.GetValueOrDefault());
            Assert.Equal(OmnichannelActivityBatchStatus.Loaded, batch.Status);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenALimitIsSet_CountsActivitiesCreatedRatherThanContactsExamined()
    {
        // Arrange
        // "Load 3" means three people to call, not three rows read off the list. Contacts dropped by the duplicate
        // filter are examined and discarded, and if they were counted against the limit the operator would get a
        // batch of one when they asked for three, with no indication why.
        var databasePath = DatabasePath("limit-counts-created");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            var reachableContactIds = new List<string>();

            await using (var seedSession = store.CreateSession())
            {
                // Seeded first so they sit at the head of the document-id ordering and are examined before any
                // contact that can actually be loaded.
                for (var i = 0; i < 3; i++)
                {
                    var busyContactId = await SaveContactAsync(seedSession, cellPhoneNumber: $"+1555555090{i}", cellNationalNumber: $"555555090{i}");

                    await SaveExistingActivityAsync(seedSession, busyContactId, ActivityStatus.Pending);
                }

                for (var i = 0; i < 4; i++)
                {
                    reachableContactIds.Add(await SaveContactAsync(seedSession, cellPhoneNumber: $"+1555555091{i}", cellNationalNumber: $"555555091{i}"));
                }

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewBatch(ActivitySources.Manual, OmnichannelConstants.Channels.Phone);
            batch.PreventDuplicates = true;
            batch.Limit = 3;

            // Act
            await LoadAsync(store, connectionString, batch);

            // Assert
            var activities = await ListLoadedActivitiesAsync(store);

            Assert.Equal(3, activities.Count);
            Assert.Equal(3L, batch.TotalLoaded.GetValueOrDefault());
            Assert.All(activities, activity => Assert.Contains(activity.ContactContentItemId, reachableContactIds));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenPhoneTimeZoneAndLastActivityFiltersAreCombined_LoadsOnlyContactsMatchingAllThree()
    {
        // Arrange
        // Every filter the operator adds is meant to narrow the list. If the loader ever unioned them instead, a
        // batch meant for "this area code, this time zone, last outcome X" would go out to everybody who matched
        // any one of the three -- a far larger list than the one the operator reviewed before pressing load.
        var databasePath = DatabasePath("filters-intersect");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            string matchesEverythingId;
            string wrongLastActivityId;
            string wrongTimeZoneId;
            string wrongPhoneId;

            await using (var seedSession = store.CreateSession())
            {
                matchesEverythingId = await SaveContactAsync(
                    seedSession,
                    cellPhoneNumber: "+15551110001",
                    cellNationalNumber: "5551110001",
                    timeZoneId: "America/New_York");

                wrongLastActivityId = await SaveContactAsync(
                    seedSession,
                    cellPhoneNumber: "+15551110002",
                    cellNationalNumber: "5551110002",
                    timeZoneId: "America/New_York");

                wrongTimeZoneId = await SaveContactAsync(
                    seedSession,
                    cellPhoneNumber: "+15551110003",
                    cellNationalNumber: "5551110003",
                    timeZoneId: "America/Chicago");

                wrongPhoneId = await SaveContactAsync(
                    seedSession,
                    cellPhoneNumber: "+15552220004",
                    cellNationalNumber: "5552220004",
                    timeZoneId: "America/New_York");

                await SaveExistingActivityAsync(seedSession, matchesEverythingId, ActivityStatus.Completed, PriorSubjectContentType, _now.AddDays(-1));
                await SaveExistingActivityAsync(seedSession, wrongLastActivityId, ActivityStatus.Completed, "SomeOtherOutreach", _now.AddDays(-1));
                await SaveExistingActivityAsync(seedSession, wrongTimeZoneId, ActivityStatus.Completed, PriorSubjectContentType, _now.AddDays(-1));
                await SaveExistingActivityAsync(seedSession, wrongPhoneId, ActivityStatus.Completed, PriorSubjectContentType, _now.AddDays(-1));

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewBatch(ActivitySources.Manual, OmnichannelConstants.Channels.Phone);
            batch.PhoneNumber = "555111";
            batch.PhoneNumberMatchType = PhoneNumberMatchType.BeginsWith;
            batch.TimeZoneIds = ["America/New_York"];
            batch.LastActivitySubjectContentType = PriorSubjectContentType;

            // Act
            await LoadAsync(store, connectionString, batch);

            // Assert
            var activities = await ListLoadedActivitiesAsync(store);
            var activity = Assert.Single(activities);

            Assert.Equal(matchesEverythingId, activity.ContactContentItemId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenTheFiltersHaveNoContactInCommon_MarksTheBatchLoadedWithoutCreatingActivities()
    {
        // Arrange
        // Filters that overlap in nobody are how an operator finds out their criteria were too narrow. The batch has
        // to settle at Loaded with nothing in it, rather than fall through to the unfiltered paging loop and hand
        // the campaign every contact in the database.
        var databasePath = DatabasePath("filters-empty");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                // Matches the phone filter, but not the time zone.
                await SaveContactAsync(
                    seedSession,
                    cellPhoneNumber: "+15553330001",
                    cellNationalNumber: "5553330001",
                    timeZoneId: "America/Chicago");

                // Matches the time zone, but not the phone filter.
                await SaveContactAsync(
                    seedSession,
                    cellPhoneNumber: "+15554440002",
                    cellNationalNumber: "5554440002",
                    timeZoneId: "America/New_York");

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewBatch(ActivitySources.Manual, OmnichannelConstants.Channels.Phone);
            batch.PhoneNumber = "555333";
            batch.PhoneNumberMatchType = PhoneNumberMatchType.BeginsWith;
            batch.TimeZoneIds = ["America/New_York"];

            // Act
            await LoadAsync(store, connectionString, batch);

            // Assert
            var activities = await ListLoadedActivitiesAsync(store);

            Assert.Empty(activities);
            Assert.Equal(0L, batch.TotalLoaded.GetValueOrDefault());
            Assert.Equal(OmnichannelActivityBatchStatus.Loaded, batch.Status);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task LoadAsync_WithMoreContactsThanOnePage_LoadsEveryContactExactlyOnce()
    {
        // Arrange
        // The loader walks the contacts a hundred at a time on a moving document-id cursor. A cursor that advances
        // wrongly does not fail loudly: it silently drops leads out of the campaign, or dials the same lead twice.
        // Seeding more than two pages is what makes the cursor arithmetic observable at all.
        var databasePath = DatabasePath("paging");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);
        const int contactCount = 250;

        try
        {
            var contactIds = new List<string>();

            await using (var seedSession = store.CreateSession())
            {
                for (var i = 0; i < contactCount; i++)
                {
                    contactIds.Add(await SaveContactAsync(
                        seedSession,
                        cellPhoneNumber: $"+1555{i:D7}",
                        cellNationalNumber: $"555{i:D7}"));
                }

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewBatch(ActivitySources.Manual, OmnichannelConstants.Channels.Phone);

            // Act
            await LoadAsync(store, connectionString, batch);

            // Assert
            var activities = await ListLoadedActivitiesAsync(store);
            var loadedContactIds = activities.Select(activity => activity.ContactContentItemId).ToArray();

            Assert.Equal(contactCount, activities.Count);
            Assert.Equal(loadedContactIds.Length, loadedContactIds.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(
                contactIds.OrderBy(id => id, StringComparer.Ordinal),
                loadedContactIds.OrderBy(id => id, StringComparer.Ordinal));
            Assert.Equal(contactCount, batch.TotalLoaded.GetValueOrDefault());
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static async Task LoadAsync(IStore store, string connectionString, OmnichannelActivityBatch batch)
    {
        await using var session = store.CreateSession();

        var loader = CreateLoader(session, store, connectionString, batch.Source);
        var context = new ActivityBatchLoadContext(batch, LoaderId, LoaderUserName);

        await loader.LoadAsync(context, TestContext.Current.CancellationToken);
    }

    private static DefaultContactActivityBatchLoader CreateLoader(
        ISession session,
        IStore store,
        string connectionString,
        string source)
    {
        var sourceOptions = new ActivityBatchSourceOptions();

        // User assignment is a separate concern with its own store; these batches leave the activities unassigned so
        // the loader never has to resolve users.
        sourceOptions.AddSource(source, entry => entry.RequiresUserAssignment = false);

        return new DefaultContactActivityBatchLoader(
            CreateBatchCatalog(),
            session,
            new UtcLocalClock(),
            new StubClock(_now),
            new StubSubjectFlowSettingsService(new SubjectFlowSettings
            {
                SubjectContentType = SubjectContentType,
                Channel = OmnichannelConstants.Channels.Phone,
                ChannelEndpointId = "flow-endpoint",
            }),
            CreateActivityManager(),
            store,
            new SqliteDbConnectionAccessor(connectionString),
            [],
            Options.Create(sourceOptions),
            NullLogger<DefaultContactActivityBatchLoader>.Instance);
    }

    /// <summary>
    /// The catalog is where the batch's own progress is written. The loader mutates the batch instance it was handed
    /// and hands that same instance to the catalog, so the tests read the outcome off the instance and this double
    /// only has to accept the writes.
    /// </summary>
    private static ICatalog<OmnichannelActivityBatch> CreateBatchCatalog()
    {
        var catalog = new Mock<ICatalog<OmnichannelActivityBatch>>();

        catalog
            .Setup(x => x.UpdateAsync(It.IsAny<OmnichannelActivityBatch>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        return catalog.Object;
    }

    /// <summary>
    /// The manager hands out blank activities and is asked to create them; the loader is what fills them in and
    /// persists them through the session, which is where the tests read them back from. Each call must yield its own
    /// instance -- a shared one would make every contact in a batch overwrite the last.
    /// </summary>
    private static IOmnichannelActivityManager CreateActivityManager()
    {
        var manager = new Mock<IOmnichannelActivityManager>();

        manager
            .Setup(x => x.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new OmnichannelActivity
            {
                ItemId = IdGenerator.GenerateId(),
            }));

        manager
            .Setup(x => x.CreateAsync(It.IsAny<OmnichannelActivity>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        return manager.Object;
    }

    private static OmnichannelActivityBatch NewBatch(string source, string channel)
        => new()
        {
            ItemId = IdGenerator.GenerateId(),
            DisplayText = "Outreach batch",
            Source = source,
            Channel = channel,
            ChannelEndpointId = "batch-endpoint",
            SubjectContentType = SubjectContentType,
            ContactContentType = ContactContentType,
            Status = OmnichannelActivityBatchStatus.Loading,
            CreatedUtc = _now.AddHours(-1),
            ScheduleAt = _now,
            OnlyPublishedLeads = true,

            // The coordinator resets the counter before it hands a batch to a loader, and the limit check reads it.
            TotalLoaded = 0,
        };

    private static async Task<IReadOnlyList<OmnichannelActivity>> ListLoadedActivitiesAsync(IStore store)
    {
        await using var session = store.CreateSession();

        // Only the activities this load created: the seeded ones carry no creator.
        var activities = await session
            .Query<OmnichannelActivity, OmnichannelActivityIndex>(
                index => index.CreatedById == LoaderId,
                collection: OmnichannelConstants.CollectionName)
            .ListAsync(TestContext.Current.CancellationToken);

        return activities.ToArray();
    }

    private static async Task<string> SaveContactAsync(
        ISession session,
        string cellPhoneNumber = null,
        string cellNationalNumber = null,
        string emailAddress = null,
        string timeZoneId = null,
        bool doNotCall = false,
        bool doNotSms = false,
        bool doNotEmail = false)
    {
        var contact = new ContentItem
        {
            ContentItemId = IdGenerator.GenerateId(),
            ContentItemVersionId = IdGenerator.GenerateId(),
            ContentType = ContactContentType,
            DisplayText = "Contact",
            Published = true,
            Latest = true,
            CreatedUtc = _now.AddDays(-30),
            ModifiedUtc = _now.AddDays(-30),
            PublishedUtc = _now.AddDays(-30),
        };

        contact.Alter<OmnichannelContactPart>(part =>
        {
            part.TimeZoneId = timeZoneId;
            part.SetDoNotCall(doNotCall, _now);
            part.SetDoNotSms(doNotSms, _now);
            part.SetDoNotEmail(doNotEmail, _now);
        });

        var contactMethods = contact.GetOrCreate<BagPart>(OmnichannelConstants.NamedParts.ContactMethods);
        contactMethods.ContentItems ??= [];

        if (!string.IsNullOrEmpty(cellPhoneNumber))
        {
            contactMethods.ContentItems.Add(NewPhoneNumberContactMethod(cellPhoneNumber, cellNationalNumber));
        }

        if (!string.IsNullOrEmpty(emailAddress))
        {
            contactMethods.ContentItems.Add(NewEmailAddressContactMethod(emailAddress));
        }

        contact.Apply(OmnichannelConstants.NamedParts.ContactMethods, contactMethods);

        await session.SaveAsync(contact, cancellationToken: TestContext.Current.CancellationToken);

        return contact.ContentItemId;
    }

    private static ContentItem NewPhoneNumberContactMethod(string phoneNumber, string nationalNumber)
    {
        var contactMethod = new ContentItem
        {
            ContentItemId = IdGenerator.GenerateId(),
            ContentItemVersionId = IdGenerator.GenerateId(),
            ContentType = OmnichannelConstants.ContentTypes.PhoneNumber,
            DisplayText = $"Cell: {phoneNumber}",
        };

        contactMethod.Alter<PhoneNumberInfoPart>(part =>
        {
            part.Number = new PhoneField
            {
                PhoneNumber = phoneNumber,
                CountryCode = "US",
                NationalNumber = nationalNumber,
            };
            part.Type = new TextField
            {
                Text = "Cell",
            };
        });

        return contactMethod;
    }

    private static ContentItem NewEmailAddressContactMethod(string emailAddress)
    {
        var contactMethod = new ContentItem
        {
            ContentItemId = IdGenerator.GenerateId(),
            ContentItemVersionId = IdGenerator.GenerateId(),
            ContentType = OmnichannelConstants.ContentTypes.EmailAddress,
            DisplayText = emailAddress,
        };

        contactMethod.Alter<EmailInfoPart>(part =>
        {
            part.Email = new TextField
            {
                Text = emailAddress,
            };
        });

        return contactMethod;
    }

    private static Task SaveExistingActivityAsync(
        ISession session,
        string contactContentItemId,
        ActivityStatus status,
        string subjectContentType = SubjectContentType,
        DateTime? completedUtc = null)
    {
        return session.SaveAsync(
            new OmnichannelActivity
            {
                ItemId = IdGenerator.GenerateId(),
                Kind = ActivityKind.Call,
                Source = ActivitySources.Manual,
                Channel = OmnichannelConstants.Channels.Phone,
                ChannelEndpointId = "existing-endpoint",
                ContactContentItemId = contactContentItemId,
                ContactContentType = ContactContentType,
                SubjectContentType = subjectContentType,
                PreferredDestination = "+15555559999",
                ScheduledUtc = _now.AddDays(-1),
                CreatedUtc = _now.AddDays(-1),
                CompletedUtc = completedUtc,
                InteractionType = ActivityInteractionType.Manual,
                Status = status,
            },
            collection: OmnichannelConstants.CollectionName,
            cancellationToken: TestContext.Current.CancellationToken);
    }

    private static async Task<IStore> CreateStoreAsync(string connectionString)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite(connectionString));

        store.RegisterIndexes(
        [
            new ContentItemIndexProvider(),
            CreateContactIndexProvider(),
            new OmnichannelActivityIndexProvider(),
        ]);

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(
            OmnichannelConstants.CollectionName,
            TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<ContentItemIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentItemVersionId", column => column.WithLength(26))
            .Column<bool>("Published")
            .Column<bool>("Latest")
            .Column<string>("ContentType", column => column.WithLength(255))
            .Column<DateTime>("ModifiedUtc")
            .Column<DateTime>("PublishedUtc")
            .Column<DateTime>("CreatedUtc")
            .Column<string>("Owner", column => column.WithLength(255))
            .Column<string>("Author", column => column.WithLength(255))
            .Column<string>("DisplayText", column => column.WithLength(255)));

        await schemaBuilder.CreateMapIndexTableAsync<OmnichannelContactIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<bool>("Published")
            .Column<bool>("Latest")
            .Column<string>("TimeZoneId", column => column.WithLength(64))
            .Column<string>("PrimaryCellPhoneNumber", column => column.WithLength(50))
            .Column<string>("NormalizedPrimaryCellPhoneNumber", column => column.WithLength(50))
            .Column<string>("PrimaryHomePhoneNumber", column => column.WithLength(50))
            .Column<string>("NormalizedPrimaryHomePhoneNumber", column => column.WithLength(50))
            .Column<string>("PrimaryEmailAddress", column => column.WithLength(255)));

        await schemaBuilder.CreateMapIndexTableAsync<OmnichannelActivityIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<ActivityKind>("Kind")
            .Column<string>("Source", column => column.WithLength(50))
            .Column<string>("Channel", column => column.WithLength(50))
            .Column<string>("ChannelEndpointId", column => column.WithLength(26))
            .Column<string>("PreferredDestination", column => column.WithLength(255))
            .Column<string>("ContactContentItemId", column => column.WithLength(26))
            .Column<string>("ContactContentType", column => column.WithLength(255))
            .Column<string>("CampaignId", column => column.WithLength(26))
            .Column<string>("SubjectContentType", column => column.WithLength(26))
            .Column<DateTime>("ScheduledUtc", column => column.NotNull())
            .Column<DateTime>("CompletedUtc")
            .Column<int>("Attempts", column => column.NotNull())
            .Column<string>("AssignedToId", column => column.WithLength(26))
            .Column<DateTime>("AssignedToUtc")
            .Column<ActivityAssignmentStatus>("AssignmentStatus")
            .Column<string>("ReservationId", column => column.WithLength(26))
            .Column<string>("ReservedById", column => column.WithLength(26))
            .Column<DateTime>("ReservedUtc")
            .Column<DateTime>("ReservationExpiresUtc")
            .Column<string>("CreatedById", column => column.WithLength(26))
            .Column<string>("DispositionId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<ActivityUrgencyLevel>("UrgencyLevel")
            .Column<ActivityStatus>("Status")
            .Column<ActivityInteractionType>("InteractionType")
            .Column<bool>("AiEscalated"),
            collection: OmnichannelConstants.CollectionName);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    /// <summary>
    /// The contact index provider is internal to its module, so it is constructed the way the existing contact-index
    /// test does rather than being reimplemented here: the phone and time-zone filters are only meaningful against
    /// the real index the module writes.
    /// </summary>
    private static IIndexProvider CreateContactIndexProvider()
    {
        var providerType = typeof(CrestApps.OrchardCore.Omnichannel.Managements.Startup).Assembly.GetType(
            "CrestApps.OrchardCore.Omnichannel.Managements.Indexes.OmnichannelContactIndexProvider",
            throwOnError: true);

        return (IIndexProvider)Activator.CreateInstance(providerType, new object[] { new DefaultPhoneNumberService() });
    }

    private static string DatabasePath(string suffix)
    {
        return Path.Combine(
            Path.GetTempPath(),
            $"contact-activity-batch-loader-{suffix}-{Guid.NewGuid():N}.db");
    }

    private sealed class SqliteDbConnectionAccessor : IDbConnectionAccessor
    {
        private readonly string _connectionString;

        public SqliteDbConnectionAccessor(string connectionString)
        {
            _connectionString = connectionString;
        }

        public DbConnection CreateConnection()
            => new SqliteConnection(_connectionString);
    }

    /// <summary>
    /// A local clock whose time zone is UTC, so the schedule and lead-date conversions are identity operations and a
    /// test never has to reason about an ambient machine time zone.
    /// </summary>
    private sealed class UtcLocalClock : ILocalClock
    {
        public Task<DateTimeOffset> GetLocalNowAsync()
            => Task.FromResult(new DateTimeOffset(_now, TimeSpan.Zero));

        public Task<ITimeZone> GetLocalTimeZoneAsync()
            => throw new NotSupportedException();

        public Task<DateTimeOffset> ConvertToLocalAsync(DateTimeOffset utcDateTime)
            => Task.FromResult(utcDateTime);

        public Task<DateTime> ConvertToUtcAsync(DateTime dateTimeLocal)
            => Task.FromResult(DateTime.SpecifyKind(dateTimeLocal, DateTimeKind.Utc));
    }

    private sealed class StubSubjectFlowSettingsService : ISubjectFlowSettingsService
    {
        private readonly SubjectFlowSettings _settings;

        public StubSubjectFlowSettingsService(SubjectFlowSettings settings)
        {
            _settings = settings;
        }

        public Task<IReadOnlyList<SubjectFlowSettings>> GetConfiguredFlowSettingsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SubjectFlowSettings>>([_settings]);

        public Task<SubjectFlowSettings> FindConfiguredFlowSettingsAsync(string subjectContentType, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Equals(_settings.SubjectContentType, subjectContentType, StringComparison.Ordinal)
                ? _settings
                : null);

        public Task<IReadOnlyList<ContentTypeDefinition>> GetConfiguredSubjectTypesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ContentTypeDefinition>>([]);

        public Task<IReadOnlyList<ContentTypeDefinition>> GetConfiguredSubjectTypesAsync(SubjectDirection direction, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ContentTypeDefinition>>([]);

        public bool IsConfigured(SubjectFlowSettings flowSettings)
            => flowSettings is not null;
    }
}
