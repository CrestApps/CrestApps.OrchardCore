using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Handlers;
using Moq;
using YesSql;
using YesSql.Services;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Handlers;

public sealed class BulkManageActivityFilterHandlerTests
{
    [Fact]
    public async Task FilteringAsync_WhenPhoneAndTimeZoneFiltersAreUsed_ConstrainsBothJoinsToLatestContacts()
    {
        // Arrange
        var filter = new BulkManageActivityFilter
        {
            PhoneNumber = "702499",
            TimeZoneIds = ["America/Los_Angeles"],
        };
        var dialect = Mock.Of<ISqlDialect>();
        var sqlBuilder = new SqlBuilder(string.Empty, dialect);
        var context = new BulkManageActivityFilterContext(
            filter,
            sqlBuilder,
            dialect,
            string.Empty,
            Mock.Of<ITableNameConvention>(),
            schema: null,
            activityTableAlias: "a");
        var handler = new BulkManageActivityFilterHandler();

        // Act
        await handler.FilteringAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.True((bool)sqlBuilder.Parameters["@PhoneLatest"]);
        Assert.True((bool)sqlBuilder.Parameters["@TimeZoneLatest"]);
        Assert.Equal("%702499%", sqlBuilder.Parameters["@PhonePattern"]);
    }
}
