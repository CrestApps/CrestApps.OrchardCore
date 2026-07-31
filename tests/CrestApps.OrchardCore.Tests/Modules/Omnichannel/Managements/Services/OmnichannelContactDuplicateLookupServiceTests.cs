using System.Linq.Expressions;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using Moq;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

public sealed class OmnichannelContactDuplicateLookupServiceTests
{
    [Fact]
    public async Task GetExistingNormalizedPhoneNumbersAsync_WhenQueryingIndexes_RequiresPublishedVersions()
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

        var service = new OmnichannelContactDuplicateLookupService(
            session.Object,
            new DefaultPhoneNumberService());

        // Act
        await service.GetExistingNormalizedPhoneNumbersAsync(
            ["+17024993350"],
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

    [Fact]
    public void AddLegacyMatches_ShouldMatchInputAgainstLegacyPhoneValues()
    {
        // Arrange
        var existingPhoneNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Act
        OmnichannelContactDuplicateLookupService.AddLegacyMatches(
            existingPhoneNumbers,
            [
            new OmnichannelContactIndex
            {
                PrimaryCellPhoneNumber = "+15551112222",
            },
            ],
            ["+15551112222"],
            static index => index.PrimaryCellPhoneNumber);

        // Assert
        Assert.Single(existingPhoneNumbers);
        Assert.Contains("+15551112222", existingPhoneNumbers);
    }

    [Fact]
    public void NormalizePhoneNumber_WhenValidE164_ShouldReturnE164()
    {
        // Arrange
        var phoneNumberService = new DefaultPhoneNumberService();
        var session = Mock.Of<ISession>();
        var service = new OmnichannelContactDuplicateLookupService(session, phoneNumberService);

        // Act & Assert
        Assert.Equal("+17024993350", service.NormalizePhoneNumber("+17024993350"));
        Assert.Equal("+17024993350", service.NormalizePhoneNumber("+1 (702) 499-3350"));

        // Numbers without + fallback to digits-only normalization.
        Assert.Equal("7024993350", service.NormalizePhoneNumber("702-499-3350"));
        Assert.Equal("7024993350", service.NormalizePhoneNumber("(702) 499-3350"));
    }

    [Fact]
    public void NormalizePhoneNumber_WhenEmptyOrNull_ShouldReturnEmpty()
    {
        // Arrange
        var phoneNumberService = new DefaultPhoneNumberService();
        var session = Mock.Of<ISession>();
        var service = new OmnichannelContactDuplicateLookupService(session, phoneNumberService);

        // Act & Assert
        Assert.Equal(string.Empty, service.NormalizePhoneNumber(""));
        Assert.Equal(string.Empty, service.NormalizePhoneNumber(null));
    }
}
