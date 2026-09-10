using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// Computes interaction volume, handling, and service-level metrics from raw interaction history.
/// The calculations are pure functions of the interactions they receive, so they carry no tenant or
/// report-rendering state and can be reused and unit-tested independently of a report provider.
/// </summary>
internal static class InteractionMetricsCalculator
{
    /// <summary>
    /// Aggregates volume, answer, handling, transfer, and recording metrics across a set of interactions.
    /// </summary>
    /// <param name="interactions">The interactions to aggregate.</param>
    /// <returns>The aggregated interaction metrics.</returns>
    public static InteractionMetrics Aggregate(IEnumerable<Interaction> interactions)
    {
        var metrics = new InteractionMetrics();

        foreach (var interaction in interactions)
        {
            metrics.Total++;

            if (IsInboundOffered(interaction))
            {
                metrics.InboundOffered++;
            }

            if (interaction.AnsweredUtc.HasValue)
            {
                metrics.Answered++;

                if (interaction.Direction == InteractionDirection.Inbound)
                {
                    metrics.InboundAnswered++;
                    metrics.AnswerSpeedSeconds += GetWaitSeconds(interaction);
                }

                if (interaction.EndedUtc.HasValue && interaction.EndedUtc.Value >= interaction.AnsweredUtc.Value)
                {
                    metrics.Handled++;
                    metrics.TalkSeconds += GetTalkSeconds(interaction);
                    metrics.WrapUpSeconds += GetWrapUpSeconds(interaction);
                }

                if (interaction.TransferHistory.Count > 0)
                {
                    metrics.Transferred++;
                }

                if (interaction.Channel == InteractionChannel.Voice)
                {
                    metrics.AnsweredVoice++;

                    if (!string.IsNullOrEmpty(interaction.RecordingReference))
                    {
                        metrics.RecordedVoice++;
                    }
                }
            }

            if (IsAbandoned(interaction))
            {
                metrics.Abandoned++;
            }

            if (interaction.Status == InteractionStatus.Failed)
            {
                metrics.Failed++;
            }
        }

        return metrics;
    }

    /// <summary>
    /// Calculates queue service-level metrics against a single answer-time threshold.
    /// </summary>
    /// <param name="interactions">The interactions to evaluate.</param>
    /// <param name="thresholdSeconds">The answer-time threshold in seconds; a non-positive value disables service-level tracking.</param>
    /// <returns>The queue service-level metrics.</returns>
    public static QueueServiceLevelMetrics CalculateQueueServiceLevel(IEnumerable<Interaction> interactions, int thresholdSeconds)
    {
        var metrics = new QueueServiceLevelMetrics();

        foreach (var interaction in interactions)
        {
            if (!IsInboundOffered(interaction))
            {
                continue;
            }

            if (interaction.AnsweredUtc.HasValue)
            {
                metrics.EligibleOffered++;
                metrics.Answered++;
                metrics.AnswerSpeedSeconds += GetWaitSeconds(interaction);

                if (thresholdSeconds > 0)
                {
                    metrics.ServiceLevelEligibleOffered++;

                    if (GetWaitSeconds(interaction) <= thresholdSeconds)
                    {
                        metrics.AnsweredWithinThreshold++;
                    }
                }
            }
            else if (IsAbandoned(interaction))
            {
                metrics.EligibleOffered++;

                if (thresholdSeconds > 0)
                {
                    metrics.ServiceLevelEligibleOffered++;
                }
            }
        }

        metrics.HasServiceLevel = metrics.ServiceLevelEligibleOffered > 0;

        return metrics;
    }

