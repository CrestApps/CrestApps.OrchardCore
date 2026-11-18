using System.Linq.Expressions;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Filters.Query;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

public sealed class OmnichannelContactPhoneContentsAdminListFilterProviderTests
{
    [Theory]
    [InlineData("phone:702499", "7024993350", "+17024993350")]
    [InlineData("phone-exact:7024993350", "7024993350", "+17024993350")]
    [InlineData("phone-starts:+1702", "7024993350", "+17024993350")]
    [InlineData("phone-ends:3350", "7024993350", "+17024993350")]
    public async Task Build_WhenPhoneTermIsParsed_AppliesExpectedPredicate(
        string filter,
        string nationalNumber,
        string e164Number)
    {
        // Arrange
        var builder = new QueryEngineBuilder<ContentItem>();
        var provider = new OmnichannelContactPhoneContentsAdminListFilterProvider();
        provider.Build(builder);

        Expression<Func<OmnichannelContactIndex, bool>> predicate = null;
        var indexedQuery = new Mock<IQuery<ContentItem, OmnichannelContactIndex>>();
        var query = new Mock<IQuery<ContentItem>>();
        query
            .Setup(x => x.With<OmnichannelContactIndex>(It.IsAny<Expression<Func<OmnichannelContactIndex, bool>>>()))
            .Callback<Expression<Func<OmnichannelContactIndex, bool>>>(value => predicate = value)
            .Returns(indexedQuery.Object);

        var matchingIndex = CreateIndex(nationalNumber, e164Number);
        var nonMatchingIndex = CreateIndex("5551112222", "+15551112222");

        // Act
        await builder.Build().Parse(filter).ExecuteAsync(query.Object);

        // Assert
        Assert.NotNull(predicate);
        Assert.True(predicate.Compile()(matchingIndex));
        Assert.False(predicate.Compile()(nonMatchingIndex));
    }

    [Theory]
    [InlineData("phone:555", PhoneNumberMatchType.Contains)]
    [InlineData("phone:+1555", PhoneNumberMatchType.Contains)]
    [InlineData("phone-exact:5555550101", PhoneNumberMatchType.Exact)]
    [InlineData("phone-exact:15555550101", PhoneNumberMatchType.Exact)]
    [InlineData("phone-exact:+15555550101", PhoneNumberMatchType.Exact)]
    [InlineData("phone-starts:555555", PhoneNumberMatchType.BeginsWith)]
    [InlineData("phone-ends:0101", PhoneNumberMatchType.EndsWith)]
    public async Task Build_WhenAPhoneTermIsParsed_MatchesExactlyWhatTheInventoryLoadMatches(string filter, PhoneNumberMatchType matchType)
    {
        // Arrange
        // Manage Content and the inventory load each had their own copy of the phone predicate, so a record listed by
        // one could be missed by the other. Both now use the shared predicate, and this compares the two row by row.
        var predicate = await CapturePredicateAsync(filter);
        var value = filter[(filter.IndexOf(':') + 1)..];

        Assert.True(PhoneNumberSearchTerm.TryParse(value, out var searchTerm));

        var shared = OmnichannelContactPhonePredicates.Match(searchTerm, matchType).Compile();
        var rows = new[]
        {
            CreateIndex("5555550101", "+15555550101"),
            CreateIndex("15555550101", null),
            CreateIndex("5555550199", "+15555550199"),
            CreateIndex("5551112222", "+15551112222"),
        };

        // Act
        var compiled = predicate.Compile();
        var provider = rows.Select(row => EvaluateLikeSql(compiled, row)).ToArray();
        var loader = rows.Select(row => EvaluateLikeSql(shared, row)).ToArray();

        // Assert
        Assert.Equal(loader, provider);
    }

    [Theory]
    [InlineData("phone-exact:5555550101")]
    [InlineData("phone-exact:15555550101")]
    [InlineData("phone-exact:+15555550101")]
    public async Task Build_WhenPhoneExactIsSearched_FindsANumberImportedWithoutACountry(string filter)
    {
        // Arrange
        // A number imported without a country keeps its digits, country code included, in the national column and
        // nothing in the E.164 column, so an exact search compared with only one of the two missed it.
        var predicate = await CapturePredicateAsync(filter);

        // Act
        var matches = predicate.Compile()(CreateIndex("15555550101", null));

        // Assert
        Assert.True(matches);
    }

