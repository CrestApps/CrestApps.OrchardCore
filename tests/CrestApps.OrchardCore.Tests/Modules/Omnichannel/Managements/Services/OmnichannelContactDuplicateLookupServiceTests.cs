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

    [Theory]
    [InlineData(1, 1)]
    [InlineData(OmnichannelContactDuplicateLookupService.LookupBatchSize, 1)]
    [InlineData(OmnichannelContactDuplicateLookupService.LookupBatchSize + 1, 2)]
    [InlineData(5_000, 100)]
    public async Task GetPhoneNumberOwnersAsync_LooksUpEachColumnSeparatelyInFixedSizeBatches(int keyCount, int expectedBatches)
    {
        // Arrange
        // An OR across the number columns made SQL Server scan the whole contact index for every import batch. Each
        // column is compared in a query of its own, so it seeks that column's index.
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

        var service = new OmnichannelContactDuplicateLookupService(session.Object, new DefaultPhoneNumberService());
        var keys = Enumerable.Range(0, keyCount).Select(i => $"+1702{i:D7}").ToArray();

        // Act
        await service.GetPhoneNumberOwnersAsync(keys, include: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expectedBatches * 4, predicates.Count);
        Assert.All(predicates, predicate => Assert.DoesNotContain("OrElse", predicate.Body.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public void ToLookupBatches_PadsTheLastBatchWithItsLastValue()
    {
        // Arrange
        // Every lookup must send the same number of parameters, or each list length compiles and caches a plan of its
        // own. Padding with a value already in the batch matches nothing new.
        var values = Enumerable.Range(0, OmnichannelContactDuplicateLookupService.LookupBatchSize + 2)
            .Select(i => $"+1702{i:D7}")
            .ToArray();

        // Act
        var batches = OmnichannelContactDuplicateLookupService.ToLookupBatches(values).ToArray();

        // Assert
        Assert.Equal(2, batches.Length);
        Assert.All(batches, batch => Assert.Equal(OmnichannelContactDuplicateLookupService.LookupBatchSize, batch.Length));
        Assert.Equal(values.Take(OmnichannelContactDuplicateLookupService.LookupBatchSize), batches[0]);
        Assert.Equal(values[^2], batches[1][0]);
        Assert.All(batches[1].Skip(1), value => Assert.Equal(values[^1], value));
        Assert.Equal(values.ToHashSet(), batches.SelectMany(batch => batch).ToHashSet());
    }

    [Fact]
    public void ToLookupBatches_WhenThereAreNoValues_YieldsNoBatch()
    {
        // Act & Assert
        Assert.Empty(OmnichannelContactDuplicateLookupService.ToLookupBatches([]));
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
