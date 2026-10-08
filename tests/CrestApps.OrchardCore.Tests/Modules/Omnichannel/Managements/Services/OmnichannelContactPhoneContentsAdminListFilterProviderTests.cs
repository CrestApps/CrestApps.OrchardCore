using System.Linq.Expressions;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Moq;
using OrchardCore.ContentManagement;
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
