using System.Linq.Expressions;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Handlers;
using Moq;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Handlers;

public sealed class TimeZoneListOmnichannelActivityFilterHandlerTests
{
    [Fact]
    public async Task FilteringAsync_WhenTimeZoneIsSpecified_QueriesPublishedContacts()
    {
        // Arrange
        Expression<Func<OmnichannelContactIndex, bool>> predicate = null;
        var indexQuery = new Mock<IQueryIndex<OmnichannelContactIndex>>();
        indexQuery
            .Setup(query => query.Where(It.IsAny<Expression<Func<OmnichannelContactIndex, bool>>>()))
            .Callback<Expression<Func<OmnichannelContactIndex, bool>>>(value => predicate = value)
            .Returns(indexQuery.Object);
        indexQuery
            .Setup(query => query.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var rootQuery = new Mock<IQuery>();
        rootQuery
            .Setup(query => query.ForIndex<OmnichannelContactIndex>())
            .Returns(indexQuery.Object);

        var session = new Mock<ISession>();
        session
            .Setup(currentSession => currentSession.Query(null))
            .Returns(rootQuery.Object);

        var activityQuery = new Mock<IQuery<OmnichannelActivity, OmnichannelActivityIndex>>();
        activityQuery
            .Setup(query => query.Where(It.IsAny<Expression<Func<OmnichannelActivityIndex, bool>>>()))
            .Returns(activityQuery.Object);

        var context = new ListOmnichannelActivityFilterContext(
            new ListOmnichannelActivityFilter
            {
                TimeZoneId = "America/Los_Angeles",
            },
            activityQuery.Object);
        var handler = new TimeZoneListOmnichannelActivityFilterHandler(session.Object);

        // Act
        await handler.FilteringAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(predicate);
        var conjunction = Assert.IsAssignableFrom<BinaryExpression>(predicate.Body);
        Assert.Equal(ExpressionType.AndAlso, conjunction.NodeType);

        var publishedMember = Assert.IsAssignableFrom<MemberExpression>(conjunction.Left);
        Assert.Equal(nameof(OmnichannelContactIndex.Published), publishedMember.Member.Name);
    }
}
