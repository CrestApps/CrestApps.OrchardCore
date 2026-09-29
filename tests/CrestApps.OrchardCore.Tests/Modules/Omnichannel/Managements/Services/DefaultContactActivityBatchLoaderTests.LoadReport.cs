using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Tests.Utilities;
using OrchardCore;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

/// <summary>
/// An inventory load filtered to one exact number found nobody, although Manage Content listed two contacts holding it.
/// Nothing said why: the duplicate check had counted an open activity under any subject, a shared-number opt-out
/// could exclude both records, and the load reported only how many it created. These tests hold the loader to
/// finding the number in whatever shape it was stored, to the subject-only duplicate rule its label promises, and to
/// accounting for every matching contact it did not load.
/// </summary>
public sealed partial class DefaultContactActivityBatchLoaderTests
{
    [Theory]
    [InlineData("5555550101")]
    [InlineData("(555) 555-0101")]
    [InlineData("+15555550101")]
    [InlineData("15555550101")]
    public async Task LoadAsync_WhenExactPhoneFilterMatchesAnyShapeOfTheNumber_FindsEveryContactHoldingIt(string phoneFilter)
    {
        // Arrange
        // Two records hold the same number: one entered through the editor with its country, one imported raw
        // without one, so its E.164 column is empty and its national column carries the country code. An exact
        // search for the number, typed nationally, internationally or with the country code but no plus, has to find
        // both -- and must not widen to a different number.
        var databasePath = DatabasePath($"exact-phone-{Guid.NewGuid():N}");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            string canonicalContactId;
            string rawImportedContactId;

            await using (var seedSession = store.CreateSession())
            {
                canonicalContactId = await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550101", cellNationalNumber: "5555550101");
                rawImportedContactId = await SaveRawImportedContactAsync(seedSession, "15555550101");
                await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550199", cellNationalNumber: "5555550199");

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewBatch(ActivitySources.Manual, OmnichannelConstants.Channels.Phone);
            batch.PhoneNumber = phoneFilter;
            batch.PhoneNumberMatchType = PhoneNumberMatchType.Exact;

            // Act
            await LoadAsync(store, connectionString, batch);

            // Assert
            var activities = await ListLoadedActivitiesAsync(store);

            Assert.Equal(
                new[] { canonicalContactId, rawImportedContactId }.OrderBy(id => id, StringComparer.Ordinal),
                activities.Select(activity => activity.ContactContentItemId).OrderBy(id => id, StringComparer.Ordinal));
            Assert.Equal(2L, batch.TotalMatched);
            Assert.Equal(2L, batch.TotalLoaded);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenPreventDuplicatesAndTheOpenActivityIsForAnotherSubject_LoadsTheContact()
    {
        // Arrange
        // The option reads "prevent duplicate activity with the same subject". The query ignored the subject, so an
        // open task of any other kind silently kept the contact out of the load.
        var databasePath = DatabasePath("prevent-duplicates-other-subject");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            string contactId;

            await using (var seedSession = store.CreateSession())
            {
                contactId = await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550201", cellNationalNumber: "5555550201");

                await SaveExistingActivityAsync(seedSession, contactId, ActivityStatus.InProgress, PriorSubjectContentType);

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewBatch(ActivitySources.Manual, OmnichannelConstants.Channels.Phone);
            batch.PreventDuplicates = true;

            // Act
            await LoadAsync(store, connectionString, batch);

            // Assert
            var activity = Assert.Single(await ListLoadedActivitiesAsync(store));

            Assert.Equal(contactId, activity.ContactContentItemId);
            Assert.Equal(0L, batch.TotalSkippedAsDuplicate);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenPreventDuplicatesAndTheOpenActivityIsForTheSameSubject_SkipsAndCountsTheContact()
    {
        // Arrange
        // The half of the rule that must keep working, now reported: the operator sees that one of the two matching
        // contacts was left out because it is already being worked for this subject.
        var databasePath = DatabasePath("prevent-duplicates-same-subject");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            string busyContactId;
            string freeContactId;

            await using (var seedSession = store.CreateSession())
            {
                busyContactId = await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550301", cellNationalNumber: "5555550301");
                freeContactId = await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550302", cellNationalNumber: "5555550302");

                await SaveExistingActivityAsync(seedSession, busyContactId, ActivityStatus.Scheduled);

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewBatch(ActivitySources.Manual, OmnichannelConstants.Channels.Phone);
            batch.PreventDuplicates = true;

            // Act
            await LoadAsync(store, connectionString, batch);

            // Assert
            var activity = Assert.Single(await ListLoadedActivitiesAsync(store));

            Assert.Equal(freeContactId, activity.ContactContentItemId);
            Assert.Equal(2L, batch.TotalMatched);
            Assert.Equal(1L, batch.TotalLoaded);
            Assert.Equal(1L, batch.TotalSkippedAsDuplicate);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenASharedNumberOptOutExcludesTwoRecords_CountsThemApartFromTheOwnOptOut()
    {
        // Arrange
        // One opted-out record can take every other record at its number out of a load. That is intended, but it was
        // invisible: the operator saw a short load and no reason. The record's own opt-out and the records excluded
        // because of it are counted separately so the report says which happened.
        var databasePath = DatabasePath("shared-number-opt-out-counted");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            string unrelatedContactId;

            await using (var seedSession = store.CreateSession())
            {
                await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550401", cellNationalNumber: "5555550401", doNotCall: true);
                await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550402", cellNationalNumber: "5555550402", homePhoneNumber: "+15555550401", homeNationalNumber: "5555550401");
                await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550401", cellNationalNumber: "5555550401");
                unrelatedContactId = await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550403", cellNationalNumber: "5555550403");

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewBatch(ActivitySources.Manual, OmnichannelConstants.Channels.Phone);

            // Act
            await LoadAsync(store, connectionString, batch);

            // Assert
            var activity = Assert.Single(await ListLoadedActivitiesAsync(store));

            Assert.Equal(unrelatedContactId, activity.ContactContentItemId);
            Assert.Equal(4L, batch.TotalMatched);
            Assert.Equal(1L, batch.TotalLoaded);
            Assert.Equal(1L, batch.TotalSkippedAsOptedOut);
            Assert.Equal(2L, batch.TotalSkippedAsSharedNumberOptedOut);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenAnAutomatedContactHasNoAddressOnTheChannel_CountsThemAsHavingNoDestination()
    {
        // Arrange
        // An automated activity needs somewhere to send to. A contact with only an email address cannot be called,
        // and until now was dropped without a word.
        var databasePath = DatabasePath("no-destination-counted");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveContactAsync(seedSession, emailAddress: "no-phone@example.com");
                await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550501", cellNationalNumber: "5555550501");

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewBatch(ActivitySources.Automatic, OmnichannelConstants.Channels.Phone);

            // Act
            await LoadAsync(store, connectionString, batch);

            // Assert
            Assert.Single(await ListLoadedActivitiesAsync(store));
            Assert.Equal(2L, batch.TotalMatched);
            Assert.Equal(1L, batch.TotalSkippedForNoDestination);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadAsync_WhenTheLimitCutsTheLoad_CountsTheMatchingContactsItLeftOut(bool withPhoneFilter)
    {
        // Arrange
        // The limit stops the load before it reaches the rest of the list, so the rest was never counted and the
        // report could not say how many more there were. Counted both ways the loader can page: over every contact,
        // and over a pre-computed filter set, where a contact outside the set must not be counted.
        var databasePath = DatabasePath($"limit-counted-{withPhoneFilter}");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                for (var i = 0; i < 5; i++)
                {
                    await SaveContactAsync(seedSession, cellPhoneNumber: $"+1555555060{i}", cellNationalNumber: $"555555060{i}");
                }

                await SaveContactAsync(seedSession, cellPhoneNumber: "+15554440609", cellNationalNumber: "5554440609");

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewBatch(ActivitySources.Manual, OmnichannelConstants.Channels.Phone);
            batch.Limit = 2;

            if (withPhoneFilter)
            {
                batch.PhoneNumber = "5555550";
                batch.PhoneNumberMatchType = PhoneNumberMatchType.BeginsWith;
            }

            // Act
            await LoadAsync(store, connectionString, batch);

            // Assert
            var expectedMatched = withPhoneFilter ? 5L : 6L;

            Assert.Equal(2, (await ListLoadedActivitiesAsync(store)).Count);
            Assert.Equal(2L, batch.TotalLoaded);
            Assert.Equal(expectedMatched, batch.TotalMatched);
            Assert.Equal(expectedMatched - 2, batch.TotalSkippedByLimit);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenTheSameBatchIsLoadedAgain_ReportsOnlyTheSecondLoad()
    {
        // Arrange
        // A reload that added its counts to the first load's would report contacts twice. The first load creates an
        // open activity for each contact; the second, preventing duplicates, must report both as duplicates and
        // nothing loaded -- not four matches and two loaded.
        var databasePath = DatabasePath("reload-resets-counts");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550701", cellNationalNumber: "5555550701");
                await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550702", cellNationalNumber: "5555550702");

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewBatch(ActivitySources.Manual, OmnichannelConstants.Channels.Phone);
            batch.PreventDuplicates = true;

            await LoadAsync(store, connectionString, batch);

            Assert.Equal(2L, batch.TotalLoaded);

            batch.Status = OmnichannelActivityBatchStatus.Loading;

            // Act
            await LoadAsync(store, connectionString, batch);

            // Assert
            Assert.Equal(2L, batch.TotalMatched);
            Assert.Equal(0L, batch.TotalLoaded);
            Assert.Equal(2L, batch.TotalSkippedAsDuplicate);
            Assert.Equal(0L, batch.TotalSkippedByLimit);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenTheFiltersMatchNobody_ReportsNoMatchingContacts()
    {
        // Arrange
        // A load whose filters found nobody must say so rather than look like a load that simply created nothing.
        var databasePath = DatabasePath("filters-match-nobody-reported");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var store = await CreateStoreAsync(connectionString);

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await SaveContactAsync(seedSession, cellPhoneNumber: "+15555550801", cellNationalNumber: "5555550801");

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var batch = NewBatch(ActivitySources.Manual, OmnichannelConstants.Channels.Phone);
            batch.PhoneNumber = "5555550899";
            batch.PhoneNumberMatchType = PhoneNumberMatchType.Exact;

            // Act
            await LoadAsync(store, connectionString, batch);

            // Assert
            Assert.Empty(await ListLoadedActivitiesAsync(store));
            Assert.Equal(OmnichannelActivityBatchStatus.Loaded, batch.Status);
            Assert.Equal(0L, batch.TotalMatched);
            Assert.Equal(0L, batch.TotalLoaded);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public void Clone_WhenTheBatchCarriesALoadReport_CopiesEveryCountAndTheProperties()
    {
        // Arrange
        // The catalog saves through a clone. A count the clone left behind would be stored as zero and the report
        // would read as though nothing had been skipped.
        var batch = new OmnichannelActivityBatch
        {
            TotalLoaded = 1,
            TotalMatched = 7,
            TotalSkippedAsDuplicate = 2,
            TotalSkippedAsOptedOut = 1,
            TotalSkippedAsSharedNumberOptedOut = 1,
            TotalSkippedForNoDestination = 1,
            TotalSkippedByLimit = 1,
            Properties = new Dictionary<string, object> { ["Custom"] = "value" },
        };

        // Act
        var clone = batch.Clone();

        // Assert
        Assert.Equal(1L, clone.TotalLoaded);
        Assert.Equal(7L, clone.TotalMatched);
        Assert.Equal(2L, clone.TotalSkippedAsDuplicate);
        Assert.Equal(1L, clone.TotalSkippedAsOptedOut);
        Assert.Equal(1L, clone.TotalSkippedAsSharedNumberOptedOut);
        Assert.Equal(1L, clone.TotalSkippedForNoDestination);
        Assert.Equal(1L, clone.TotalSkippedByLimit);
        Assert.True(clone.Properties.ContainsKey("Custom"));
    }

    /// <summary>
    /// A contact whose number arrived from an import without a country: it cannot be canonicalised, so the index keeps
    /// its digits in the national column and leaves the E.164 column empty.
    /// </summary>
    private static async Task<string> SaveRawImportedContactAsync(ISession session, string rawPhoneNumber)
    {
        var contact = new ContentItem
        {
            ContentItemId = IdGenerator.GenerateId(),
            ContentItemVersionId = IdGenerator.GenerateId(),
            ContentType = ContactContentType,
            DisplayText = "Imported contact",
            Published = true,
            Latest = true,
            CreatedUtc = _now.AddDays(-30),
            ModifiedUtc = _now.AddDays(-30),
            PublishedUtc = _now.AddDays(-30),
        };

        contact.Alter<OmnichannelContactPart>(_ => { });

        var contactMethod = new ContentItem
        {
            ContentItemId = IdGenerator.GenerateId(),
            ContentItemVersionId = IdGenerator.GenerateId(),
            ContentType = OmnichannelConstants.ContentTypes.PhoneNumber,
            DisplayText = rawPhoneNumber,
        };

        contactMethod.Alter<PhoneNumberInfoPart>(part =>
        {
            part.Number = new PhoneField
            {
                PhoneNumber = rawPhoneNumber,
            };
            part.Type = new TextField
            {
                Text = "Cell",
            };
        });

        var contactMethods = contact.GetOrCreate<BagPart>(OmnichannelConstants.NamedParts.ContactMethods);
        contactMethods.ContentItems = [contactMethod];
        contact.Apply(OmnichannelConstants.NamedParts.ContactMethods, contactMethods);

        await session.SaveAsync(contact, cancellationToken: TestContext.Current.CancellationToken);

        return contact.ContentItemId;
    }
}
