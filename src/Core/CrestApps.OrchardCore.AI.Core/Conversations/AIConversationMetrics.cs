using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// The measures an alert can watch.
/// </summary>
public enum AIConversationMetric
{
    /// <summary>
    /// The number of conversations started.
    /// </summary>
    Conversations,

    /// <summary>
    /// The percentage of conversations the AI resolved.
    /// </summary>
    ResolutionRate,

    /// <summary>
    /// The average conversation length, in seconds.
    /// </summary>
    AverageHandleTimeSeconds,

    /// <summary>
    /// The average time the AI took to answer, in milliseconds.
    /// </summary>
    AverageResponseLatencyMs,

    /// <summary>
    /// The percentage of conversations the customer rated down.
    /// </summary>
    NegativeFeedbackRate,

    /// <summary>
    /// The average QA score.
    /// </summary>
    AverageQAScore,

    /// <summary>
    /// The percentage of evaluated conversations that failed QA.
    /// </summary>
    QAFailureRate,

    /// <summary>
    /// The percentage of evaluated conversations with a failed tool.
    /// </summary>
    ToolFailureRate,

    /// <summary>
    /// The percentage of evaluated conversations where the AI stated something it had no source for.
    /// </summary>
    HallucinationRate,

    /// <summary>
    /// The number of AI phone calls.
    /// </summary>
    AICalls,

    /// <summary>
    /// The percentage of AI phone calls handed to a person.
    /// </summary>
    AICallHandoffRate,

    /// <summary>
    /// The percentage of AI phone calls that failed.
    /// </summary>
    AICallFailureRate,

    /// <summary>
    /// The average time after the answer before the caller heard the AI, in milliseconds.
    /// </summary>
    AverageTimeToFirstAudioMs,
}

/// <summary>
/// A metric's value over a window.
/// </summary>
/// <param name="Value">The value, or <see langword="null"/> when nothing was measured.</param>
/// <param name="SampleSize">How many conversations, evaluations or calls it was measured over.</param>
public sealed record AIConversationMetricValue(double? Value, int SampleSize);

