using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// Computes interaction volume, handling, and service-level metrics from raw interaction history.
/// The calculations are pure functions of the interactions they receive, so they carry no tenant or
/// report-rendering state and can be reused and unit-tested independently of a report provider. Whether an
/// interaction was answered, abandoned, sent to voicemail or failed is decided by an
/// <see cref="InteractionOutcomeClassifier"/>, the same one every other Contact Center report uses; without one, the
/// interactions alone decide.
/// </summary>
internal static class InteractionMetricsCalculator
{
    /// <summary>
    /// Aggregates volume, answer, handling, transfer, and recording metrics across a set of interactions.
    /// </summary>
    /// <param name="interactions">The interactions to aggregate.</param>
    /// <param name="outcomes">The classifier that decides each interaction's outcome.</param>
    /// <returns>The aggregated interaction metrics.</returns>
    public static InteractionMetrics Aggregate(IEnumerable<Interaction> interactions, InteractionOutcomeClassifier outcomes = null)
    {
        outcomes ??= InteractionOutcomeClassifier.WithoutEvents;
        var metrics = new InteractionMetrics();

        foreach (var interaction in interactions)
        {
            metrics.Total++;

            if (IsInboundOffered(interaction))
            {
                metrics.InboundOffered++;
            }

            var outcome = outcomes.Classify(interaction);

            if (outcome == InteractionOutcome.Answered)
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

            // Abandonment is measured against inbound volume, so only an inbound caller's abandon counts toward it.
            if (outcome == InteractionOutcome.Abandoned && IsInboundOffered(interaction))
            {
                metrics.Abandoned++;
            }
            else if (outcome == InteractionOutcome.Voicemail)
            {
                metrics.Voicemail++;
            }
            else if (outcome == InteractionOutcome.CallbackRequested)
            {
                metrics.CallbackRequested++;
            }
            else if (outcome == InteractionOutcome.Failed)
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
    /// <param name="outcomes">The classifier that decides each interaction's outcome.</param>
    /// <returns>The queue service-level metrics.</returns>
    public static QueueServiceLevelMetrics CalculateQueueServiceLevel(IEnumerable<Interaction> interactions, int thresholdSeconds, InteractionOutcomeClassifier outcomes = null)
    {
        outcomes ??= InteractionOutcomeClassifier.WithoutEvents;
        var metrics = new QueueServiceLevelMetrics();

        foreach (var interaction in interactions)
        {
            if (!IsInboundOffered(interaction))
            {
                continue;
            }

            // A call sent to voicemail, or a caller who took a callback, was neither answered nor abandoned, so it is
            // not in the service level.
            var outcome = outcomes.Classify(interaction);

            if (outcome == InteractionOutcome.Answered)
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
            else if (outcome == InteractionOutcome.Abandoned)
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
    /// <param name="outcomes">The classifier that decides each interaction's outcome.</param>
    /// <returns>The combined queue service-level metrics.</returns>
    public static QueueServiceLevelMetrics CalculateCombinedQueueServiceLevel(
        IEnumerable<Interaction> interactions,
        IReadOnlyDictionary<string, ActivityQueue> queues,
        InteractionOutcomeClassifier outcomes = null)
    {
        outcomes ??= InteractionOutcomeClassifier.WithoutEvents;
        var metrics = new QueueServiceLevelMetrics();

        foreach (var interaction in interactions)
        {
            if (!IsInboundOffered(interaction))
            {
                continue;
            }

            queues.TryGetValue(interaction.QueueId ?? string.Empty, out var queue);
            var thresholdSeconds = queue?.SlaThresholdSeconds ?? 0;

            // A call sent to voicemail, or a caller who took a callback, was neither answered nor abandoned, so it is
            // not in the service level.
            var outcome = outcomes.Classify(interaction);

            if (outcome == InteractionOutcome.Answered)
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
            else if (outcome == InteractionOutcome.Abandoned)
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
