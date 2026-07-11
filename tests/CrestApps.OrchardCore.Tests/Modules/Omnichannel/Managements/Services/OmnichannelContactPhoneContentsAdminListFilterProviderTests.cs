using System.Linq.Expressions;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
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

        Expression<Func<OmnichannelContactPhoneIndex, bool>> predicate = null;
        var indexedQuery = new Mock<IQuery<ContentItem, OmnichannelContactPhoneIndex>>();
        var query = new Mock<IQuery<ContentItem>>();
        query
            .Setup(x => x.With<OmnichannelContactPhoneIndex>(It.IsAny<Expression<Func<OmnichannelContactPhoneIndex, bool>>>()))
            .Callback<Expression<Func<OmnichannelContactPhoneIndex, bool>>>(value => predicate = value)
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

    private static OmnichannelContactPhoneIndex CreateIndex(
        string nationalNumber,
        string e164Number)
    {
        return new OmnichannelContactPhoneIndex
        {
            ContentItemId = "contact-id",
            NationalPrimaryCellPhoneNumber = nationalNumber,
            NationalPrimaryHomePhoneNumber = string.Empty,
            E164PrimaryCellPhoneNumber = e164Number,
            E164PrimaryHomePhoneNumber = string.Empty,
        };
    }
}
