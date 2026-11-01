using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using OrchardCore;

namespace CrestApps.OrchardCore.ContactCenter.IntervalStatistics;

/// <summary>
/// Computes one UTC day of 15-minute interval statistics. Queue work is counted by the calculator the Contact Center
/// reports use, in the interval it was offered in, so the intervals of a day add up to the day's report. Agent state
/// time is split at interval boundaries.
/// </summary>
internal static class IntervalStatisticsCalculator
{
    /// <summary>
    /// Computes the queues' intervals of a day.
    /// </summary>
    /// <param name="dayStart">The UTC midnight that starts the day.</param>
    /// <param name="interactions">The interactions created on the day.</param>
    /// <param name="queues">The queues by identifier, for their service level thresholds.</param>
    /// <param name="outcomes">The classifier that decides each interaction's outcome.</param>
    /// <param name="shortAbandonSeconds">The wait below which an abandon is a short abandon.</param>
    /// <returns>One day per queue that had work, with its intervals in order.</returns>
    public static List<QueueIntervalStatistics> ComputeQueues(
        DateTime dayStart,
        IEnumerable<Interaction> interactions,
        IReadOnlyDictionary<string, ActivityQueue> queues,
        InteractionOutcomeClassifier outcomes,
        int shortAbandonSeconds)
    {
        ArgumentNullException.ThrowIfNull(interactions);
        ArgumentNullException.ThrowIfNull(queues);
        outcomes ??= InteractionOutcomeClassifier.WithoutEvents;

        var days = new List<QueueIntervalStatistics>();

        foreach (var ofQueue in interactions.Where(interaction => !string.IsNullOrEmpty(interaction.QueueId)).GroupBy(interaction => interaction.QueueId, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var threshold = queues.TryGetValue(ofQueue.Key, out var queue) ? queue.SlaThresholdSeconds : 0;
            var day = new QueueIntervalStatistics
            {
                ItemId = IdGenerator.GenerateId(),
                QueueId = ofQueue.Key,
                Date = dayStart,
            };

            foreach (var group in ofQueue.GroupBy(interaction => (Start: IntervalStatisticsConstants.IntervalStart(interaction.CreatedUtc), interaction.Channel)).OrderBy(group => group.Key.Start).ThenBy(group => group.Key.Channel))
            {
                var items = group.ToArray();
                var metrics = InteractionMetricsCalculator.Aggregate(items, outcomes);
                var serviceLevel = InteractionMetricsCalculator.CalculateQueueServiceLevel(items, threshold, outcomes);
                var interval = new QueueIntervalStatistic
                {
                    QueueId = ofQueue.Key,
                    Channel = group.Key.Channel,
                    IntervalStartUtc = group.Key.Start,
                    Offered = metrics.InboundOffered,
                    Outbound = metrics.Total - metrics.InboundOffered,
                    Answered = metrics.InboundAnswered,
                    Handled = metrics.Handled,
                    Abandoned = metrics.Abandoned,
                    Voicemail = metrics.Voicemail,
                    CallbackRequested = metrics.CallbackRequested,
                    Transferred = metrics.Transferred,
                    ServiceLevelOffered = serviceLevel.ServiceLevelEligibleOffered,
                    AnsweredWithinServiceLevel = serviceLevel.AnsweredWithinThreshold,
                    ServiceLevelThresholdSeconds = threshold,
                    AnswerWaitSeconds = metrics.AnswerSpeedSeconds,
                    TalkSeconds = metrics.TalkSeconds,
                    WrapUpSeconds = metrics.WrapUpSeconds,
                };

                foreach (var interaction in items.Where(InteractionMetricsCalculator.IsInboundOffered))
                {
                    var outcome = outcomes.Classify(interaction);

                    if (outcome == InteractionOutcome.Answered)
                    {
                        interval.MaxAnswerWaitSeconds = Math.Max(interval.MaxAnswerWaitSeconds, InteractionMetricsCalculator.GetWaitSeconds(interaction));
                    }
                    else if (outcome == InteractionOutcome.Abandoned && outcomes.GetWaitBeforeAbandonSeconds(interaction) < shortAbandonSeconds)
                    {
                        interval.ShortAbandoned++;
                    }
                }

                day.Intervals.Add(interval);
            }

            days.Add(day);
        }

        return days;
    }

    /// <summary>
    /// Computes the agents' intervals of a day.
    /// </summary>
    /// <param name="dayStart">The UTC midnight that starts the day.</param>
    /// <param name="presence">The agents' state intervals over the day.</param>
    /// <param name="interactions">The interactions created on the day.</param>
    /// <param name="agents">The agent profiles by identifier, for their users.</param>
    /// <param name="outcomes">The classifier that decides each interaction's outcome.</param>
    /// <returns>One day per agent that was signed in or handled work, with its intervals in order.</returns>
    public static List<AgentIntervalStatistics> ComputeAgents(
        DateTime dayStart,
        IEnumerable<AgentPresenceInterval> presence,
        IEnumerable<Interaction> interactions,
        IReadOnlyDictionary<string, AgentProfile> agents,
        InteractionOutcomeClassifier outcomes)
    {
        ArgumentNullException.ThrowIfNull(presence);
        ArgumentNullException.ThrowIfNull(interactions);
        ArgumentNullException.ThrowIfNull(agents);
        outcomes ??= InteractionOutcomeClassifier.WithoutEvents;

        var dayEnd = dayStart.AddDays(1);
        var intervals = new Dictionary<(string AgentId, DateTime Start), AgentIntervalStatistic>();

        foreach (var span in presence)
        {
            if (span.Status == AgentPresenceStatus.Offline || string.IsNullOrEmpty(span.AgentId))
            {
                continue;
            }

            var start = span.StartUtc < dayStart ? dayStart : span.StartUtc;
            var end = span.EndUtc > dayEnd ? dayEnd : span.EndUtc;

            while (start < end)
            {
                var intervalStart = IntervalStatisticsConstants.IntervalStart(start);
                var pieceEnd = intervalStart + IntervalStatisticsConstants.Interval;

                if (pieceEnd > end)
                {
                    pieceEnd = end;
                }

                var seconds = (pieceEnd - start).TotalSeconds;
                var interval = GetOrAdd(intervals, agents, span.AgentId, intervalStart);
                var state = interval.States.FirstOrDefault(candidate => candidate.Status == span.Status && string.Equals(candidate.Reason, span.Reason, StringComparison.Ordinal));

                if (state is null)
                {
                    state = new AgentIntervalState { Status = span.Status, Reason = span.Reason };
                    interval.States.Add(state);
                }

                state.Seconds += seconds;
                interval.SignedInSeconds += seconds;
                start = pieceEnd;
            }
        }

        foreach (var interaction in interactions)
        {
            if (string.IsNullOrEmpty(interaction.AgentId) ||
                outcomes.Classify(interaction) != InteractionOutcome.Answered ||
                !interaction.EndedUtc.HasValue ||
                interaction.EndedUtc < interaction.AnsweredUtc)
            {
                continue;
            }

            var interval = GetOrAdd(intervals, agents, interaction.AgentId, IntervalStatisticsConstants.IntervalStart(interaction.CreatedUtc));
            var queueId = interaction.QueueId ?? string.Empty;
            var queue = interval.Queues.FirstOrDefault(candidate => string.Equals(candidate.QueueId, queueId, StringComparison.Ordinal));

            if (queue is null)
            {
                queue = new AgentIntervalQueue { QueueId = queueId };
                interval.Queues.Add(queue);
            }

            queue.Handled++;
            queue.HandleSeconds += InteractionMetricsCalculator.GetTalkSeconds(interaction) + InteractionMetricsCalculator.GetWrapUpSeconds(interaction);
        }

        return [.. intervals.Values
            .GroupBy(interval => interval.AgentId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new AgentIntervalStatistics
            {
                ItemId = IdGenerator.GenerateId(),
                AgentId = group.Key,
                UserId = group.First().UserId,
                Date = dayStart,
                Intervals = [.. group.OrderBy(interval => interval.IntervalStartUtc)],
            })];
    }

    private static AgentIntervalStatistic GetOrAdd(
        Dictionary<(string AgentId, DateTime Start), AgentIntervalStatistic> intervals,
        IReadOnlyDictionary<string, AgentProfile> agents,
        string agentId,
        DateTime start)
    {
        if (!intervals.TryGetValue((agentId, start), out var interval))
        {
            interval = new AgentIntervalStatistic
            {
                AgentId = agentId,
                UserId = agents.TryGetValue(agentId, out var agent) ? agent.UserId : null,
                IntervalStartUtc = start,
            };

            intervals.Add((agentId, start), interval);
        }

        return interval;
    }
}
