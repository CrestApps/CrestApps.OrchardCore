using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Commerce;
using CrestApps.OrchardCore.Learning.Commerce.Reports;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Authorization;
using Moq;
using OrchardCore.DataSources;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Learning.Commerce;

/// <summary>
/// The Learning commerce data sets name what their identifiers point at, so a report joins the course, the account,
/// the pool and the person to show their names instead of raw ids.
/// </summary>
public sealed class LearningCommerceDataSetReferenceTests
{
    [Fact]
    public void ThePoolAndSeatIdentifiers_ReferenceWhatTheyIdentify()
    {
        // Arrange
        IRecordDataSet pools = new LicensePoolReportDataSet(Mock.Of<ISession>(), Mock.Of<IAuthorizationService>(), new PassThroughStringLocalizer<LicensePoolReportDataSet>());
        IRecordDataSet seats = new LicenseSeatReportDataSet(Mock.Of<ISession>(), Mock.Of<IAuthorizationService>(), new PassThroughStringLocalizer<LicenseSeatReportDataSet>());

        // Act
        DataFieldReference Reference(IRecordDataSet dataSet, string field)
            => Assert.Single(dataSet.Fields.Single(candidate => candidate.Name == field).References ?? []);

        // Assert
        Assert.Equal(LearningConstants.ContentTypes.Course, Reference(pools, "CourseId").DataSet);
        Assert.Equal("Account", Reference(pools, "AccountId").DataSet);
        Assert.Equal("Account", Reference(seats, "AccountId").DataSet);
        Assert.Equal(LicensePoolReportDataSet.DataSetName, Reference(seats, "PoolId").DataSet);
        Assert.Equal(LearningCommerceConstants.DataSourceName, Reference(seats, "PoolId").Source);
        Assert.NotNull(Reference(seats, "UserId"));
    }
}
