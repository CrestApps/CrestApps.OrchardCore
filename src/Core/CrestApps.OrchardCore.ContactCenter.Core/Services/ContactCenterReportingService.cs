using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IContactCenterReportingService"/> by aggregating the
/// interaction history and the CRM activity inventory over a reporting period.
/// </summary>
public sealed class ContactCenterReportingService : IContactCenterReportingService
{
    private readonly ISession _session;
    private readonly IActivityQueueManager _queueManager;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly ICatalogManager<OmnichannelCampaign> _campaignManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterReportingService"/> class.
    /// </summary>
    /// <param name="session">The YesSql session used to query interactions and activities.</param>
    /// <param name="queueManager">The queue manager used to resolve queue names and settings.</param>
    /// <param name="queueItemManager">The queue item manager used to read live waiting depth.</param>
    /// <param name="agentManager">The agent profile manager used to resolve agent names.</param>
    /// <param name="campaignManager">The campaign manager used to resolve campaign names.</param>
    public ContactCenterReportingService(
        ISession session,
        IActivityQueueManager queueManager,
        IQueueItemManager queueItemManager,
        IAgentProfileManager agentManager,
        ICatalogManager<OmnichannelCampaign> campaignManager)
    {
        _session = session;
        _queueManager = queueManager;
        _queueItemManager = queueItemManager;
        _agentManager = agentManager;
        _campaignManager = campaignManager;
    }

    /// <inheritdoc/>
    public Task<CallInsightsReport> GetCallInsightsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        return GetCallInsightsAsync(fromUtc, toUtc, criteria: null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CallInsightsReport> GetCallInsightsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        ContactCenterReportCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var interactions = await QueryInteractionsAsync(fromUtc, toUtc, cancellationToken);

        return BuildCallInsights(fromUtc, toUtc, FilterInteractions(interactions, criteria));
    }

