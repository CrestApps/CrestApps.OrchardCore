using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterReportingServiceTests
{
    private static readonly DateTime _from = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _to = new(2026, 1, 31, 23, 59, 59, DateTimeKind.Utc);
    private static readonly DateTime _day = new(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void BuildCallInsights_ComputesVolumeOutcomesAndHandleTime()
    {
        // Arrange
        var interactions = new[]
        {
            Interaction(InteractionDirection.Inbound, InteractionStatus.Ended, answeredAfter: 10, endedAfter: 70),
            Interaction(InteractionDirection.Inbound, InteractionStatus.Ended, answeredAfter: null, endedAfter: null),
            Interaction(InteractionDirection.Outbound, InteractionStatus.Ended, answeredAfter: 5, endedAfter: 35),
            Interaction(InteractionDirection.Outbound, InteractionStatus.Failed, answeredAfter: null, endedAfter: null),
        };

        // Act
        var report = ContactCenterReportingService.BuildCallInsights(_from, _to, interactions);

        // Assert
        Assert.Equal(4, report.Total);
        Assert.Equal(2, report.Inbound);
        Assert.Equal(2, report.Outbound);
        Assert.Equal(2, report.Answered);
        Assert.Equal(1, report.Abandoned);
        Assert.Equal(1, report.Failed);
        Assert.Equal(45d, report.AverageHandleTimeSeconds);
        Assert.Equal(7.5d, report.AverageSpeedOfAnswerSeconds);
        Assert.Equal(90d, report.TotalTalkTimeSeconds);
    }

    [Fact]
    public void BuildCallInsights_GroupsDailyVolume()
    {
        // Arrange
        var interactions = new[]
        {
            Interaction(InteractionDirection.Inbound, InteractionStatus.Ended, answeredAfter: 3, endedAfter: 30, createdUtc: _day),
            Interaction(InteractionDirection.Inbound, InteractionStatus.Ended, answeredAfter: null, endedAfter: null, createdUtc: _day),
            Interaction(InteractionDirection.Outbound, InteractionStatus.Ended, answeredAfter: 3, endedAfter: 30, createdUtc: _day.AddDays(1)),
        };

        // Act
        var report = ContactCenterReportingService.BuildCallInsights(_from, _to, interactions);

        // Assert
        Assert.Equal(2, report.Daily.Count);

        var first = report.Daily[0];

        Assert.Equal(DateOnly.FromDateTime(_day), first.Date);
        Assert.Equal(2, first.Total);
        Assert.Equal(1, first.Answered);
        Assert.Equal(1, first.Abandoned);
    }

    [Fact]
    public void EnterpriseMetrics_KeepInboundAccessibilitySeparateFromOutboundAttempts()
    {
        // Arrange
        var inboundAnswered = Interaction(InteractionDirection.Inbound, InteractionStatus.Ended, answeredAfter: 10, endedAfter: 70);
        inboundAnswered.RecordingReference = "recording-1";
        inboundAnswered.TransferHistory.Add(new InteractionTransferHistoryEntry());
        inboundAnswered.WrapUpStartedUtc = inboundAnswered.EndedUtc;
        inboundAnswered.WrapUpCompletedUtc = inboundAnswered.EndedUtc.Value.AddSeconds(30);

        var interactions = new[]
        {
            inboundAnswered,
            Interaction(InteractionDirection.Inbound, InteractionStatus.Ended, answeredAfter: null, endedAfter: null),
            Interaction(InteractionDirection.Inbound, InteractionStatus.Failed, answeredAfter: null, endedAfter: null),
            Interaction(InteractionDirection.Outbound, InteractionStatus.Ended, answeredAfter: 5, endedAfter: 35),
        };

        // Act
        var metrics = EnterpriseInteractionReportProvider.Aggregate(interactions);

        // Assert
        Assert.Equal(4, metrics.Total);
        Assert.Equal(3, metrics.InboundOffered);
        Assert.Equal(2, metrics.Answered);
        Assert.Equal(1, metrics.InboundAnswered);
        Assert.Equal(1, metrics.Abandoned);
        Assert.Equal(1, metrics.Failed);
        Assert.Equal(0.5d, metrics.AnswerRate);
        Assert.Equal(1d / 3d, metrics.InboundAnswerRate);
        Assert.Equal(1d / 3d, metrics.AbandonmentRate);
        Assert.Equal(10d, metrics.AverageSpeedOfAnswerSeconds);
        Assert.Equal(60d, metrics.AverageHandleTimeSeconds);
        Assert.Equal(0.5d, metrics.TransferRate);
        Assert.Equal(0.5d, metrics.RecordingCoverage);
    }

    [Fact]
    public void QueueServiceLevel_UsesInboundAnsweredAndAbandonedEligibility()
    {
        // Arrange
        var interactions = new[]
        {
            Interaction(InteractionDirection.Inbound, InteractionStatus.Ended, answeredAfter: 10, endedAfter: 70),
            Interaction(InteractionDirection.Inbound, InteractionStatus.Ended, answeredAfter: 30, endedAfter: 90),
            Interaction(InteractionDirection.Inbound, InteractionStatus.Ended, answeredAfter: null, endedAfter: null),
            Interaction(InteractionDirection.Inbound, InteractionStatus.Failed, answeredAfter: null, endedAfter: null),
            Interaction(InteractionDirection.Outbound, InteractionStatus.Ended, answeredAfter: 5, endedAfter: 35),
        };

        // Act
        var metrics = EnterpriseInteractionReportProvider.CalculateQueueServiceLevel(interactions, thresholdSeconds: 20);

        // Assert
        Assert.True(metrics.HasServiceLevel);
        Assert.Equal(3, metrics.EligibleOffered);
        Assert.Equal(2, metrics.Answered);
        Assert.Equal(1, metrics.AnsweredWithinThreshold);
        Assert.Equal(1d / 3d, metrics.ServiceLevel);
        Assert.Equal(20d, metrics.AverageSpeedOfAnswerSeconds);
    }

    [Fact]
    public void FilterInteractions_AppliesQueueAgentChannelAndDirection()
    {
        // Arrange
        var matching = AgentInteraction("agent-1", InteractionDirection.Inbound, answeredAfter: 5, endedAfter: 65);
        matching.QueueId = "queue-1";
        matching.Channel = InteractionChannel.Voice;

        var otherQueue = AgentInteraction("agent-1", InteractionDirection.Inbound, answeredAfter: 5, endedAfter: 65);
        otherQueue.QueueId = "queue-2";

        var interactions = new[]
        {
            matching,
            otherQueue,
            AgentInteraction("agent-2", InteractionDirection.Inbound, answeredAfter: 5, endedAfter: 65),
            AgentInteraction("agent-1", InteractionDirection.Outbound, answeredAfter: 5, endedAfter: 65),
        };

        var criteria = new ContactCenterReportCriteria
        {
            QueueId = "queue-1",
            AgentId = "agent-1",
            Channel = InteractionChannel.Voice,
            Direction = InteractionDirection.Inbound,
        };

        // Act
        var filtered = ContactCenterReportingService.FilterInteractions(interactions, criteria);

        // Assert
        Assert.Same(matching, Assert.Single(filtered));
    }

    [Fact]
    public void FilterActivities_AppliesCampaignSourceChannelAndStatus()
    {
        // Arrange
        var matching = ActivityIndex("campaign-1", ActivityStatus.Completed);
        matching.Source = ActivitySources.Inbound;
        matching.Channel = OmnichannelConstants.Channels.Phone;

        var activities = new[]
        {
            matching,
            ActivityIndex("campaign-2", ActivityStatus.Completed),
            ActivityIndex("campaign-1", ActivityStatus.Failed),
        };

        var criteria = new ContactCenterReportCriteria
        {
            CampaignId = "campaign-1",
            ActivitySource = ActivitySources.Inbound,
            Channel = InteractionChannel.Voice,
            ActivityStatus = ActivityStatus.Completed,
        };

        // Act
        var filtered = ContactCenterReportingService.FilterActivities(activities, criteria);

        // Assert
        Assert.Same(matching, Assert.Single(filtered));
    }

    [Fact]
    public void BuildAgentProductivity_AggregatesHandledAndCompleted()
    {
        // Arrange
        var interactions = new[]
        {
            AgentInteraction("agent-1", InteractionDirection.Inbound, answeredAfter: 5, endedAfter: 65),
            AgentInteraction("agent-1", InteractionDirection.Outbound, answeredAfter: 5, endedAfter: 35),
            AgentInteraction("agent-2", InteractionDirection.Inbound, answeredAfter: null, endedAfter: null),
        };

        var completedByUser = new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["user-1"] = 4,
        };

        var agents = new[]
        {
            new AgentProfile { ItemId = "agent-1", UserId = "user-1", DisplayName = "Agent One" },
            new AgentProfile { ItemId = "agent-2", UserId = "user-2", DisplayName = "Agent Two" },
        };

        // Act
        var report = ContactCenterReportingService.BuildAgentProductivity(_from, _to, interactions, completedByUser, agents);

        // Assert
        var top = report.Rows[0];

        Assert.Equal("Agent One", top.DisplayName);
        Assert.Equal(2, top.InteractionsHandled);
        Assert.Equal(1, top.InboundHandled);
        Assert.Equal(1, top.OutboundHandled);
        Assert.Equal(90d, top.TotalTalkTimeSeconds);
        Assert.Equal(45d, top.AverageHandleTimeSeconds);
        Assert.Equal(4, top.ActivitiesCompleted);

        // The second agent never answered an interaction and completed no activity, so is excluded.
        Assert.Single(report.Rows);
    }

    [Fact]
    public void BuildAgentProductivity_IncludesWrapUpInHandleTime()
    {
        // Arrange
        var interaction = AgentInteraction("agent-1", InteractionDirection.Inbound, answeredAfter: 5, endedAfter: 65);
        interaction.WrapUpStartedUtc = interaction.EndedUtc;
        interaction.WrapUpCompletedUtc = interaction.EndedUtc.Value.AddSeconds(30);

        var agents = new[]
        {
            new AgentProfile { ItemId = "agent-1", UserId = "user-1", DisplayName = "Agent One" },
        };

        // Act
        var report = ContactCenterReportingService.BuildAgentProductivity(
            _from,
            _to,
            [interaction],
            new Dictionary<string, long>(),
            agents);

        // Assert
        var row = Assert.Single(report.Rows);

        Assert.Equal(60d, row.TotalTalkTimeSeconds);
        Assert.Equal(30d, row.TotalWrapUpTimeSeconds);
        Assert.Equal(30d, row.AverageWrapUpTimeSeconds);
        Assert.Equal(90d, row.AverageHandleTimeSeconds);
    }

    [Fact]
    public void BuildQueueUsage_AggregatesPerQueueAndIncludesWaiting()
    {
        // Arrange
        var interactions = new[]
        {
            QueueInteraction("queue-1", InteractionDirection.Inbound, InteractionStatus.Ended, answeredAfter: 4, endedAfter: 64),
            QueueInteraction("queue-1", InteractionDirection.Inbound, InteractionStatus.Ended, answeredAfter: null, endedAfter: null),
        };

        var queues = new[]
        {
            new ActivityQueue { ItemId = "queue-1", Name = "Support", SlaThresholdSeconds = 120 },
            new ActivityQueue { ItemId = "queue-2", Name = "Sales", SlaThresholdSeconds = 60 },
        };

        var waiting = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["queue-1"] = 0,
            ["queue-2"] = 3,
        };

        // Act
        var report = ContactCenterReportingService.BuildQueueUsage(_from, _to, interactions, queues, waiting);

        // Assert
        Assert.Equal(2, report.Rows.Count);

        var support = report.Rows.Single(row => row.QueueId == "queue-1");

        Assert.Equal("Support", support.QueueName);
        Assert.Equal(2, support.InteractionsHandled);
        Assert.Equal(1, support.Answered);
        Assert.Equal(1, support.Abandoned);
        Assert.Equal(60d, support.AverageHandleTimeSeconds);

        var sales = report.Rows.Single(row => row.QueueId == "queue-2");

        Assert.Equal(0, sales.InteractionsHandled);
        Assert.Equal(3, sales.CurrentWaiting);
    }

    [Fact]
    public void BuildCampaignSummary_BucketsCompletedVersusPending()
    {
        // Arrange
        var activities = new[]
        {
            ActivityIndex("camp-1", ActivityStatus.Completed),
            ActivityIndex("camp-1", ActivityStatus.NotStated),
            ActivityIndex("camp-1", ActivityStatus.InProgress),
            ActivityIndex("camp-1", ActivityStatus.Failed),
            ActivityIndex("camp-1", ActivityStatus.Cancelled),
        };

        var names = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["camp-1"] = "Winback",
        };

        // Act
        var report = ContactCenterReportingService.BuildCampaignSummary(_from, _to, activities, names);

        // Assert
        var row = Assert.Single(report.Rows);

        Assert.Equal("Winback", row.CampaignName);
        Assert.Equal(5, row.Counts.Total);
        Assert.Equal(1, row.Counts.Completed);
        Assert.Equal(1, row.Counts.Pending);
        Assert.Equal(1, row.Counts.InProgress);
        Assert.Equal(1, row.Counts.Failed);
        Assert.Equal(1, row.Counts.Cancelled);
        Assert.Equal(0.2d, row.Counts.CompletionRate);
        Assert.Equal(5, report.Totals.Total);
    }

    [Fact]
    public void BuildCampaignSummary_UnknownCampaign_DoesNotExposeIdentifierAsName()
    {
        // Arrange
        var activities = new[]
        {
            ActivityIndex("deleted-campaign-id", ActivityStatus.Completed),
        };

        // Act
        var report = ContactCenterReportingService.BuildCampaignSummary(
            _from,
            _to,
            activities,
            new Dictionary<string, string>(StringComparer.Ordinal));

        // Assert
        var row = Assert.Single(report.Rows);

        Assert.Equal("deleted-campaign-id", row.CampaignId);
        Assert.Null(row.CampaignName);
    }

    [Fact]
    public void AutomatedOutboundCallBatch_PopulatesCallInsightsAndCampaignSummary()
    {
        // Arrange
        var interactions = new[]
        {
            Interaction(InteractionDirection.Outbound, InteractionStatus.Ended, answeredAfter: 4, endedAfter: 64),
            Interaction(InteractionDirection.Outbound, InteractionStatus.Ended, answeredAfter: 8, endedAfter: 98),
            Interaction(InteractionDirection.Outbound, InteractionStatus.Ended, answeredAfter: 6, endedAfter: 36),
            Interaction(InteractionDirection.Outbound, InteractionStatus.Failed, answeredAfter: null, endedAfter: null),
            Interaction(InteractionDirection.Outbound, InteractionStatus.Ringing, answeredAfter: null, endedAfter: null),
        };
        var activities = new[]
        {
            AutomatedCallActivity("campaign-ai-reminders", ActivityStatus.Completed),
            AutomatedCallActivity("campaign-ai-reminders", ActivityStatus.Completed),
            AutomatedCallActivity("campaign-ai-reminders", ActivityStatus.Completed),
            AutomatedCallActivity("campaign-ai-reminders", ActivityStatus.Failed),
            AutomatedCallActivity("campaign-ai-reminders", ActivityStatus.Dialing),
        };
        var campaignNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["campaign-ai-reminders"] = "AI appointment reminders",
        };

        // Act
        var calls = ContactCenterReportingService.BuildCallInsights(_from, _to, interactions);
        var campaigns = ContactCenterReportingService.BuildCampaignSummary(_from, _to, activities, campaignNames);

        // Assert
        Assert.Equal(5, calls.Total);
        Assert.Equal(5, calls.Outbound);
        Assert.Equal(3, calls.Answered);
        Assert.Equal(1, calls.Failed);
        Assert.Equal(180d, calls.TotalTalkTimeSeconds);
        Assert.Equal(60d, calls.AverageHandleTimeSeconds);
        Assert.Equal(6d, calls.AverageSpeedOfAnswerSeconds);

        var campaign = Assert.Single(campaigns.Rows);

        Assert.Equal("AI appointment reminders", campaign.CampaignName);
        Assert.Equal(5, campaign.Counts.Total);
        Assert.Equal(3, campaign.Counts.Completed);
        Assert.Equal(1, campaign.Counts.InProgress);
        Assert.Equal(1, campaign.Counts.Failed);
        Assert.Equal(0.6d, campaign.Counts.CompletionRate);
        Assert.All(activities, activity =>
        {
            Assert.Equal(ActivityInteractionType.Automated, activity.InteractionType);
            Assert.Equal(ActivitySources.PowerDial, activity.Source);
            Assert.Equal(OmnichannelConstants.Channels.Phone, activity.Channel);
            Assert.Equal(1, activity.Attempts);
        });
    }

    [Fact]
    public void BuildSubjectInventory_GroupsBySubjectType()
    {
        // Arrange
        var activities = new[]
        {
            SubjectActivity("Lead", ActivityStatus.Completed),
            SubjectActivity("Lead", ActivityStatus.NotStated),
            SubjectActivity("Ticket", ActivityStatus.Completed),
        };

        // Act
        var report = ContactCenterReportingService.BuildSubjectInventory(_from, _to, activities);

        // Assert
        Assert.Equal(2, report.Rows.Count);

        var lead = report.Rows.Single(row => row.SubjectContentType == "Lead");

        Assert.Equal(2, lead.Counts.Total);
        Assert.Equal(1, lead.Counts.Completed);
        Assert.Equal(1, lead.Counts.Pending);
        Assert.Equal(3, report.Totals.Total);
    }

    private static Interaction Interaction(
        InteractionDirection direction,
        InteractionStatus status,
        int? answeredAfter,
        int? endedAfter,
        DateTime? createdUtc = null)
    {
        var created = createdUtc ?? _day;

        return new Interaction
        {
            ItemId = Guid.NewGuid().ToString("n"),
            Channel = InteractionChannel.Voice,
            Direction = direction,
            Status = status,
            CreatedUtc = created,
            AnsweredUtc = answeredAfter.HasValue ? created.AddSeconds(answeredAfter.Value) : null,
            EndedUtc = endedAfter.HasValue ? created.AddSeconds(endedAfter.Value) : null,
        };
    }

    private static Interaction AgentInteraction(string agentId, InteractionDirection direction, int? answeredAfter, int? endedAfter)
    {
        var interaction = Interaction(direction, InteractionStatus.Ended, answeredAfter, endedAfter);
        interaction.AgentId = agentId;

        return interaction;
    }

    private static Interaction QueueInteraction(string queueId, InteractionDirection direction, InteractionStatus status, int? answeredAfter, int? endedAfter)
    {
        var interaction = Interaction(direction, status, answeredAfter, endedAfter);
        interaction.QueueId = queueId;

        return interaction;
    }

    private static OmnichannelActivityIndex ActivityIndex(string campaignId, ActivityStatus status)
    {
        return new OmnichannelActivityIndex
        {
            CampaignId = campaignId,
            Status = status,
            CreatedUtc = _day,
            Attempts = 1,
        };
    }

    private static OmnichannelActivityIndex SubjectActivity(string subjectContentType, ActivityStatus status)
    {
        return new OmnichannelActivityIndex
        {
            SubjectContentType = subjectContentType,
            Status = status,
            CreatedUtc = _day,
            Attempts = 1,
        };
    }

    private static OmnichannelActivityIndex AutomatedCallActivity(string campaignId, ActivityStatus status)
    {
        return new OmnichannelActivityIndex
        {
            CampaignId = campaignId,
            Kind = ActivityKind.Call,
            Source = ActivitySources.PowerDial,
            InteractionType = ActivityInteractionType.Automated,
            Channel = OmnichannelConstants.Channels.Phone,
            Status = status,
            CreatedUtc = _day,
            Attempts = 1,
        };
    }
}
