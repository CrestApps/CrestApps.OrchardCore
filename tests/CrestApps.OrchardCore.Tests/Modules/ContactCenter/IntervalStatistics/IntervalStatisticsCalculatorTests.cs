using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.IntervalStatistics;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.IntervalStatistics;

/// <summary>
/// The interval calculator: work counts in the 15-minute interval it was offered in, with the same rules as the Contact
/// Center reports, so a day's intervals add up to the day's report; agent state time is split at interval boundaries.
/// </summary>
public sealed class IntervalStatisticsCalculatorTests
{
    private static readonly DateTime _day = new(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void QueueWork_CountsInTheIntervalItWasOfferedIn()
    {
        var interactions = new[]
        {
            Inbound("a", "sales", created: At(9, 2), answered: At(9, 2, 30), ended: At(9, 8)),
            Inbound("b", "sales", created: At(9, 14), answered: At(9, 17), ended: At(9, 20)),
            Inbound("c", "sales", created: At(9, 16), answered: null, ended: At(9, 16, 3), status: InteractionStatus.Ended),
            Inbound("d", "sales", created: At(9, 20), answered: null, ended: At(9, 21), status: InteractionStatus.Ended),
        };

        var days = IntervalStatisticsCalculator.ComputeQueues(_day, interactions, Queues(("sales", 60)), InteractionOutcomeClassifier.WithoutEvents, shortAbandonSeconds: 5);

        var day = Assert.Single(days);
        Assert.Equal("sales", day.QueueId);
        Assert.Equal(_day, day.Date);
        Assert.Equal(2, day.Intervals.Count);

        var first = day.Intervals[0];
        Assert.Equal(At(9, 0), first.IntervalStartUtc);
        Assert.Equal(2, first.Offered);
        Assert.Equal(2, first.Answered);
        Assert.Equal(2, first.Handled);
        Assert.Equal(2, first.ServiceLevelOffered);

        // 30 seconds and 3 minutes: only the first is inside the 60-second threshold.
        Assert.Equal(1, first.AnsweredWithinServiceLevel);
        Assert.Equal(210, first.AnswerWaitSeconds);
        Assert.Equal(180, first.MaxAnswerWaitSeconds);
        Assert.Equal(60, first.ServiceLevelThresholdSeconds);

        var second = day.Intervals[1];
        Assert.Equal(At(9, 15), second.IntervalStartUtc);
        Assert.Equal(2, second.Offered);
        Assert.Equal(2, second.Abandoned);

        // The caller who hung up after 3 seconds is a short abandon; the one who waited a minute is not.
        Assert.Equal(1, second.ShortAbandoned);
        Assert.Equal(0d, second.ServiceLevel);
    }

    [Fact]
    public void ADaysIntervals_AddUpToTheDaysReport()
    {
        var random = new Random(7);
        var interactions = new List<Interaction>();

        for (var i = 0; i < 400; i++)
        {
            var created = _day.AddSeconds(random.Next(0, 86_400));
            var queue = random.Next(3) == 0 ? "support" : "sales";
            var answered = random.Next(4) == 0 ? (DateTime?)null : created.AddSeconds(random.Next(1, 400));
            var ended = (answered ?? created).AddSeconds(random.Next(5, 900));

            interactions.Add(Inbound("i" + i, queue, created, answered, ended, answered is null ? InteractionStatus.Ended : InteractionStatus.Ended));
        }

        var queues = Queues(("sales", 30), ("support", 120));
        var days = IntervalStatisticsCalculator.ComputeQueues(_day, interactions, queues, InteractionOutcomeClassifier.WithoutEvents, shortAbandonSeconds: 5);

        foreach (var day in days)
        {
            var ofQueue = interactions.Where(interaction => interaction.QueueId == day.QueueId).ToArray();
            var expected = InteractionMetricsCalculator.Aggregate(ofQueue);
            var serviceLevel = InteractionMetricsCalculator.CalculateQueueServiceLevel(ofQueue, queues[day.QueueId].SlaThresholdSeconds);
            var total = Sum(day.Intervals);

            Assert.Equal(expected.InboundOffered, total.Offered);
            Assert.Equal(expected.InboundAnswered, total.Answered);
            Assert.Equal(expected.Handled, total.Handled);
            Assert.Equal(expected.Abandoned, total.Abandoned);
            Assert.Equal(expected.AnswerSpeedSeconds, total.AnswerWaitSeconds, 6);
            Assert.Equal(expected.TalkSeconds + expected.WrapUpSeconds, total.HandleSeconds, 6);
            Assert.Equal(serviceLevel.ServiceLevelEligibleOffered, total.ServiceLevelOffered);
            Assert.Equal(serviceLevel.AnsweredWithinThreshold, total.AnsweredWithinServiceLevel);
        }
    }

    [Fact]
    public void Rollup_CombinesIntoHalfHoursAndHours()
    {
        var intervals = new[]
        {
            new QueueIntervalStatistic { IntervalStartUtc = At(9, 0), Offered = 3, MaxAnswerWaitSeconds = 10 },
            new QueueIntervalStatistic { IntervalStartUtc = At(9, 15), Offered = 4, MaxAnswerWaitSeconds = 40 },
            new QueueIntervalStatistic { IntervalStartUtc = At(9, 30), Offered = 5, MaxAnswerWaitSeconds = 20 },
        };

        var halfHours = IntervalStatisticsRollup.Combine(intervals, 30);
        var hours = IntervalStatisticsRollup.Combine(intervals, 60);

        Assert.Equal([7L, 5L], halfHours.Select(interval => interval.Offered));
        Assert.Equal(30, halfHours[0].IntervalMinutes);
        Assert.Equal(12, Assert.Single(hours).Offered);
        Assert.Equal(40, hours[0].MaxAnswerWaitSeconds);
        Assert.Throws<ArgumentOutOfRangeException>(() => IntervalStatisticsRollup.Combine(intervals, 20));
    }

    [Fact]
    public void AgentTime_IsSplitAtIntervalBoundaries()
    {
        var presence = new[]
        {
            Presence("agent-1", AgentPresenceStatus.Available, null, At(9, 5), At(9, 40)),
            Presence("agent-1", AgentPresenceStatus.Break, "Lunch", At(9, 40), At(9, 50)),
            Presence("agent-1", AgentPresenceStatus.Offline, null, At(9, 50), At(10, 30)),
        };
        var interactions = new[]
        {
            Inbound("a", "sales", created: At(9, 20), answered: At(9, 21), ended: At(9, 26), agentId: "agent-1"),
        };

        var days = IntervalStatisticsCalculator.ComputeAgents(_day, presence, interactions, Agents(("agent-1", "user-1")), InteractionOutcomeClassifier.WithoutEvents);

        var day = Assert.Single(days);
        Assert.Equal("user-1", day.UserId);
        Assert.Equal([At(9, 0), At(9, 15), At(9, 30), At(9, 45)], day.Intervals.Select(interval => interval.IntervalStartUtc));
        Assert.Equal(600, day.Intervals[0].SignedInSeconds);
        Assert.Equal(900, day.Intervals[1].SignedInSeconds);
        Assert.Equal(600, day.Intervals[2].SecondsIn(AgentPresenceStatus.Available));
        Assert.Equal(300, day.Intervals[2].SecondsIn(AgentPresenceStatus.Break));
        Assert.Equal("Lunch", day.Intervals[3].States.Single(state => state.Status == AgentPresenceStatus.Break).Reason);

        // Offline time is not signed-in time, so the agent's last interval holds only the five minutes of break.
        Assert.Equal(300, day.Intervals[3].SignedInSeconds);

        var queue = Assert.Single(day.Intervals[1].Queues);
        Assert.Equal("sales", queue.QueueId);
        Assert.Equal(1, queue.Handled);
        Assert.Equal(300, queue.HandleSeconds);
    }

    [Fact]
    public void AgentTime_OutsideTheDay_IsLeftOut()
    {
        var presence = new[]
        {
            Presence("agent-1", AgentPresenceStatus.Available, null, _day.AddMinutes(-30), _day.AddMinutes(10)),
        };

        var day = Assert.Single(IntervalStatisticsCalculator.ComputeAgents(_day, presence, [], Agents(("agent-1", "user-1")), InteractionOutcomeClassifier.WithoutEvents));

        Assert.Equal(600, Assert.Single(day.Intervals).SignedInSeconds);
    }

    private static QueueIntervalStatistic Sum(IEnumerable<QueueIntervalStatistic> intervals)
    {
        var total = new QueueIntervalStatistic();

        foreach (var interval in intervals)
        {
            total.Add(interval);
        }

        return total;
    }

    private static DateTime At(int hour, int minute, int second = 0)
        => _day.AddHours(hour).AddMinutes(minute).AddSeconds(second);

    private static Interaction Inbound(string id, string queueId, DateTime created, DateTime? answered, DateTime? ended, InteractionStatus status = InteractionStatus.Ended, string agentId = null)
        => new Interaction
        {
            ItemId = id,
            Channel = InteractionChannel.Voice,
            Direction = InteractionDirection.Inbound,
            QueueId = queueId,
            AgentId = agentId,
            CreatedUtc = created,
            AnsweredUtc = answered,
            EndedUtc = ended,
        }.RestorePersistedStatus(status);

    private static AgentPresenceInterval Presence(string agentId, AgentPresenceStatus status, string reason, DateTime start, DateTime end)
        => new()
        {
            AgentId = agentId,
            Status = status,
            Reason = reason,
            StartUtc = start,
            EndUtc = end,
        };

    private static Dictionary<string, ActivityQueue> Queues(params (string Id, int Threshold)[] queues)
        => queues.ToDictionary(queue => queue.Id, queue => new ActivityQueue { ItemId = queue.Id, Name = queue.Id, SlaThresholdSeconds = queue.Threshold }, StringComparer.Ordinal);

    private static Dictionary<string, AgentProfile> Agents(params (string Id, string UserId)[] agents)
        => agents.ToDictionary(agent => agent.Id, agent => new AgentProfile { ItemId = agent.Id, UserId = agent.UserId }, StringComparer.Ordinal);
}