    /// <inheritdoc/>
    public Task<AgentProductivityReport> GetAgentProductivityAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        return GetAgentProductivityAsync(fromUtc, toUtc, criteria: null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<AgentProductivityReport> GetAgentProductivityAsync(
        DateTime fromUtc,
        DateTime toUtc,
        ContactCenterReportCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var interactions = await QueryInteractionsAsync(fromUtc, toUtc, cancellationToken);
        var agents = (await _agentManager.GetAllAsync(cancellationToken)).ToArray();
        var filteredAgents = string.IsNullOrEmpty(criteria?.AgentId)
            ? agents
            : agents.Where(agent => agent.ItemId == criteria.AgentId).ToArray();
        var completedByUser = await QueryCompletedActivitiesByUserAsync(
            fromUtc,
            toUtc,
            criteria,
            filteredAgents,
            cancellationToken);

        return BuildAgentProductivity(fromUtc, toUtc, FilterInteractions(interactions, criteria), completedByUser, filteredAgents);
    }

    /// <inheritdoc/>
    public Task<QueueUsageReport> GetQueueUsageAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        return GetQueueUsageAsync(fromUtc, toUtc, criteria: null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<QueueUsageReport> GetQueueUsageAsync(
        DateTime fromUtc,
        DateTime toUtc,
        ContactCenterReportCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var interactions = await QueryInteractionsAsync(fromUtc, toUtc, cancellationToken);
        var queues = (await _queueManager.GetAllAsync(cancellationToken)).ToArray();

        var waitingByQueue = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var queue in queues)
        {
            var waiting = await _queueItemManager.ListWaitingAsync(queue.ItemId, cancellationToken);
            waitingByQueue[queue.ItemId] = waiting.Count;
        }

        return BuildQueueUsage(fromUtc, toUtc, FilterInteractions(interactions, criteria), FilterQueues(queues, criteria), waitingByQueue);
    }

    /// <inheritdoc/>
    public Task<CampaignSummaryReport> GetCampaignSummaryAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        return GetCampaignSummaryAsync(fromUtc, toUtc, criteria: null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CampaignSummaryReport> GetCampaignSummaryAsync(
        DateTime fromUtc,
        DateTime toUtc,
        ContactCenterReportCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var activities = await QueryActivityIndexesAsync(fromUtc, toUtc, cancellationToken);
        var campaigns = await _campaignManager.GetAllAsync(cancellationToken);

        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var campaign in campaigns)
        {
            names[campaign.ItemId] = string.IsNullOrWhiteSpace(campaign.DisplayText) ? campaign.ItemId : campaign.DisplayText;
        }

        return BuildCampaignSummary(fromUtc, toUtc, FilterActivities(activities, criteria), names);
    }

    /// <inheritdoc/>
    public Task<SubjectInventoryReport> GetSubjectInventoryAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        return GetSubjectInventoryAsync(fromUtc, toUtc, criteria: null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SubjectInventoryReport> GetSubjectInventoryAsync(
        DateTime fromUtc,
        DateTime toUtc,
        ContactCenterReportCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var activities = await QueryActivityIndexesAsync(fromUtc, toUtc, cancellationToken);

        return BuildSubjectInventory(fromUtc, toUtc, FilterActivities(activities, criteria));
    }

    /// <summary>
    /// Applies the supplied report criteria to an interaction population.
    /// </summary>
    /// <param name="interactions">The interactions to filter.</param>
    /// <param name="criteria">The optional report criteria.</param>
    /// <returns>The filtered interactions.</returns>
    public static IReadOnlyList<Interaction> FilterInteractions(
        IReadOnlyList<Interaction> interactions,
        ContactCenterReportCriteria criteria)
    {
        if (criteria is null)
        {
            return interactions;
        }

        return interactions
            .Where(interaction => string.IsNullOrEmpty(criteria.QueueId) || interaction.QueueId == criteria.QueueId)
            .Where(interaction => string.IsNullOrEmpty(criteria.AgentId) || interaction.AgentId == criteria.AgentId)
            .Where(interaction => !criteria.Channel.HasValue || interaction.Channel == criteria.Channel.Value)
            .Where(interaction => !criteria.Direction.HasValue || interaction.Direction == criteria.Direction.Value)
            .ToArray();
    }

    /// <summary>
    /// Applies the supplied report criteria to a CRM activity population.
    /// </summary>
    /// <param name="activities">The activity indexes to filter.</param>
    /// <param name="criteria">The optional report criteria.</param>
    /// <returns>The filtered activity indexes.</returns>
    public static IReadOnlyList<OmnichannelActivityIndex> FilterActivities(
        IReadOnlyList<OmnichannelActivityIndex> activities,
        ContactCenterReportCriteria criteria)
    {
        if (criteria is null)
        {
            return activities;
        }

        var channel = criteria.Channel switch
        {
            InteractionChannel.Voice => OmnichannelConstants.Channels.Phone,
            InteractionChannel.Sms => OmnichannelConstants.Channels.Sms,
            InteractionChannel.Email => OmnichannelConstants.Channels.Email,
            InteractionChannel.Chat => InteractionChannel.Chat.ToString(),
            _ => null,
        };

        return activities
            .Where(activity => string.IsNullOrEmpty(criteria.CampaignId) || activity.CampaignId == criteria.CampaignId)
            .Where(activity => string.IsNullOrEmpty(criteria.ActivitySource) || activity.Source == criteria.ActivitySource)
            .Where(activity => string.IsNullOrEmpty(channel) || string.Equals(activity.Channel, channel, StringComparison.OrdinalIgnoreCase))
            .Where(activity => !criteria.ActivityStatus.HasValue || activity.Status == criteria.ActivityStatus.Value)
            .ToArray();
    }

    private static IReadOnlyList<ActivityQueue> FilterQueues(
        IReadOnlyList<ActivityQueue> queues,
        ContactCenterReportCriteria criteria)
    {
        if (string.IsNullOrEmpty(criteria?.QueueId))
        {
            return queues;
        }

        return queues.Where(queue => queue.ItemId == criteria.QueueId).ToArray();
    }

    internal static CallInsightsReport BuildCallInsights(DateTime fromUtc, DateTime toUtc, IReadOnlyList<Interaction> interactions)
    {
        var report = new CallInsightsReport
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Total = interactions.Count,
        };

        var talkTimeTotal = 0d;
        var wrapUpTimeTotal = 0d;
        var answerSpeedTotal = 0d;
        var answeredWithHandleTime = 0L;

        var channelCounts = new Dictionary<InteractionChannel, long>();
        var statusCounts = new Dictionary<InteractionStatus, long>();
        var dailyPoints = new Dictionary<DateOnly, CallInsightsDailyPoint>();

        foreach (var interaction in interactions)
        {
            if (interaction.Direction == InteractionDirection.Inbound)
            {
                report.Inbound++;
            }
            else
            {
                report.Outbound++;
            }

            var answered = interaction.AnsweredUtc.HasValue;
            var abandoned = !answered && interaction.Direction == InteractionDirection.Inbound && interaction.Status == InteractionStatus.Ended;

            if (answered)
            {
                report.Answered++;
                answerSpeedTotal += Math.Max(0d, (interaction.AnsweredUtc.Value - interaction.CreatedUtc).TotalSeconds);

                if (interaction.EndedUtc.HasValue && interaction.EndedUtc.Value >= interaction.AnsweredUtc.Value)
                {
                    talkTimeTotal += (interaction.EndedUtc.Value - interaction.AnsweredUtc.Value).TotalSeconds;
                    answeredWithHandleTime++;
                }

                wrapUpTimeTotal += GetWrapUpSeconds(interaction);
            }

            if (abandoned)
            {
                report.Abandoned++;
            }

            if (interaction.Status == InteractionStatus.Failed)
            {
                report.Failed++;
            }

            channelCounts[interaction.Channel] = channelCounts.GetValueOrDefault(interaction.Channel) + 1;
            statusCounts[interaction.Status] = statusCounts.GetValueOrDefault(interaction.Status) + 1;

            var day = DateOnly.FromDateTime(interaction.CreatedUtc);

            if (!dailyPoints.TryGetValue(day, out var point))
            {
                point = new CallInsightsDailyPoint { Date = day };
                dailyPoints[day] = point;
            }

            point.Total++;

            if (answered)
            {
                point.Answered++;
            }

            if (abandoned)
            {
                point.Abandoned++;
            }
        }

        report.TotalTalkTimeSeconds = talkTimeTotal;
        report.TotalWrapUpTimeSeconds = wrapUpTimeTotal;
        report.AverageHandleTimeSeconds = answeredWithHandleTime > 0 ? (talkTimeTotal + wrapUpTimeTotal) / answeredWithHandleTime : 0d;
        report.AverageSpeedOfAnswerSeconds = report.Answered > 0 ? answerSpeedTotal / report.Answered : 0d;

        report.ByChannel = channelCounts
            .OrderByDescending(entry => entry.Value)
            .Select(entry => new ContactCenterReportCount { Label = entry.Key.ToString(), Count = entry.Value })
            .ToList();

        report.ByStatus = statusCounts
            .OrderByDescending(entry => entry.Value)
            .Select(entry => new ContactCenterReportCount { Label = entry.Key.ToString(), Count = entry.Value })
            .ToList();

        report.Daily = dailyPoints.Values
            .OrderBy(point => point.Date)
            .ToList();

        return report;
    }

    internal static AgentProductivityReport BuildAgentProductivity(
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlyList<Interaction> interactions,
        IReadOnlyDictionary<string, long> completedByUser,
        IReadOnlyList<AgentProfile> agents)
    {
        var stats = new Dictionary<string, AgentProductivityRow>(StringComparer.Ordinal);

        foreach (var interaction in interactions)
        {
            if (!interaction.AnsweredUtc.HasValue || string.IsNullOrEmpty(interaction.AgentId))
            {
                continue;
            }

            if (!stats.TryGetValue(interaction.AgentId, out var row))
            {
                row = new AgentProductivityRow { AgentId = interaction.AgentId };
                stats[interaction.AgentId] = row;
            }

            row.InteractionsHandled++;

            if (interaction.Direction == InteractionDirection.Inbound)
            {
                row.InboundHandled++;
            }
            else
            {
                row.OutboundHandled++;
            }

            if (interaction.EndedUtc.HasValue && interaction.EndedUtc.Value >= interaction.AnsweredUtc.Value)
            {
                row.TotalTalkTimeSeconds += (interaction.EndedUtc.Value - interaction.AnsweredUtc.Value).TotalSeconds;
            }

            row.TotalWrapUpTimeSeconds += GetWrapUpSeconds(interaction);
        }

        foreach (var agent in agents)
        {
            var completed = !string.IsNullOrEmpty(agent.UserId) && completedByUser.TryGetValue(agent.UserId, out var count)
                ? count
                : 0L;

            stats.TryGetValue(agent.ItemId, out var row);

            if (row is null && completed == 0)
            {
                continue;
            }

            row ??= new AgentProductivityRow { AgentId = agent.ItemId };
            row.DisplayName = ResolveAgentName(agent);
            row.ActivitiesCompleted = completed;

            stats[agent.ItemId] = row;
        }

        foreach (var row in stats.Values)
        {
            if (string.IsNullOrEmpty(row.DisplayName))
            {
                row.DisplayName = row.AgentId;
            }

            row.AverageWrapUpTimeSeconds = row.InteractionsHandled > 0 ? row.TotalWrapUpTimeSeconds / row.InteractionsHandled : 0d;
            row.AverageHandleTimeSeconds = row.InteractionsHandled > 0
                ? (row.TotalTalkTimeSeconds + row.TotalWrapUpTimeSeconds) / row.InteractionsHandled
                : 0d;
        }

        return new AgentProductivityReport
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Rows = stats.Values
                .OrderByDescending(row => row.InteractionsHandled)
                .ThenByDescending(row => row.ActivitiesCompleted)
                .ToList(),
        };
    }

