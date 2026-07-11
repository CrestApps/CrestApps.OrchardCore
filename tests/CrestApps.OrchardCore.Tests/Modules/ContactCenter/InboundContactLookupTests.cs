using System.Linq.Expressions;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using Moq;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class InboundContactLookupTests
{
    [Fact]
    public async Task FindContactItemIdsAsync_WhenQueryingPhoneIndexes_RequiresPublishedVersions()
    {
        // Arrange
        var predicates = new List<Expression<Func<OmnichannelContactIndex, bool>>>();
        var indexQuery = new Mock<IQueryIndex<OmnichannelContactIndex>>();
        indexQuery
            .Setup(query => query.Where(It.IsAny<Expression<Func<OmnichannelContactIndex, bool>>>()))
            .Callback<Expression<Func<OmnichannelContactIndex, bool>>>(predicate => predicates.Add(predicate))
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

        var lookup = new InboundContactLookup(session.Object, new DefaultPhoneNumberService());

        // Act
        await lookup.FindContactItemIdsAsync(
            "+17024993350",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, predicates.Count);
        Assert.All(predicates, predicate =>
        {
            var conjunction = Assert.IsAssignableFrom<BinaryExpression>(predicate.Body);
            Assert.Equal(ExpressionType.AndAlso, conjunction.NodeType);

            var publishedMember = Assert.IsAssignableFrom<MemberExpression>(conjunction.Left);
            Assert.Equal(nameof(OmnichannelContactIndex.Published), publishedMember.Member.Name);
        });
    }
}
