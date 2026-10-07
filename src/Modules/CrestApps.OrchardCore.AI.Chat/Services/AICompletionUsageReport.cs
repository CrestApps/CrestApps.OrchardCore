using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.ViewModels;

namespace CrestApps.OrchardCore.AI.Chat.Services;

/// <summary>
/// The completion half of the AI usage report: which completion records count, and how they are grouped.
/// </summary>
internal static class AICompletionUsageReport
{
    private const string Unknown = "Unknown";

    /// <summary>
    /// The records the report counts: the chat completions that belong to a chat session or a chat interaction, and to
    /// the chosen profile when one is chosen. Embeddings, speech and realtime requests made for a session are metered
    /// too, but are reported in the metered usage table rather than counted as completions.
    /// </summary>
    /// <param name="records">The records read for the date range.</param>
    /// <param name="profileId">The profile to keep, or <see langword="null"/> for every profile.</param>
    public static IReadOnlyList<AICompletionUsageRecord> Relevant(IEnumerable<AICompletionUsageRecord> records, string profileId)
        => records
            .Where(record => AIUsageReport.GetOperationType(record) == AIUsageOperationTypes.Chat)
            .Where(record => !string.IsNullOrEmpty(record.SessionId) || !string.IsNullOrEmpty(record.InteractionId))
            .Where(record => string.IsNullOrEmpty(profileId) || string.Equals(record.ProfileId, profileId, StringComparison.Ordinal))
            .ToList();

    /// <summary>
    /// The rows of the completion table, grouped as asked.
    /// </summary>
    /// <param name="records">The records the report counts.</param>
    /// <param name="groupBy">What to group by.</param>
    /// <param name="profileNames">Profile names by identifier.</param>
    public static IReadOnlyList<AICompletionUsageSummaryViewModel> BuildRows(
        IReadOnlyCollection<AICompletionUsageRecord> records,
        AICompletionUsageGroupBy groupBy,
        IReadOnlyDictionary<string, string> profileNames)
    {
        if (groupBy == AICompletionUsageGroupBy.UserAndModel)
        {
            return records
                .GroupBy(record => (UserLabel: GetUserLabel(record), record.IsAuthenticated, ClientName: record.ClientName ?? Unknown, ModelName: GetModelLabel(record)))
                .Select(group => Summarize(group, row =>
                {
                    row.UserLabel = group.Key.UserLabel;
                    row.IsAuthenticated = group.Key.IsAuthenticated;
                    row.ClientName = group.Key.ClientName;
                    row.ModelName = group.Key.ModelName;
                }))
                .OrderByDescending(row => row.TotalTokens)
                .ThenByDescending(row => row.TotalCalls)
                .ThenBy(row => row.UserLabel, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return records
            .GroupBy(record => Label(record, groupBy, profileNames), StringComparer.Ordinal)
            .Select(group => Summarize(group, row => row.GroupLabel = group.Key))
            .OrderByDescending(row => row.TotalTokens)
            .ThenByDescending(row => row.TotalCalls)
            .ThenBy(row => row.GroupLabel, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// The text tokens recorded against each chat session, so an automated call can be shown with the tokens its
    /// completions used: every chat completion's tokens, and the text part of every realtime response.
    /// </summary>
    /// <param name="records">The records read for the date range.</param>
    public static IReadOnlyDictionary<string, long> TokensBySession(IEnumerable<AICompletionUsageRecord> records)
        => records
            .Where(record => !string.IsNullOrEmpty(record.SessionId))
            .GroupBy(record => record.SessionId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Sum(TextTokens), StringComparer.Ordinal);

    /// <summary>
    /// The audio tokens recorded against each chat session by realtime voice responses and their transcriptions.
    /// Sessions with none are left out.
    /// </summary>
    /// <param name="records">The records read for the date range.</param>
    public static IReadOnlyDictionary<string, long> AudioTokensBySession(IEnumerable<AICompletionUsageRecord> records)
        => records
            .Where(record => !string.IsNullOrEmpty(record.SessionId))
            .Where(record => record.InputAudioTokenCount > 0 || record.OutputAudioTokenCount > 0)
            .GroupBy(record => record.SessionId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Sum(record => (long)record.InputAudioTokenCount + record.OutputAudioTokenCount), StringComparer.Ordinal);

    private static long TextTokens(AICompletionUsageRecord record)
        => AIUsageReport.GetOperationType(record) switch
        {
            AIUsageOperationTypes.Chat => record.TotalTokenCount,
            AIUsageOperationTypes.Realtime or AIUsageOperationTypes.RealtimeTranscription
                => Math.Max(0L, (long)record.TotalTokenCount - record.InputAudioTokenCount - record.OutputAudioTokenCount),
            _ => 0,
        };

    // A provider that reports no model leaves the name empty rather than null, so fall back on the deployment.
    private static string GetModelLabel(AICompletionUsageRecord record)
        => !string.IsNullOrEmpty(record.ModelName)
            ? record.ModelName
            : !string.IsNullOrEmpty(record.DeploymentName) ? record.DeploymentName : Unknown;

    private static string Label(AICompletionUsageRecord record, AICompletionUsageGroupBy groupBy, IReadOnlyDictionary<string, string> profileNames)
        => groupBy switch
        {
            AICompletionUsageGroupBy.Model => GetModelLabel(record),
            AICompletionUsageGroupBy.Deployment => record.DeploymentName ?? Unknown,
            AICompletionUsageGroupBy.Connection => record.ConnectionName ?? Unknown,
            AICompletionUsageGroupBy.Profile => string.IsNullOrEmpty(record.ProfileId)
                ? Unknown
                : profileNames.GetValueOrDefault(record.ProfileId) ?? record.ProfileId,
            _ => Unknown,
        };

    private static AICompletionUsageSummaryViewModel Summarize(IEnumerable<AICompletionUsageRecord> group, Action<AICompletionUsageSummaryViewModel> label)
    {
        var records = group.ToList();
        var latencySamples = records.Where(record => record.ResponseLatencyMs > 0).ToList();

        var row = new AICompletionUsageSummaryViewModel
        {
            TotalCalls = records.Count,
            TotalSessions = records.Select(record => record.SessionId).Where(sessionId => !string.IsNullOrEmpty(sessionId)).Distinct(StringComparer.Ordinal).Count(),
            TotalChatInteractions = records.Select(record => record.InteractionId).Where(interactionId => !string.IsNullOrEmpty(interactionId)).Distinct(StringComparer.Ordinal).Count(),
            TotalInputTokens = records.Sum(record => (long)record.InputTokenCount),
            TotalOutputTokens = records.Sum(record => (long)record.OutputTokenCount),
            TotalTokens = records.Sum(record => (long)record.TotalTokenCount),
            AverageResponseLatencyMs = latencySamples.Count > 0
                ? Math.Round(latencySamples.Average(record => record.ResponseLatencyMs), 0)
                : 0,
        };

        label(row);

        return row;
    }

    private static string GetUserLabel(AICompletionUsageRecord record)
    {
        if (!string.IsNullOrEmpty(record.UserName))
        {
            return record.UserName;
        }

        if (record.IsAuthenticated && !string.IsNullOrEmpty(record.UserId))
        {
            return record.UserId;
        }

        return "Anonymous";
    }
}
