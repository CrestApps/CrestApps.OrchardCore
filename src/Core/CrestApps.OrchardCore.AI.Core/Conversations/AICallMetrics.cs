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
    /// Estimates what a call cost: its minutes with the carrier, its minutes or tokens with the AI deployment, and the
    /// texts sent during it.
    /// </summary>
    /// <param name="summary">The call's summary.</param>
    /// <param name="settings">The prices.</param>
    /// <param name="textsSent">How many texts were sent during the call.</param>
    /// <returns>The cost, or <see langword="null"/> when no price is set.</returns>
    public static decimal? EstimateCost(AIVoiceSessionSummary summary, AICostSettings settings, int textsSent = 0)
    {
        ArgumentNullException.ThrowIfNull(summary);

        if (settings is null)
        {
            return null;
        }

        var price = settings.Deployments?.FirstOrDefault(candidate => string.Equals(candidate.DeploymentName, summary.DeploymentName, StringComparison.OrdinalIgnoreCase));

        if (settings.TelephonyPerMinute == 0 && settings.SmsPerMessage == 0 && price is null)
        {
            return null;
        }

        var minutes = (decimal)(summary.SessionDurationMs ?? 0) / 60_000m;
        var cost = minutes * settings.TelephonyPerMinute + textsSent * settings.SmsPerMessage;

        if (price is not null)
        {
            cost += minutes * price.PerMinute;
            cost += PerMillion(summary.InputTextTokens, price.InputPerMillion);
            cost += PerMillion(summary.OutputTextTokens, price.OutputPerMillion);
            cost += PerMillion(summary.InputAudioTokens, price.AudioInputPerMillion);
            cost += PerMillion(summary.OutputAudioTokens, price.AudioOutputPerMillion);
        }

        return decimal.Round(cost, 4);
    }

    private static decimal PerMillion(long? tokens, decimal pricePerMillion)
        => tokens is > 0 ? tokens.Value * pricePerMillion / 1_000_000m : 0;
}