/// <summary>
/// Measures AI conversations over a window from the records reports read: the session metrics, the QA evaluations
/// and the AI call summaries.
/// </summary>
public sealed class AIConversationMetrics
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIConversationMetrics"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider. The session metrics store exists only while session
    /// analytics is on, so it is looked up when needed.</param>
    public AIConversationMetrics(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Measures a metric.
    /// </summary>
    /// <param name="metric">What to measure.</param>
    /// <param name="profileId">The AI profile, or <see langword="null"/> for all of them.</param>
    /// <param name="fromUtc">The inclusive start of the window.</param>
    /// <param name="toUtc">The exclusive end of the window.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task<AIConversationMetricValue> MeasureAsync(AIConversationMetric metric, string profileId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        switch (metric)
        {
            case AIConversationMetric.Conversations:
            case AIConversationMetric.ResolutionRate:
            case AIConversationMetric.AverageHandleTimeSeconds:
            case AIConversationMetric.AverageResponseLatencyMs:
            case AIConversationMetric.NegativeFeedbackRate:
                var eventStore = _serviceProvider.GetService<IAIChatSessionEventStore>();

                if (eventStore is null)
                {
                    return new AIConversationMetricValue(null, 0);
                }

                // The store filters whole days; the window is cut here.
                var events = (await eventStore.GetAsync(profileId, fromUtc.Date, toUtc, cancellationToken))
                    .Where(item => item.SessionStartedUtc >= fromUtc && item.SessionStartedUtc < toUtc)
                    .ToList();

                return MeasureSessions(metric, events);

            case AIConversationMetric.AverageQAScore:
            case AIConversationMetric.QAFailureRate:
            case AIConversationMetric.ToolFailureRate:
            case AIConversationMetric.HallucinationRate:
                var evaluationStore = _serviceProvider.GetRequiredService<IAIConversationEvaluationStore>();

                return MeasureEvaluations(metric, await evaluationStore.GetAsync(profileId, fromUtc, toUtc, cancellationToken));

            default:
                var summaryStore = _serviceProvider.GetService<IAIVoiceSessionSummaryStore>();

                if (summaryStore is null)
                {
                    return new AIConversationMetricValue(null, 0);
                }

                var calls = (await summaryStore.GetAsync(fromUtc, toUtc, cancellationToken))
                    .Where(call => call.CreatedUtc >= fromUtc && call.CreatedUtc < toUtc)
                    .Where(call => string.IsNullOrEmpty(profileId) || string.Equals(call.AIProfileId, profileId, StringComparison.Ordinal))
                    .ToList();

                return MeasureCalls(metric, calls);
        }
    }

    internal static AIConversationMetricValue MeasureSessions(AIConversationMetric metric, IReadOnlyList<AIChatSessionEvent> events)
        => metric switch
        {
            AIConversationMetric.Conversations => new(events.Count, events.Count),
            AIConversationMetric.ResolutionRate => Rate(events, item => item.IsResolved),
            AIConversationMetric.AverageHandleTimeSeconds => Average(events, item => item.HandleTimeSeconds),
            AIConversationMetric.AverageResponseLatencyMs => Average(events.Where(item => item.CompletionCount > 0).ToList(), item => item.AverageResponseLatencyMs),
            AIConversationMetric.NegativeFeedbackRate => Rate(events, item => item.ThumbsDownCount > 0 || item.UserRating == false),
            _ => throw new ArgumentOutOfRangeException(nameof(metric)),
        };

    internal static AIConversationMetricValue MeasureEvaluations(AIConversationMetric metric, IReadOnlyList<AIConversationEvaluationIndex> evaluations)
        => metric switch
        {
            AIConversationMetric.AverageQAScore => Average(evaluations, item => item.Score),
            AIConversationMetric.QAFailureRate => Rate(evaluations, item => !item.Passed),
            AIConversationMetric.ToolFailureRate => Rate(evaluations, item => HasIssue(item, AIConversationIssueKinds.ToolFailure)),
            AIConversationMetric.HallucinationRate => Rate(evaluations, item => HasIssue(item, AIConversationIssueKinds.Hallucination)),
            _ => throw new ArgumentOutOfRangeException(nameof(metric)),
        };

    internal static AIConversationMetricValue MeasureCalls(AIConversationMetric metric, IReadOnlyList<AIVoiceSessionSummaryIndex> calls)
        => metric switch
        {
            AIConversationMetric.AICalls => new(calls.Count, calls.Count),
            AIConversationMetric.AICallHandoffRate => Rate(calls, call => call.Outcome == nameof(AIVoiceSessionOutcome.HandedToAgent) || call.Outcome == nameof(AIVoiceSessionOutcome.Transferred)),
            AIConversationMetric.AICallFailureRate => Rate(calls, call => call.Outcome == nameof(AIVoiceSessionOutcome.Failed)),
            AIConversationMetric.AverageTimeToFirstAudioMs => Average(
                calls.Where(call => call.TimeToFirstAssistantAudioMs.HasValue).ToList(),
                call => call.TimeToFirstAssistantAudioMs.Value),
            _ => throw new ArgumentOutOfRangeException(nameof(metric)),
        };

    private static bool HasIssue(AIConversationEvaluationIndex evaluation, string kind)
        => !string.IsNullOrEmpty(evaluation.IssueKinds)
            && evaluation.IssueKinds.Split(',').Contains(kind, StringComparer.Ordinal);

    private static AIConversationMetricValue Rate<T>(IReadOnlyList<T> items, Func<T, bool> predicate)
        => items.Count == 0
            ? new(null, 0)
            : new(Math.Round(100d * items.Count(predicate) / items.Count, 2), items.Count);

    private static AIConversationMetricValue Average<T>(IReadOnlyList<T> items, Func<T, double> selector)
        => items.Count == 0
            ? new(null, 0)
            : new(Math.Round(items.Average(selector), 2), items.Count);
}