    [Theory]
    [InlineData("702", true)]
    [InlineData("702 555", true)]
    [InlineData("(702) 555-0104", true)]
    [InlineData("702.555.0104", true)]
    [InlineData("+1 702", true)]
    [InlineData("70", false)]
    [InlineData("John 2", false)]
    [InlineData("Unit 702", false)]
    [InlineData("702+1", false)]
    [InlineData("", false)]
    public void TryParsePhoneLikeTerm_ReadsOnlyASearchWrittenLikeAPhoneNumber(string value, bool expected)
    {
        // Act
        var result = OmnichannelContactPhoneContentsAdminListFilterProvider.TryParsePhoneLikeTerm(value, out _);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("702555", true)]
    [InlineData("7025550104", true)]
    [InlineData("1702555", true)]
    [InlineData("+1702555", true)]
    [InlineData("5550104", false)]
    [InlineData("+1555", false)]
    public void SearchBoxMatch_MatchesTheNumbersThatStartWithTheSearch(string search, bool expected)
    {
        // Arrange
        Assert.True(PhoneNumberSearchTerm.TryParse(search, out var searchTerm));

        var predicate = OmnichannelContactPhonePredicates.SearchBoxMatch(searchTerm).Compile();
        var row = CreateIndex("7025550104", "+17025550104");

        // Act
        var matches = EvaluateLikeSql(predicate, row);

        // Assert
        Assert.Equal(expected, matches);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisplayTextOrPhoneMatch_FindsTheRecordsByNameOrByTheStartOfTheirNumber(bool usePhoneNumberIndex)
    {
        var (connectionString, path) = ContactPhoneTestStore.CreateDatabase("phone-search-box");
        var store = await ContactPhoneTestStore.CreateAsync(connectionString);

        try
        {
            // Arrange
            var vegas = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Cell", "+17025550104"));
            var losAngeles = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Home", "+13105557020"));
            var named = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType);
            named.DisplayText = "Unit 702";

            await ContactPhoneTestStore.SaveAsync(store, vegas, losAngeles, named);

            // Act
            var nationalMatches = await SearchAsync(store, "702", term =>
                OmnichannelContactPhoneContentsAdminListFilterProvider.DisplayTextOrPhoneMatch("702", useExactMatch: false, term, usePhoneNumberIndex));
            var e164Matches = await SearchAsync(store, "+1310", term =>
                OmnichannelContactPhoneContentsAdminListFilterProvider.DisplayTextOrPhoneMatch("+1310", useExactMatch: false, term, usePhoneNumberIndex));

            // Assert
            // "702" is the start of one number and part of a name; the number that only holds it further in is left out.
            Assert.Equal(
                new[] { vegas.ContentItemId, named.ContentItemId }.Order(),
                nationalMatches.Order());
            Assert.Equal([losAngeles.ContentItemId], e164Matches);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    [Theory]
    [InlineData("702")]
    [InlineData("1702")]
    [InlineData("+1702")]
    [InlineData("(702) 555")]
    public async Task PhoneMatch_WithThePhoneNumberIndex_FindsEveryNumberOfAnyTypeButNotTheDisplayText(string search)
    {
        var (connectionString, path) = ContactPhoneTestStore.CreateDatabase("phone-search-box-numbers");
        var store = await ContactPhoneTestStore.CreateAsync(connectionString);

        try
        {
            // Arrange
            var cell = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Cell", "+17025550104"));
            var work = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.LeadContentType, ("Work", "+17025559999"));
            var elsewhere = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Cell", "+13105557020"));
            var named = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType);
            named.DisplayText = "Unit 702";

            await ContactPhoneTestStore.SaveAsync(store, cell, work, elsewhere, named);

            // Act
            var matches = await SearchAsync(store, search, term =>
                OmnichannelContactPhoneContentsAdminListFilterProvider.PhoneMatch(term, usePhoneNumberIndex: true));

            // Assert
            // The work number lives only in the phone number index; the name that holds the digits is not a number.
            Assert.Equal(new[] { cell.ContentItemId, work.ContentItemId }.Order(), matches.Order());
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    [Fact]
    public async Task PhoneMatch_BeforeThePhoneNumberIndexIsBackfilled_ReadsTheContactIndex()
    {
        var (connectionString, path) = ContactPhoneTestStore.CreateDatabase("phone-search-box-legacy");

        // Records saved before the phone number index existed have no rows in it.
        var store = await ContactPhoneTestStore.CreateAsync(connectionString, withPhoneNumberIndex: false);

        try
        {
            // Arrange
            var cell = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Cell", "+17025550104"));
            var home = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Home", "+17025557020"));

            await ContactPhoneTestStore.SaveAsync(store, cell, home);

            // Act
            var fromContactIndex = await SearchAsync(store, "702", term =>
                OmnichannelContactPhoneContentsAdminListFilterProvider.PhoneMatch(term, usePhoneNumberIndex: false));
            var fromPhoneNumberIndex = await SearchAsync(store, "702", term =>
                OmnichannelContactPhoneContentsAdminListFilterProvider.PhoneMatch(term, usePhoneNumberIndex: true));

            // Assert
            Assert.Equal(new[] { cell.ContentItemId, home.ContentItemId }.Order(), fromContactIndex.Order());
            Assert.Empty(fromPhoneNumberIndex);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PhoneMatch_WhenTwoSearchesAreJoinedWithOr_RunsAndFindsEither(bool usePhoneNumberIndex)
    {
        var (connectionString, path) = ContactPhoneTestStore.CreateDatabase("phone-search-box-or");
        var store = await ContactPhoneTestStore.CreateAsync(connectionString, withPhoneNumberIndex: usePhoneNumberIndex);

        try
        {
            // Arrange
            // A search the list parses as typed, such as "(702) 555-9999", joins its words with "or". YesSql writes
            // that "or" with no space after it, so a term that did not open with a parenthesis made the SQL invalid
            // and the list failed with a server error.
            var vegas = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Cell", "+17025550104"));
            var losAngeles = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Cell", "+13105557020"));
            var other = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Cell", "+14155550000"));

            await ContactPhoneTestStore.SaveAsync(store, vegas, losAngeles, other);

            Assert.True(PhoneNumberSearchTerm.TryParse("702", out var first));
            Assert.True(PhoneNumberSearchTerm.TryParse("310", out var second));

            await using var session = store.CreateSession();

            // Act
            var items = await session
                .Query<ContentItem>()
                .With<ContentItemIndex>(index => index.Latest)
                .Any(
                    query => query.With(OmnichannelContactPhoneContentsAdminListFilterProvider.PhoneMatch(first, usePhoneNumberIndex)),
                    query => query.With(OmnichannelContactPhoneContentsAdminListFilterProvider.PhoneMatch(second, usePhoneNumberIndex)))
                .ListAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(
                new[] { vegas.ContentItemId, losAngeles.ContentItemId }.Order(),
                items.Select(item => item.ContentItemId).Order());
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PhoneMatch_NeverComparesTheDisplayText(bool usePhoneNumberIndex)
    {
        // Arrange
        // A display text match is a LIKE '%…%' no index can answer; with it in the search, every search by number read
        // the display text of every contact or lead of the list, and timed out on a large tenant.
        Assert.True(PhoneNumberSearchTerm.TryParse("7025550104", out var searchTerm));

        // Act
        var predicate = OmnichannelContactPhoneContentsAdminListFilterProvider.PhoneMatch(searchTerm, usePhoneNumberIndex);

        // Assert
        Assert.DoesNotContain(nameof(ContentItemIndex.DisplayText), predicate.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("702555", "+17025550104", true)]
    [InlineData("1702555", "+17025550104", true)]
    [InlineData("+1702555", "+17025550104", true)]
    [InlineData("7025550104", "+17025550104", true)]
    [InlineData("5550104", "+17025550104", false)]
    [InlineData("+44207", "+442071234567", true)]
    [InlineData("+1702", "+442071234567", false)]
    public void SearchBoxNumberMatch_MatchesTheE164NumbersThatStartWithTheSearch(string search, string number, bool expected)
    {
        // Arrange
        Assert.True(PhoneNumberSearchTerm.TryParse(search, out var searchTerm));

        var predicate = OmnichannelContactPhonePredicates.SearchBoxNumberMatch(searchTerm).Compile();

        // Act
        var matches = predicate(new OmnichannelContactPhoneNumberIndex { NormalizedPhoneNumber = number });

        // Assert
        Assert.Equal(expected, matches);
    }

    private static async Task<string[]> SearchAsync(
        IStore store,
        string search,
        Func<PhoneNumberSearchTerm, Expression<Func<ContentItemIndex, bool>>> predicate)
    {
        Assert.True(OmnichannelContactPhoneContentsAdminListFilterProvider.TryParsePhoneLikeTerm(search, out var searchTerm));

        await using var session = store.CreateSession();

        var items = await session
            .Query<ContentItem>()
            .With<ContentItemIndex>(index => index.Latest)
            .With(predicate(searchTerm))
            .ListAsync(TestContext.Current.CancellationToken);

        return items.Select(item => item.ContentItemId).ToArray();
    }

    /// <summary>
    /// Evaluates a predicate in memory the way the database would: a string method called on an empty column throws
    /// here, where in SQL the comparison with NULL is simply false.
    /// </summary>
    private static bool EvaluateLikeSql(Func<OmnichannelContactIndex, bool> predicate, OmnichannelContactIndex row)
    {
        try
        {
            return predicate(row);
        }
        catch (NullReferenceException)
        {
            return false;
        }
    }

    private static async Task<Expression<Func<OmnichannelContactIndex, bool>>> CapturePredicateAsync(string filter)
    {
        var builder = new QueryEngineBuilder<ContentItem>();
        new OmnichannelContactPhoneContentsAdminListFilterProvider().Build(builder);

        Expression<Func<OmnichannelContactIndex, bool>> predicate = null;
        var query = new Mock<IQuery<ContentItem>>();
        query
            .Setup(x => x.With<OmnichannelContactIndex>(It.IsAny<Expression<Func<OmnichannelContactIndex, bool>>>()))
            .Callback<Expression<Func<OmnichannelContactIndex, bool>>>(value => predicate = value)
            .Returns(new Mock<IQuery<ContentItem, OmnichannelContactIndex>>().Object);

        await builder.Build().Parse(filter).ExecuteAsync(query.Object);

        Assert.NotNull(predicate);

        return predicate;
    }

    private static OmnichannelContactIndex CreateIndex(
        string nationalNumber,
        string e164Number)
    {
        return new OmnichannelContactIndex
        {
            ContentItemId = "contact-id",
            PrimaryCellPhoneNumber = nationalNumber,
            PrimaryHomePhoneNumber = string.Empty,
            NormalizedPrimaryCellPhoneNumber = e164Number,
            NormalizedPrimaryHomePhoneNumber = string.Empty,
        };
    }
}
