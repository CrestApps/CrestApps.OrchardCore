using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;

namespace CrestApps.OrchardCore.AI.Chat.Reports;

/// <summary>
/// The chat AI profiles the AI chat reports filter by and name their rows after.
/// </summary>
internal static class AIChatReportProfiles
{
    /// <summary>
    /// The name of each chat AI profile by its identifier: its display text, or its technical name without one.
    /// </summary>
    /// <param name="profileStore">The AI profile store.</param>
    /// <returns>The names, by profile identifier.</returns>
    public static async Task<IReadOnlyDictionary<string, string>> GetNamesAsync(IAIProfileStore profileStore)
        => (await profileStore.GetByTypeAsync(AIProfileType.Chat))
            .Where(profile => !string.IsNullOrEmpty(profile.ItemId))
            .GroupBy(profile => profile.ItemId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => string.IsNullOrWhiteSpace(group.First().DisplayText) ? group.First().Name : group.First().DisplayText,
                StringComparer.Ordinal);
}
