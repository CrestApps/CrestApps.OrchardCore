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
