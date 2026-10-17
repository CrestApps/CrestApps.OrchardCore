using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Reports;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Reports;

/// <summary>
/// The disposition filter lets several dispositions be picked together, and the page filter picks a page of a report
/// that pages its rows. These pin how both are read from the report filter and how the shared in-memory filter, which
/// every other Omnichannel report goes through, applies the dispositions.
/// </summary>
public sealed class OmnichannelReportFilterTests
{
    [Fact]
    public async Task GetCriteriaAsync_ReadsEveryDispositionPicked_AndDropsBlankOnes()
    {
        // Arrange
        var filter = new ReportFilter();
        filter.Set<string[]>(OmnichannelReportFilter.DispositionIds, ["no-answer", " busy ", "", null]);

        // Act
        var criteria = await GetCriteriaAsync(filter);

        // Assert
        Assert.Equal(["busy", "no-answer"], criteria.DispositionIds.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task GetCriteriaAsync_WithNoDispositionPicked_KeepsEveryDisposition()
    {
        // Arrange
        var absent = new ReportFilter();
        var empty = new ReportFilter();
        empty.Set<string[]>(OmnichannelReportFilter.DispositionIds, ["", " "]);

        // Act
        var absentCriteria = await GetCriteriaAsync(absent);
        var emptyCriteria = await GetCriteriaAsync(empty);

        // Assert
        Assert.Null(absentCriteria.DispositionIds);
        Assert.Null(emptyCriteria.DispositionIds);
    }

    [Theory]
    [InlineData(4, 4)]
    [InlineData(1, 1)]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    public async Task GetCriteriaAsync_ReadsThePage_AndNeverGoesBelowTheFirst(int stored, int expected)
    {
        // Arrange
        var filter = new ReportFilter();
        filter.Set(OmnichannelReportFilter.Page, stored);

        // Act
        var criteria = await GetCriteriaAsync(filter);

        // Assert
        Assert.Equal(expected, criteria.Page);
    }

    [Fact]
    public async Task GetCriteriaAsync_WithNoPage_StartsOnTheFirst()
    {
        // Act
        var criteria = await GetCriteriaAsync(new ReportFilter());

        // Assert
        Assert.Equal(1, criteria.Page);
    }

    [Fact]
    public void Filter_WithDispositionsPicked_KeepsOnlyActivitiesThatEndedWithOneOfThem()
    {
        // Arrange
        var activities = new[]
        {
            new OmnichannelActivityIndex { ItemId = "a", DispositionId = "no-answer" },
            new OmnichannelActivityIndex { ItemId = "b", DispositionId = "busy" },
            new OmnichannelActivityIndex { ItemId = "c", DispositionId = "sold" },
            new OmnichannelActivityIndex { ItemId = "d", DispositionId = null },
        };
        var criteria = new OmnichannelReportCriteria
        {
            DispositionIds = new HashSet<string>(["no-answer", "busy"], StringComparer.Ordinal),
        };

        // Act
        var filtered = OmnichannelReportQuery.Filter(activities, criteria);

        // Assert
        Assert.Equal(["a", "b"], filtered.Select(activity => activity.ItemId));
    }

    [Fact]
    public void Filter_WithNoDispositionPicked_KeepsActivitiesWithAndWithoutADisposition()
    {
        // Arrange
        var activities = new[]
        {
            new OmnichannelActivityIndex { ItemId = "a", DispositionId = "no-answer" },
            new OmnichannelActivityIndex { ItemId = "b", DispositionId = null },
        };

        // Act
        var filtered = OmnichannelReportQuery.Filter(activities, new OmnichannelReportCriteria());

        // Assert
        Assert.Equal(["a", "b"], filtered.Select(activity => activity.ItemId));
    }

    private static Task<OmnichannelReportCriteria> GetCriteriaAsync(ReportFilter filter)
    {
        return OmnichannelReportFilter.GetCriteriaAsync(
            filter,
            Mock.Of<ICatalogManager<OmnichannelCampaign>>(),
            new ActivitySourceOptions(),
            TestContext.Current.CancellationToken);
    }
}