    internal static QueueUsageReport BuildQueueUsage(
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlyList<Interaction> interactions,
        IReadOnlyList<ActivityQueue> queues,
        IReadOnlyDictionary<string, int> waitingByQueue)
    {
        var byQueue = new Dictionary<string, QueueUsageAccumulator>(StringComparer.Ordinal);

        foreach (var interaction in interactions)
        {
            var key = interaction.QueueId ?? string.Empty;

            if (!byQueue.TryGetValue(key, out var accumulator))
            {
                accumulator = new QueueUsageAccumulator();
                byQueue[key] = accumulator;
            }

            accumulator.Handled++;

            if (interaction.AnsweredUtc.HasValue)
            {
                accumulator.Answered++;
                accumulator.AnswerSpeedTotal += Math.Max(0d, (interaction.AnsweredUtc.Value - interaction.CreatedUtc).TotalSeconds);

                if (interaction.EndedUtc.HasValue && interaction.EndedUtc.Value >= interaction.AnsweredUtc.Value)
                {
                    accumulator.TalkTimeTotal += (interaction.EndedUtc.Value - interaction.AnsweredUtc.Value).TotalSeconds;
                    accumulator.AnsweredWithHandleTime++;
                }
            }
            else if (interaction.Direction == InteractionDirection.Inbound && interaction.Status == InteractionStatus.Ended)
            {
                accumulator.Abandoned++;
            }
        }

        var report = new QueueUsageReport
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
        };

