using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Aggregates a period's interactions into the call insights report. Each interaction is counted under the outcome
/// the shared <see cref="InteractionOutcomeClassifier"/> gives it, so these figures agree with every other report.
/// </summary>
internal static class CallInsightsBuilder
{
    /// <summary>
    /// Builds the call insights report.
    /// </summary>
    /// <param name="fromUtc">The start of the period.</param>
    /// <param name="toUtc">The end of the period.</param>
    /// <param name="interactions">The period's interactions.</param>
    /// <param name="outcomes">The classifier that decides each interaction's outcome.</param>
    /// <returns>The report.</returns>
    public static CallInsightsReport Build(
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlyList<Interaction> interactions,
        InteractionOutcomeClassifier outcomes)
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
        var outcomeCounts = new Dictionary<InteractionOutcome, long>();
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

            var outcome = outcomes.Classify(interaction);
            var answered = outcome == InteractionOutcome.Answered;
            var abandoned = outcome == InteractionOutcome.Abandoned && interaction.Direction == InteractionDirection.Inbound;

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
            else if (abandoned)
            {
                report.Abandoned++;
            }
            else if (outcome == InteractionOutcome.Voicemail)
            {
                report.Voicemail++;
            }
            else if (outcome == InteractionOutcome.CallbackRequested)
            {
                report.CallbackRequested++;
            }
            else if (outcome == InteractionOutcome.Failed)
            {
                report.Failed++;
            }

            channelCounts[interaction.Channel] = channelCounts.GetValueOrDefault(interaction.Channel) + 1;
            statusCounts[interaction.Status] = statusCounts.GetValueOrDefault(interaction.Status) + 1;
            outcomeCounts[outcome] = outcomeCounts.GetValueOrDefault(outcome) + 1;

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
        report.ByChannel = Counts(channelCounts);
        report.ByStatus = Counts(statusCounts);
        report.ByOutcome = Counts(outcomeCounts);
        report.Daily = [.. dailyPoints.Values.OrderBy(point => point.Date)];

        return report;
    }

    /// <summary>
    /// Gets the after-call work an interaction's wrap-up took.
    /// </summary>
    /// <param name="interaction">The interaction.</param>
    /// <returns>The wrap-up time in seconds, or zero when wrap-up did not start and complete in order.</returns>
    public static double GetWrapUpSeconds(Interaction interaction)
    {
        if (!interaction.WrapUpStartedUtc.HasValue ||
            !interaction.WrapUpCompletedUtc.HasValue ||
            interaction.WrapUpCompletedUtc.Value < interaction.WrapUpStartedUtc.Value)
        {
            return 0d;
        }

        return (interaction.WrapUpCompletedUtc.Value - interaction.WrapUpStartedUtc.Value).TotalSeconds;
    }

    private static List<ContactCenterReportCount> Counts<TKey>(Dictionary<TKey, long> counts)
        where TKey : notnull
        => [.. counts
            .OrderByDescending(entry => entry.Value)
            .Select(entry => new ContactCenterReportCount { Label = entry.Key.ToString(), Count = entry.Value })];
}
