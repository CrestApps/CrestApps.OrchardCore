using System.Globalization;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Core.Models;

namespace CrestApps.OrchardCore.AI.Chat.Services;

/// <summary>
/// The voice half of the AI usage report, built from the per-call AI voice session summaries.
/// </summary>
internal static class AIVoiceUsageReport
{
    private const string Unknown = "Unknown";
    private const string NoCampaign = "No campaign";
    private const double MillisecondsPerMinute = 60_000d;

    /// <summary>
    /// One summary per call, for the chosen profile when one is chosen.
    /// </summary>
    /// <remarks>
    /// A call can be summarized twice when the hangup and the end of the live session race each other. The one
    /// that measured the audio is kept, a live session's over a turn-based one, and then the latest.
    /// </remarks>
    /// <param name="rows">The summaries read for the date range.</param>
    /// <param name="profileId">The profile to keep, or <see langword="null"/> for every profile.</param>
    public static IReadOnlyList<AIVoiceSessionSummaryIndex> Calls(IEnumerable<AIVoiceSessionSummaryIndex> rows, string profileId)
        => rows
            .Where(row => string.IsNullOrEmpty(profileId) || string.Equals(row.AIProfileId, profileId, StringComparison.Ordinal))
            .GroupBy(row => row.ActivityId ?? row.ItemId, StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(row => row.SessionDurationMs.HasValue)
                .ThenByDescending(row => string.Equals(row.Engine, nameof(AIVoiceSessionEngine.Realtime), StringComparison.Ordinal))
                .ThenByDescending(row => row.CreatedUtc)
                .First())
            .ToList();

    /// <summary>
    /// The usage of a set of calls.
    /// </summary>
    /// <param name="label">What the set is.</param>
    /// <param name="calls">The calls.</param>
    /// <param name="textTokensBySession">The text completion tokens recorded against each chat session.</param>
    public static AIVoiceUsageSummaryViewModel Summarize(string label, IReadOnlyCollection<AIVoiceSessionSummaryIndex> calls, IReadOnlyDictionary<string, long> textTokensBySession)
    {
        var sessions = calls.Where(call => call.SessionDurationMs.HasValue).ToList();
        var callerMeasured = calls.Where(call => call.CallerSpeakingMs.HasValue).ToList();
        var silenceMeasured = calls.Where(call => call.MutualSilenceMs.HasValue).ToList();
        var firstAudio = calls.Where(call => call.TimeToFirstAssistantAudioMs.HasValue).ToList();
        var interruptible = calls.Where(call => call.BargeIns.HasValue).ToList();
        var audioTokens = calls
            .Where(call => call.InputAudioTokens.HasValue || call.OutputAudioTokens.HasValue)
            .Select(call => (call.InputAudioTokens ?? 0) + (call.OutputAudioTokens ?? 0))
            .ToList();

        return new AIVoiceUsageSummaryViewModel
        {
            Label = label,
            Calls = calls.Count,
            SessionMinutes = Minutes(sessions.Sum(call => call.SessionDurationMs.Value)),
            AIMinutes = Minutes(calls.Sum(call => call.AssistantSpeakingMs ?? 0)),
            CallerMinutes = callerMeasured.Count == 0 ? null : Minutes(callerMeasured.Sum(call => call.CallerSpeakingMs.Value)),
            SilenceMinutes = silenceMeasured.Count == 0 ? null : Minutes(silenceMeasured.Sum(call => call.MutualSilenceMs.Value)),
            AverageSessionSeconds = sessions.Count == 0 ? null : Math.Round(sessions.Average(call => call.SessionDurationMs.Value) / 1_000d, 1),
            AverageTimeToFirstAudioMs = firstAudio.Count == 0 ? null : Math.Round(firstAudio.Average(call => call.TimeToFirstAssistantAudioMs.Value), 0),
            HandoffRate = calls.Count == 0
                ? 0
                : calls.Count(call => string.Equals(call.Outcome, nameof(AIVoiceSessionOutcome.HandedToAgent), StringComparison.Ordinal)) / (double)calls.Count,
            BargeInsPerCall = interruptible.Count == 0 ? null : Math.Round(interruptible.Average(call => call.BargeIns.Value), 2),
            IdlePrompts = calls.Sum(call => call.IdlePrompts),
            TextTokens = calls
                .Select(call => call.AISessionId)
                .Where(sessionId => !string.IsNullOrEmpty(sessionId))
                .Distinct(StringComparer.Ordinal)
                .Sum(sessionId => textTokensBySession.GetValueOrDefault(sessionId)),
            AudioTokens = audioTokens.Count == 0 ? null : audioTokens.Sum(),
        };
    }

    /// <summary>
    /// The rows of the voice table, grouped as asked.
    /// </summary>
    /// <param name="calls">The calls.</param>
    /// <param name="groupBy">What to group by.</param>
    /// <param name="profileNames">Current profile names by identifier.</param>
    /// <param name="toLocal">Converts a UTC time to the site's local time, for grouping by day.</param>
    /// <param name="textTokensBySession">The text completion tokens recorded against each chat session.</param>
    public static IReadOnlyList<AIVoiceUsageSummaryViewModel> BuildRows(
        IReadOnlyCollection<AIVoiceSessionSummaryIndex> calls,
        AIVoiceUsageGroupBy groupBy,
        IReadOnlyDictionary<string, string> profileNames,
        Func<DateTime, DateTime> toLocal,
        IReadOnlyDictionary<string, long> textTokensBySession)
    {
        var rows = calls
            .GroupBy(call => Label(call, groupBy, profileNames, toLocal), StringComparer.Ordinal)
            .Select(group => Summarize(group.Key, group.ToList(), textTokensBySession));

        // A day is read in date order; everything else by how much it was used.
        return groupBy == AIVoiceUsageGroupBy.Day
            ? rows.OrderBy(row => row.Label, StringComparer.Ordinal).ToList()
            : rows.OrderByDescending(row => row.Calls).ThenBy(row => row.Label, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string Label(AIVoiceSessionSummaryIndex call, AIVoiceUsageGroupBy groupBy, IReadOnlyDictionary<string, string> profileNames, Func<DateTime, DateTime> toLocal)
        => groupBy switch
        {
            AIVoiceUsageGroupBy.Deployment => string.IsNullOrEmpty(call.DeploymentName)
                ? Unknown
                : string.IsNullOrEmpty(call.ModelName) ? call.DeploymentName : $"{call.DeploymentName} ({call.ModelName})",
            AIVoiceUsageGroupBy.Profile => (string.IsNullOrEmpty(call.AIProfileId) ? null : profileNames.GetValueOrDefault(call.AIProfileId))
                ?? call.AIProfileName
                ?? call.AIProfileId
                ?? Unknown,
            AIVoiceUsageGroupBy.Campaign => string.IsNullOrEmpty(call.CampaignId) ? NoCampaign : call.CampaignName ?? call.CampaignId,
            AIVoiceUsageGroupBy.Channel => call.Channel ?? Unknown,
            AIVoiceUsageGroupBy.Engine => string.Equals(call.Engine, nameof(AIVoiceSessionEngine.Realtime), StringComparison.Ordinal) ? "Realtime" : "Turn-based",
            AIVoiceUsageGroupBy.Day => toLocal(call.StartedUtc ?? call.EndedUtc ?? call.CreatedUtc).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => Unknown,
        };

    private static double Minutes(long milliseconds)
        => Math.Round(milliseconds / MillisecondsPerMinute, 1);
}