    /// <summary>
    /// Calculates queue service-level metrics where each interaction is evaluated against its own queue's threshold.
    /// </summary>
    /// <param name="interactions">The interactions to evaluate.</param>
    /// <param name="queues">The queues keyed by identifier, used to resolve each interaction's answer-time threshold.</param>
    /// <returns>The combined queue service-level metrics.</returns>
    public static QueueServiceLevelMetrics CalculateCombinedQueueServiceLevel(
        IEnumerable<Interaction> interactions,
        IReadOnlyDictionary<string, ActivityQueue> queues)
    {
        var metrics = new QueueServiceLevelMetrics();

        foreach (var interaction in interactions)
        {
            if (!IsInboundOffered(interaction))
            {
                continue;
            }

            queues.TryGetValue(interaction.QueueId ?? string.Empty, out var queue);
            var thresholdSeconds = queue?.SlaThresholdSeconds ?? 0;

            if (interaction.AnsweredUtc.HasValue)
            {
                metrics.EligibleOffered++;
                metrics.Answered++;
                metrics.AnswerSpeedSeconds += GetWaitSeconds(interaction);

                if (thresholdSeconds > 0)
                {
                    metrics.ServiceLevelEligibleOffered++;

                    if (GetWaitSeconds(interaction) <= thresholdSeconds)
                    {
                        metrics.AnsweredWithinThreshold++;
                    }
                }
            }
            else if (IsAbandoned(interaction))
            {
                metrics.EligibleOffered++;

                if (thresholdSeconds > 0)
                {
                    metrics.ServiceLevelEligibleOffered++;
                }
            }
        }

        metrics.HasServiceLevel = metrics.ServiceLevelEligibleOffered > 0;

        return metrics;
    }

    /// <summary>
    /// Determines whether an interaction was offered inbound and therefore counts toward offered volume.
    /// </summary>
    /// <param name="interaction">The interaction to inspect.</param>
    /// <returns><see langword="true"/> when the interaction is inbound; otherwise <see langword="false"/>.</returns>
    public static bool IsInboundOffered(Interaction interaction)
    {
        return interaction.Direction == InteractionDirection.Inbound;
    }

    /// <summary>
    /// Determines whether an inbound interaction ended without being answered and therefore counts as abandoned.
    /// </summary>
    /// <param name="interaction">The interaction to inspect.</param>
    /// <returns><see langword="true"/> when the interaction was abandoned; otherwise <see langword="false"/>.</returns>
    public static bool IsAbandoned(Interaction interaction)
    {
        return interaction.Direction == InteractionDirection.Inbound &&
            !interaction.AnsweredUtc.HasValue &&
            interaction.Status == InteractionStatus.Ended;
    }

    /// <summary>
    /// Gets the seconds an interaction waited before being answered.
    /// </summary>
    /// <param name="interaction">The interaction to measure.</param>
    /// <returns>The wait time in seconds, or zero when the interaction was never answered.</returns>
    public static double GetWaitSeconds(Interaction interaction)
    {
        return interaction.AnsweredUtc.HasValue
            ? Math.Max(0d, (interaction.AnsweredUtc.Value - interaction.CreatedUtc).TotalSeconds)
            : 0d;
    }

    /// <summary>
    /// Gets the seconds an interaction waited from creation until it ended.
    /// </summary>
    /// <param name="interaction">The interaction to measure.</param>
    /// <returns>The wait-until-end time in seconds, or zero when the interaction never ended.</returns>
    public static double GetWaitUntilEndSeconds(Interaction interaction)
    {
        return interaction.EndedUtc.HasValue
            ? Math.Max(0d, (interaction.EndedUtc.Value - interaction.CreatedUtc).TotalSeconds)
            : 0d;
    }

    /// <summary>
    /// Gets the talk-time seconds between an interaction being answered and ended.
    /// </summary>
    /// <param name="interaction">The interaction to measure.</param>
    /// <returns>The talk time in seconds, or zero when the interaction was not answered and ended in order.</returns>
    public static double GetTalkSeconds(Interaction interaction)
    {
        return interaction.AnsweredUtc.HasValue &&
            interaction.EndedUtc.HasValue &&
            interaction.EndedUtc.Value >= interaction.AnsweredUtc.Value
            ? (interaction.EndedUtc.Value - interaction.AnsweredUtc.Value).TotalSeconds
            : 0d;
    }

    /// <summary>
    /// Gets the wrap-up seconds between an interaction's wrap-up starting and completing.
    /// </summary>
    /// <param name="interaction">The interaction to measure.</param>
    /// <returns>The wrap-up time in seconds, or zero when wrap-up did not start and complete in order.</returns>
    public static double GetWrapUpSeconds(Interaction interaction)
    {
        return interaction.WrapUpStartedUtc.HasValue &&
            interaction.WrapUpCompletedUtc.HasValue &&
            interaction.WrapUpCompletedUtc.Value >= interaction.WrapUpStartedUtc.Value
            ? (interaction.WrapUpCompletedUtc.Value - interaction.WrapUpStartedUtc.Value).TotalSeconds
            : 0d;
    }
}
