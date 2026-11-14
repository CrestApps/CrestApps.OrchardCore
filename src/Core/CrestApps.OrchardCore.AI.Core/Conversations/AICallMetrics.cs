using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.AI.Core.Models;

namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Turns an AI call's measurements into what is reported: latency percentiles and the estimated cost.
/// </summary>
public static class AICallMetrics
{
    /// <summary>
    /// The value below which the given share of the samples fall, by the nearest-rank method.
    /// </summary>
    /// <param name="samples">The samples.</param>
    /// <param name="percentile">The percentile, from 0 to 100.</param>
    /// <returns>The value, or <see langword="null"/> without samples.</returns>
    public static int? Percentile(IReadOnlyCollection<int> samples, double percentile)
    {
        if (samples is not { Count: > 0 })
        {
            return null;
        }

        var sorted = samples.OrderBy(sample => sample).ToArray();
        var rank = (int)Math.Ceiling(Math.Clamp(percentile, 0, 100) / 100 * sorted.Length);

        return sorted[Math.Clamp(rank - 1, 0, sorted.Length - 1)];
    }

    /// <summary>
    /// Puts the latency percentiles of a call on its summary: the time from the caller's turn to the AI's first audio,
    /// the model's part of it, and the time the turn detector waited after the caller stopped.
    /// </summary>
    /// <param name="summary">The summary.</param>
    /// <param name="responseLatenciesMs">Each answer's time from the caller's turn to the AI's first audio.</param>
    /// <param name="modelLatenciesMs">Each answer's model time.</param>
    /// <param name="detectorWaitsMs">Each answer's turn-detector wait, where it was seen.</param>
    public static void ApplyLatencies(AIVoiceSessionSummary summary, IReadOnlyCollection<int> responseLatenciesMs, IReadOnlyCollection<int> modelLatenciesMs, IReadOnlyCollection<int> detectorWaitsMs)
    {
        ArgumentNullException.ThrowIfNull(summary);

        summary.LatencySamples = responseLatenciesMs?.Count ?? 0;
        summary.ResponseLatencyP50Ms = Percentile(responseLatenciesMs, 50);
        summary.ResponseLatencyP90Ms = Percentile(responseLatenciesMs, 90);
        summary.ResponseLatencyP95Ms = Percentile(responseLatenciesMs, 95);
        summary.ResponseLatencyMaxMs = responseLatenciesMs is { Count: > 0 } ? responseLatenciesMs.Max() : null;
        summary.ModelLatencyP50Ms = Percentile(modelLatenciesMs, 50);
        summary.DetectorWaitP50Ms = Percentile(detectorWaitsMs, 50);
    }

    /// <summary>
    /// Estimates what a call cost: its minutes with the carrier, at the carrier's price of a minute, and its tokens or
    /// minutes with the AI deployment, at the prices set on the deployment. The AI part is priced like any metered
    /// request: input and output text and audio tokens, cached tokens, and the call's length for deployments billed
    /// by the minute (<see cref="AIUsageMeters.AudioSeconds"/>).
    /// </summary>
    /// <param name="summary">The call's summary.</param>
    /// <param name="carrierPerMinute">What a minute of a phone call costs with the carrier, or zero when not set.</param>
    /// <param name="carrierCurrency">The currency of the carrier's price.</param>
    /// <param name="pricing">The prices of the deployment that answered the call, or <see langword="null"/>.</param>
    /// <returns>The cost and its currency, or <see langword="null"/> for both when no price is set. When the carrier's
    /// and the deployment's prices are in different currencies they cannot be added, and the AI part is returned.</returns>
    public static (decimal? Cost, string Currency) EstimateCost(AIVoiceSessionSummary summary, decimal carrierPerMinute, string carrierCurrency, AIDeploymentPricing pricing)
    {
        ArgumentNullException.ThrowIfNull(summary);

        var minutes = (decimal)(summary.SessionDurationMs ?? 0) / 60_000m;
        decimal? carrierCost = carrierPerMinute > 0
            ? minutes * carrierPerMinute
            : null;
        var aiCost = AIUsageCostEstimator.Estimate(ToUsageRecord(summary), pricing);

        if (aiCost is null)
        {
            return carrierCost is null
                ? (null, null)
                : (decimal.Round(carrierCost.Value, 4), carrierCurrency);
        }

        if (carrierCost is null || !string.Equals(carrierCurrency, pricing.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return (decimal.Round(aiCost.Value, 4), pricing.Currency);
        }

        return (decimal.Round(carrierCost.Value + aiCost.Value, 4), pricing.Currency);
    }

    /// <summary>
    /// Describes a call's AI usage as one metered request, so it is priced by the same rules as every other request.
    /// </summary>
    private static AICompletionUsageRecord ToUsageRecord(AIVoiceSessionSummary summary)
    {
        var inputAudioTokens = summary.InputAudioTokens ?? 0;
        var outputAudioTokens = summary.OutputAudioTokens ?? 0;

        return new AICompletionUsageRecord
        {
            DeploymentName = summary.DeploymentName,
            InputTokenCount = ToInt((summary.InputTextTokens ?? 0) + inputAudioTokens),
            OutputTokenCount = ToInt((summary.OutputTextTokens ?? 0) + outputAudioTokens),
            InputAudioTokenCount = ToInt(inputAudioTokens),
            OutputAudioTokenCount = ToInt(outputAudioTokens),
            CachedInputTokenCount = ToInt(summary.CachedInputTokens ?? 0),
            AudioDurationMs = summary.SessionDurationMs ?? 0,
        };
    }

    private static int ToInt(long value) => (int)Math.Clamp(value, 0, int.MaxValue);
}
