using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Reports;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Reports;

public sealed class OmnichannelReportAggregatorTests
{
    private static readonly DateTime _day = new(2026, 2, 10, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void BuildActivitySummary_ComputesCountsAndBreakdowns()
    {
        // Arrange
        var activities = new[]
        {
            Activity(ActivityStatus.Completed, ActivitySources.Inbound, OmnichannelConstants.Channels.Phone),
            Activity(ActivityStatus.NotStated, ActivitySources.Dialer, OmnichannelConstants.Channels.Phone),
            Activity(ActivityStatus.InProgress, ActivitySources.Manual, OmnichannelConstants.Channels.Sms),
            Activity(ActivityStatus.Failed, ActivitySources.Manual, OmnichannelConstants.Channels.Email),
        };

        // Act
        var data = OmnichannelReportAggregator.BuildActivitySummary(activities);

        // Assert
        Assert.Equal(4, data.Counts.Total);
        Assert.Equal(1, data.Counts.Completed);
        Assert.Equal(1, data.Counts.Pending);
        Assert.Equal(1, data.Counts.InProgress);
        Assert.Equal(1, data.Counts.Failed);
        Assert.Equal(2, data.BySource[ActivitySources.Manual]);
        Assert.Equal(2, data.ByChannel[OmnichannelConstants.Channels.Phone]);
        Assert.Single(data.Daily);
        Assert.Equal(4, data.Daily[0].Count);
    }

    [Fact]
    public void BuildCampaignPerformance_GroupsByCampaignWithTotals()
    {
        // Arrange
        var activities = new[]
        {
            Campaign("camp-1", ActivityStatus.Completed),
            Campaign("camp-1", ActivityStatus.NotStated),
            Campaign("camp-2", ActivityStatus.Completed),
        };

        // Act
        var data = OmnichannelReportAggregator.BuildCampaignPerformance(activities);

        // Assert
        Assert.Equal(2, data.Rows.Count);

        var top = data.Rows[0];

        Assert.Equal("camp-1", top.CampaignId);
        Assert.Equal(2, top.Counts.Total);
        Assert.Equal(1, top.Counts.Completed);
        Assert.Equal(1, top.Counts.Pending);
        Assert.Equal(3, data.Totals.Total);
        Assert.Equal(2, data.Totals.Completed);
    }

    [Fact]
    public void BuildCampaignGroupPerformance_AggregatesCampaignsInSameGroup()
    {
        // Arrange
        var activities = new[]
        {
            Campaign("camp-1", ActivityStatus.Completed),
            Campaign("camp-2", ActivityStatus.NotStated),
            Campaign("camp-3", ActivityStatus.Failed),
        };
        var campaignGroupIds = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["camp-1"] = "group-1",
            ["camp-2"] = "group-1",
            ["camp-3"] = "group-2",
        };

        // Act
        var data = OmnichannelReportAggregator.BuildCampaignGroupPerformance(activities, campaignGroupIds);

        // Assert
        Assert.Equal(2, data.Rows.Count);
        var group = data.Rows.Single(row => row.CampaignGroupId == "group-1");
        Assert.Equal(2, group.Counts.Total);
        Assert.Equal(1, group.Counts.Completed);
        Assert.Equal(1, group.Counts.Pending);
    }

    [Fact]
    public void CountByDisposition_CountsCompletedByDisposition()
    {
        // Arrange
        var completed = new[]
        {
            Disposition("won"),
            Disposition("won"),
            Disposition("lost"),
            Disposition(null),
        };

        // Act
        var counts = OmnichannelReportAggregator.CountByDisposition(completed);

        // Assert
        Assert.Equal(2, counts["won"]);
        Assert.Equal(1, counts["lost"]);
        Assert.Equal(1, counts[string.Empty]);
    }

    [Fact]
    public void Filter_AppliesCampaignChannelSourceAndStatus()
    {
        // Arrange
        var matching = Activity(ActivityStatus.Completed, ActivitySources.Inbound, OmnichannelConstants.Channels.Phone);
        matching.CampaignId = "campaign-1";

        var activities = new[]
        {
            matching,
            Activity(ActivityStatus.Completed, ActivitySources.Inbound, OmnichannelConstants.Channels.Sms),
            Activity(ActivityStatus.Failed, ActivitySources.Inbound, OmnichannelConstants.Channels.Phone),
            Activity(ActivityStatus.Completed, ActivitySources.Manual, OmnichannelConstants.Channels.Phone),
        };

        var criteria = new OmnichannelReportCriteria
        {
            CampaignId = "campaign-1",
            CampaignIds = new HashSet<string>(["campaign-1"], StringComparer.Ordinal),
            Channel = OmnichannelConstants.Channels.Phone,
            Source = ActivitySources.Inbound,
            Status = ActivityStatus.Completed,
        };

        // Act
        var filtered = OmnichannelReportQuery.Filter(activities, criteria);

        // Assert
        Assert.Same(matching, Assert.Single(filtered));
    }

    private static OmnichannelActivityIndex Activity(ActivityStatus status, string source, string channel)
    {
        return new OmnichannelActivityIndex
        {
            Status = status,
            Source = source,
            Channel = channel,
            CreatedUtc = _day,
        };
    }

    private static OmnichannelActivityIndex Campaign(string campaignId, ActivityStatus status)
    {
        return new OmnichannelActivityIndex
        {
            CampaignId = campaignId,
            Status = status,
            CreatedUtc = _day,
        };
    }

    private static OmnichannelActivityIndex Disposition(string dispositionId)
    {
        return new OmnichannelActivityIndex
        {
            Status = ActivityStatus.Completed,
            DispositionId = dispositionId,
            CompletedUtc = _day,
            CreatedUtc = _day,
        };
    }
}