        foreach (var queue in queues)
        {
            byQueue.TryGetValue(queue.ItemId, out var accumulator);
            var waiting = waitingByQueue.GetValueOrDefault(queue.ItemId);

            if (accumulator is null && waiting == 0)
            {
                continue;
            }

            report.Rows.Add(BuildQueueRow(queue.ItemId, queue.Name ?? queue.ItemId, queue.SlaThresholdSeconds, waiting, accumulator));
            byQueue.Remove(queue.ItemId);
        }

        foreach (var entry in byQueue)
        {
            var name = string.IsNullOrEmpty(entry.Key) ? null : entry.Key;
            report.Rows.Add(BuildQueueRow(entry.Key, name, 0, 0, entry.Value));
        }

        report.Rows = report.Rows
            .OrderByDescending(row => row.InteractionsHandled)
            .ThenByDescending(row => row.CurrentWaiting)
            .ToList();

        return report;
    }

    internal static CampaignSummaryReport BuildCampaignSummary(
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlyList<OmnichannelActivityIndex> activities,
        IReadOnlyDictionary<string, string> campaignNames)
    {
        var report = new CampaignSummaryReport
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
        };

        foreach (var group in activities.GroupBy(activity => activity.CampaignId ?? string.Empty, StringComparer.Ordinal))
        {
            var counts = BuildCounts(group);

            report.Rows.Add(new CampaignSummaryRow
            {
                CampaignId = group.Key,
                CampaignName = string.IsNullOrEmpty(group.Key)
                    ? null
                    : campaignNames.GetValueOrDefault(group.Key, group.Key),
                Counts = counts,
            });

            Accumulate(report.Totals, counts);
        }

        report.Rows = report.Rows
            .OrderByDescending(row => row.Counts.Total)
            .ToList();

        return report;
    }

    internal static SubjectInventoryReport BuildSubjectInventory(
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlyList<OmnichannelActivityIndex> activities)
    {
        var report = new SubjectInventoryReport
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
        };

        foreach (var group in activities.GroupBy(activity => activity.SubjectContentType ?? string.Empty, StringComparer.Ordinal))
        {
            var counts = BuildCounts(group);

            report.Rows.Add(new SubjectInventoryRow
            {
                SubjectContentType = group.Key,
                Counts = counts,
            });

            Accumulate(report.Totals, counts);
        }

        report.Rows = report.Rows
            .OrderByDescending(row => row.Counts.Total)
            .ToList();

        return report;
    }

    private async Task<IReadOnlyList<Interaction>> QueryInteractionsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var interactions = await _session.Query<Interaction, InteractionIndex>(
            index => index.CreatedUtc >= fromUtc && index.CreatedUtc <= toUtc,
            collection: ContactCenterConstants.CollectionName)
            .ListAsync(cancellationToken);

        return interactions.ToArray();
    }

    private async Task<IReadOnlyList<OmnichannelActivityIndex>> QueryActivityIndexesAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var activities = await _session.QueryIndex<OmnichannelActivityIndex>(
            index => index.CreatedUtc >= fromUtc && index.CreatedUtc <= toUtc,
            collection: OmnichannelConstants.CollectionName)
            .ListAsync(cancellationToken);

        return activities.ToArray();
    }

    private async Task<IReadOnlyDictionary<string, long>> QueryCompletedActivitiesByUserAsync(
        DateTime fromUtc,
        DateTime toUtc,
        ContactCenterReportCriteria criteria,
        IReadOnlyList<AgentProfile> agents,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(criteria?.QueueId) || criteria?.Direction.HasValue == true)
        {
            return new Dictionary<string, long>(StringComparer.Ordinal);
        }

        var completed = await _session.QueryIndex<OmnichannelActivityIndex>(
            index => index.Status == ActivityStatus.Completed && index.CompletedUtc >= fromUtc && index.CompletedUtc <= toUtc,
            collection: OmnichannelConstants.CollectionName)
            .ListAsync(cancellationToken);

        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        var allowedUserIds = agents
            .Where(agent => !string.IsNullOrEmpty(agent.UserId))
            .Select(agent => agent.UserId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var activity in FilterActivities(completed.ToArray(), criteria))
        {
            var userId = activity.AssignedToId;

            if (string.IsNullOrEmpty(userId) || !allowedUserIds.Contains(userId))
            {
                continue;
            }

            result[userId] = result.GetValueOrDefault(userId) + 1;
        }

        return result;
    }

    private static QueueUsageRow BuildQueueRow(string queueId, string queueName, int slaThresholdSeconds, int currentWaiting, QueueUsageAccumulator accumulator)
    {
        var row = new QueueUsageRow
        {
            QueueId = queueId,
            QueueName = queueName,
            SlaThresholdSeconds = slaThresholdSeconds,
            CurrentWaiting = currentWaiting,
        };

        if (accumulator is not null)
        {
            row.InteractionsHandled = accumulator.Handled;
            row.Answered = accumulator.Answered;
            row.Abandoned = accumulator.Abandoned;
            row.AverageHandleTimeSeconds = accumulator.AnsweredWithHandleTime > 0 ? accumulator.TalkTimeTotal / accumulator.AnsweredWithHandleTime : 0d;
            row.AverageSpeedOfAnswerSeconds = accumulator.Answered > 0 ? accumulator.AnswerSpeedTotal / accumulator.Answered : 0d;
        }

        return row;
    }

    private static ActivityProgressCounts BuildCounts(IEnumerable<OmnichannelActivityIndex> activities)
    {
        var counts = new ActivityProgressCounts();

        foreach (var activity in activities)
        {
            counts.Total++;
            counts.TotalAttempts += Math.Max(0, activity.Attempts);

            switch (activity.Status)
            {
                case ActivityStatus.Completed:
                    counts.Completed++;

                    break;
                case ActivityStatus.Failed:
                    counts.Failed++;

                    break;
                case ActivityStatus.Cancelled:
                case ActivityStatus.Purged:
                    counts.Cancelled++;

                    break;
                case ActivityStatus.AwaitingAgentResponse:
                case ActivityStatus.AwaitingCustomerAnswer:
                case ActivityStatus.Reserved:
                case ActivityStatus.Dialing:
                case ActivityStatus.InProgress:
                    counts.InProgress++;

                    break;
                default:
                    counts.Pending++;

                    break;
            }
        }

        return counts;
    }

    private static void Accumulate(ActivityProgressCounts totals, ActivityProgressCounts counts)
    {
        totals.Total += counts.Total;
        totals.Completed += counts.Completed;
        totals.Pending += counts.Pending;
        totals.InProgress += counts.InProgress;
        totals.Failed += counts.Failed;
        totals.Cancelled += counts.Cancelled;
        totals.TotalAttempts += counts.TotalAttempts;
    }

    private static string ResolveAgentName(AgentProfile agent)
    {
        if (!string.IsNullOrWhiteSpace(agent.DisplayName))
        {
            return agent.DisplayName;
        }

        if (!string.IsNullOrWhiteSpace(agent.UserName))
        {
            return agent.UserName;
        }

        return agent.ItemId;
    }

    private static double GetWrapUpSeconds(Interaction interaction)
    {
        if (!interaction.WrapUpStartedUtc.HasValue ||
            !interaction.WrapUpCompletedUtc.HasValue ||
            interaction.WrapUpCompletedUtc.Value < interaction.WrapUpStartedUtc.Value)
        {
            return 0d;
        }

        return (interaction.WrapUpCompletedUtc.Value - interaction.WrapUpStartedUtc.Value).TotalSeconds;
    }

    private sealed class QueueUsageAccumulator
    {
        public long Handled { get; set; }

        public long Answered { get; set; }

        public long Abandoned { get; set; }

        public double TalkTimeTotal { get; set; }

        public double AnswerSpeedTotal { get; set; }

        public long AnsweredWithHandleTime { get; set; }
    }
}
