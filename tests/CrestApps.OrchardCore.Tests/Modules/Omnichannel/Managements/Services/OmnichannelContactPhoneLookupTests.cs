using System.Linq.Expressions;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Moq;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

public sealed class OmnichannelContactPhoneLookupTests
{
    [Fact]
    public async Task FindByNumbersAsync_FindsARecordByEveryNumberWhateverItsType()
    {
        var (connectionString, path) = ContactPhoneTestStore.CreateDatabase("phone-lookup-types");
        var store = await ContactPhoneTestStore.CreateAsync(connectionString);

        try
        {
            // Arrange
            var company = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Work", "+17025550104"));
            var person = ContactPhoneTestStore.CreateRecord(
                ContactPhoneTestStore.ContactContentType,
                ("Cell", "+17025550101"),
                ("Cell", "+17025550102"),
                ("Office", "+17025550105"));
            await ContactPhoneTestStore.SaveAsync(store, company, person);

            await using var session = store.CreateSession();
            var lookup = ContactPhoneTestStore.CreateLookup(session);

            // Act
            var byWork = await lookup.FindByNumbersAsync(["+17025550104"], OmnichannelContactVersion.Published, cancellationToken: TestContext.Current.CancellationToken);
            var bySecondCell = await lookup.FindByNumbersAsync(["+17025550102"], OmnichannelContactVersion.Published, cancellationToken: TestContext.Current.CancellationToken);
            var byOffice = await lookup.FindByNumbersAsync(["+17025550105"], OmnichannelContactVersion.Latest, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(company.ContentItemId, Assert.Single(byWork).ContentItemId);
            Assert.Equal("Work", byWork[0].PhoneType);
            Assert.Equal(person.ContentItemId, Assert.Single(bySecondCell).ContentItemId);
            Assert.Equal(person.ContentItemId, Assert.Single(byOffice).ContentItemId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    [Fact]
    public async Task FindByNumbersAsync_WhenContentTypesAreGiven_KeepsOnlyThoseTypes()
    {
        var (connectionString, path) = ContactPhoneTestStore.CreateDatabase("phone-lookup-content-types");
        var store = await ContactPhoneTestStore.CreateAsync(connectionString);

        try
        {
            // Arrange
            var contact = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Work", "+17025550104"));
            var lead = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.LeadContentType, ("Cell", "+17025550104"));
            await ContactPhoneTestStore.SaveAsync(store, contact, lead);

            await using var session = store.CreateSession();
            var lookup = ContactPhoneTestStore.CreateLookup(session);

            // Act
            var all = await lookup.FindByNumbersAsync(["+17025550104"], OmnichannelContactVersion.Published, cancellationToken: TestContext.Current.CancellationToken);
            var contactsOnly = await lookup.FindByNumbersAsync(["+17025550104"], OmnichannelContactVersion.Published, [ContactPhoneTestStore.ContactContentType], TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(2, all.Count);
            Assert.Equal(contact.ContentItemId, Assert.Single(contactsOnly).ContentItemId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    [Fact]
    public async Task FindByNumbersAsync_WhenARecordHasAPublishedVersionAndADraft_ReadsTheVersionAskedFor()
    {
        var (connectionString, path) = ContactPhoneTestStore.CreateDatabase("phone-lookup-versions");
        var store = await ContactPhoneTestStore.CreateAsync(connectionString);

        try
        {
            // Arrange
            var published = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Work", "+17025550104"));
            published.Latest = false;

            var draft = ContactPhoneTestStore.CreateRecord(
                ContactPhoneTestStore.ContactContentType,
                ("Work", "+17025550104"),
                ("Cell", "+17025550101"));
            draft.ContentItemId = published.ContentItemId;
            draft.Published = false;

            await ContactPhoneTestStore.SaveAsync(store, published, draft);

            await using var session = store.CreateSession();
            var lookup = ContactPhoneTestStore.CreateLookup(session);

            // Act
            var any = await lookup.FindByNumbersAsync(["+17025550104"], OmnichannelContactVersion.Any, cancellationToken: TestContext.Current.CancellationToken);
            var draftOnly = await lookup.FindByNumbersAsync(["+17025550101"], OmnichannelContactVersion.Published, cancellationToken: TestContext.Current.CancellationToken);
            var latest = await lookup.FindByNumbersAsync(["+17025550101"], OmnichannelContactVersion.Latest, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            // The same number of the same record is one row, whichever versions hold it.
            Assert.Equal(published.ContentItemId, Assert.Single(any).ContentItemId);
            Assert.Empty(draftOnly);
            Assert.Equal(published.ContentItemId, Assert.Single(latest).ContentItemId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    [Fact]
    public async Task GetNumbersAsync_ReturnsEveryNumberOfTheRecords()
    {
        var (connectionString, path) = ContactPhoneTestStore.CreateDatabase("phone-lookup-numbers");
        var store = await ContactPhoneTestStore.CreateAsync(connectionString);

        try
        {
            // Arrange
            var contact = ContactPhoneTestStore.CreateRecord(
                ContactPhoneTestStore.ContactContentType,
                ("Cell", "+17025550101"),
                ("Work", "+17025550104"),
                ("Other", "+17025550106"));
            await ContactPhoneTestStore.SaveAsync(store, contact);

            await using var session = store.CreateSession();
            var lookup = ContactPhoneTestStore.CreateLookup(session);

            // Act
            var numbers = await lookup.GetNumbersAsync([contact.ContentItemId], OmnichannelContactVersion.Any, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(
                ["+17025550101", "+17025550104", "+17025550106"],
                numbers.Select(row => row.NormalizedPhoneNumber).Order(StringComparer.Ordinal));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    [Fact]
    public async Task Lookups_UntilTheBackfillHasFinished_StillFindARecordSavedBeforeTheIndexByItsCellAndHomeNumbers()
    {
        var (connectionString, path) = ContactPhoneTestStore.CreateDatabase("phone-lookup-fallback");

        // A record saved before the upgrade has a contact index row but no phone number rows.
        var legacyStore = await ContactPhoneTestStore.CreateAsync(connectionString, withPhoneNumberIndex: false);
        var contact = ContactPhoneTestStore.CreateRecord(
            ContactPhoneTestStore.ContactContentType,
            ("Cell", "+17025550101"),
            ("Home", "+17025550103"),
            ("Work", "+17025550104"));
        await ContactPhoneTestStore.SaveAsync(legacyStore, contact);

        try
        {
            await using var session = legacyStore.CreateSession();
            var pending = ContactPhoneTestStore.CreateLookup(session, backfilled: false);
            var finished = ContactPhoneTestStore.CreateLookup(session, backfilled: true);

            // Act
            var byCell = await pending.FindByNumbersAsync(["+17025550101"], OmnichannelContactVersion.Published, cancellationToken: TestContext.Current.CancellationToken);
            var byHome = await pending.FindByNumbersAsync(["+17025550103"], OmnichannelContactVersion.Published, cancellationToken: TestContext.Current.CancellationToken);
            var numbers = await pending.GetNumbersAsync([contact.ContentItemId], OmnichannelContactVersion.Any, TestContext.Current.CancellationToken);
            var afterBackfill = await finished.FindByNumbersAsync(["+17025550101"], OmnichannelContactVersion.Published, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(contact.ContentItemId, Assert.Single(byCell).ContentItemId);
            Assert.Equal(contact.ContentItemId, Assert.Single(byHome).ContentItemId);
            Assert.Equal(["+17025550101", "+17025550103"], numbers.Select(row => row.NormalizedPhoneNumber).Order(StringComparer.Ordinal));

            // Once the backfill has finished, the phone number index alone is read: this record was never indexed
            // again in this test, so it is not found, which shows the rows above came from the fallback.
            Assert.Empty(afterBackfill);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(legacyStore, path);
        }
    }

    [Theory]
    [InlineData(true, 1, 1)]
    [InlineData(true, 5_000, 100)]
    [InlineData(false, 5_000, 300)]
    public async Task FindByNumbersAsync_ComparesOneIndexedColumnPerQueryInFixedSizeBatches(bool backfilled, int numberCount, int expectedQueries)
    {
        // Arrange
        // An OR across columns keeps SQL Server from seeking either column's index. While the backfill runs, the cell
        // and home columns are each read in a query of their own as well.
        var predicates = new List<LambdaExpression>();
        var session = CreateRecordingSession(predicates);
        var lookup = new OmnichannelContactPhoneLookup(session, ContactPhoneTestStore.CreateBackfillState(backfilled));
        var numbers = Enumerable.Range(0, numberCount).Select(i => $"+1702{i:D7}").ToArray();

        // Act
        await lookup.FindByNumbersAsync(numbers, OmnichannelContactVersion.Published, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedQueries, predicates.Count);
        Assert.All(predicates, predicate => Assert.DoesNotContain("OrElse", predicate.Body.ToString(), StringComparison.Ordinal));
    }

    private static ISession CreateRecordingSession(List<LambdaExpression> predicates)
    {
        var phoneQuery = new Mock<IQueryIndex<OmnichannelContactPhoneNumberIndex>>();
        phoneQuery
            .Setup(query => query.Where(It.IsAny<Expression<Func<OmnichannelContactPhoneNumberIndex, bool>>>()))
            .Callback<Expression<Func<OmnichannelContactPhoneNumberIndex, bool>>>(predicates.Add)
            .Returns(phoneQuery.Object);
        phoneQuery
            .Setup(query => query.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var contactQuery = new Mock<IQueryIndex<OmnichannelContactIndex>>();
        contactQuery
            .Setup(query => query.Where(It.IsAny<Expression<Func<OmnichannelContactIndex, bool>>>()))
            .Callback<Expression<Func<OmnichannelContactIndex, bool>>>(predicates.Add)
            .Returns(contactQuery.Object);
        contactQuery
            .Setup(query => query.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var rootQuery = new Mock<IQuery>();
        rootQuery.Setup(query => query.ForIndex<OmnichannelContactPhoneNumberIndex>()).Returns(phoneQuery.Object);
        rootQuery.Setup(query => query.ForIndex<OmnichannelContactIndex>()).Returns(contactQuery.Object);

        var session = new Mock<ISession>();
        session.Setup(currentSession => currentSession.Query(null)).Returns(rootQuery.Object);

        return session.Object;
    }
}
